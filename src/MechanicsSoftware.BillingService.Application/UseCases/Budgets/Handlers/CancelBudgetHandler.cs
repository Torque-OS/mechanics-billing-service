using MechanicsSoftware.BillingService.Application.Abstractions;
using MechanicsSoftware.BillingService.Application.Exceptions;
using MechanicsSoftware.BillingService.Application.UseCases.Payments;
using MechanicsSoftware.BillingService.Domain.Entities;
using MechanicsSoftware.BillingService.Domain.ValueObjects;

namespace MechanicsSoftware.BillingService.Application.UseCases.Budgets.Handlers;

/// <summary>
/// Saga compensation (ADR-011, F4-17): cancels the budget of a cancelled service order and settles its payment —
/// an open QR is cancelled, a received payment is refunded; there is no refund without a cancellation.
/// Whether the order may still be cancelled (a completed one may not) is decided by the OS Service, which only then
/// sends <c>CancelBudget</c>; here only the payment state matters.
/// <para>
/// Mercado Pago is called first and nothing is stored if it fails, so repeating the cancellation is the retry; the
/// <c>X-Idempotency-Key</c> is fixed per payment, so a refund is never made twice. Repeating a cancellation that
/// already happened publishes <c>BudgetCancelled</c> again without calling Mercado Pago.
/// </para>
/// </summary>
public sealed class CancelBudgetHandler(
    IBillingDbContext db, IMercadoPagoClient mercadoPago, IBudgetEventPublisher publisher, IPaymentHistory history)
{
    private static readonly HashSet<string> ClosedUnpaidStatuses =
        new(StringComparer.OrdinalIgnoreCase) { "canceled", "expired", "failed" };

    /// <summary>Staff endpoint.</summary>
    public async Task<BudgetResponse> ExecuteAsync(Guid budgetId, CancellationToken cancellationToken = default) =>
        await CancelAsync(await db.Budgets.FindWithItemsAsync(budgetId, cancellationToken), cancellationToken);

    /// <summary><c>CancelBudget</c> command from the Saga.</summary>
    public async Task<BudgetResponse> ExecuteForServiceOrderAsync(
        Guid serviceOrderId, CancellationToken cancellationToken = default)
    {
        var budget = await db.Budgets.FindByServiceOrderAsync(serviceOrderId, cancellationToken)
                     ?? throw new NotFoundException($"{nameof(Budget)} of service order", serviceOrderId);

        return await CancelAsync(budget, cancellationToken);
    }

    private async Task<BudgetResponse> CancelAsync(Budget budget, CancellationToken cancellationToken)
    {
        if (budget.IsCancelled)
        {
            await publisher.PublishBudgetCancelledAsync(budget, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            return BudgetResponse.From(budget);
        }

        var payment = await db.Payments.FindByBudgetAsync(budget.Id, cancellationToken);
        var settlement = payment is null ? null : await SettleAsync(payment, cancellationToken);

        // Transactional outbox: BudgetCancelled is stored with both status changes and sent once SaveChanges commits.
        budget.Cancel(payment);
        await publisher.PublishBudgetCancelledAsync(budget, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        if (settlement is not null)
            await history.RecordAsync(
                PaymentSynchronizer.Entry(payment!, settlement.Value.Event, settlement.Value.Order), cancellationToken);

        return BudgetResponse.From(budget);
    }

    /// <summary>
    /// Closes the payment on Mercado Pago and then locally. The order is read first because it may have moved since
    /// our last sync: paid a moment ago, expired, refunded in the panel, or refunded by an earlier attempt whose
    /// commit failed. Returns the history event to record, or null when the payment was already closed.
    /// </summary>
    private async Task<(string Event, MercadoPagoOrder Order)?> SettleAsync(
        Payment payment, CancellationToken cancellationToken)
    {
        if (payment.Status.IsFinal)
            return null;

        var orderId = payment.ProviderOrderId
                      ?? throw new PaymentProviderException($"Payment '{payment.Id}' has no Mercado Pago order.");
        var order = await mercadoPago.GetOrderAsync(orderId, cancellationToken);
        var isPending = payment.Status.Value == PaymentStatus.Status.Pending;

        if (Is(order, "processed"))
        {
            order = await CallProviderAsync(payment, "refund_failed",
                () => mercadoPago.RefundOrderAsync(orderId, $"refund-{payment.Id}", cancellationToken),
                cancellationToken);
            RefundLocally(payment, isPending);
            return ("refunded", order);
        }

        if (Is(order, "refunded"))
        {
            RefundLocally(payment, isPending);
            return ("refunded", order);
        }

        if (isPending && (Is(order, "created") || Is(order, "action_required")))
        {
            order = await CallProviderAsync(payment, "cancel_failed",
                () => mercadoPago.CancelOrderAsync(orderId, $"cancel-{payment.Id}", cancellationToken),
                cancellationToken);
            payment.Cancel();
            return ("cancelled", order);
        }

        if (isPending && ClosedUnpaidStatuses.Contains(order.Status))
        {
            // The QR can no longer be paid: nothing to call on Mercado Pago.
            payment.Cancel();
            return ("cancelled", order);
        }

        throw new PaymentProviderException(
            $"Mercado Pago order {orderId} is '{order.Status}' while the payment is {payment.Status}; " +
            "it cannot be cancelled or refunded now.");
    }

    private static bool Is(MercadoPagoOrder order, string status) =>
        string.Equals(order.Status, status, StringComparison.OrdinalIgnoreCase);

    // Paid between our last sync and the cancellation: the confirmation is recorded on the way to the refund, without
    // PaymentConfirmed — the order is being cancelled, the Saga must not move it to execution.
    private static void RefundLocally(Payment payment, bool isPending)
    {
        if (isPending)
            payment.Confirm();

        payment.Refund();
    }

    private async Task<MercadoPagoOrder> CallProviderAsync(
        Payment payment, string failureEvent, Func<Task<MercadoPagoOrder>> call, CancellationToken cancellationToken)
    {
        try
        {
            return await call();
        }
        catch (PaymentProviderException exception)
        {
            await history.RecordAsync(
                PaymentSynchronizer.Entry(payment, failureEvent, failure: exception.Message), cancellationToken);
            throw;
        }
    }
}
