using MechanicsSoftware.BillingService.Application.Abstractions;
using MechanicsSoftware.BillingService.Application.Exceptions;
using MechanicsSoftware.BillingService.Domain.Entities;
using MechanicsSoftware.BillingService.Domain.Exceptions;

namespace MechanicsSoftware.BillingService.Application.UseCases.Payments;

/// <summary>
/// Reads the order from Mercado Pago and records the outcome — the provider, not the webhook body, is the
/// source of truth. Applies at most once: a payment that already reached a final status is left untouched,
/// so repeated or out-of-order notifications never publish a second Saga event.
/// The event goes through the transactional outbox (published before SaveChanges, sent once it commits);
/// the history entry is written after the commit.
/// </summary>
public sealed class PaymentSynchronizer(
    IBillingDbContext db, IMercadoPagoClient mercadoPago, IPaymentEventPublisher publisher, IPaymentHistory history)
{
    private static readonly HashSet<string> FailedStatuses =
        new(StringComparer.OrdinalIgnoreCase) { "canceled", "expired", "failed" };

    public async Task SyncAsync(Payment payment, CancellationToken cancellationToken = default)
    {
        if (payment.ProviderOrderId is null)
            throw new ConflictException("Payment has no provider order to verify.");

        if (payment.Status.IsFinal)
            return;

        var order = await mercadoPago.GetOrderAsync(payment.ProviderOrderId, cancellationToken);

        string historyEvent;
        if (string.Equals(order.Status, "processed", StringComparison.OrdinalIgnoreCase))
        {
            payment.Confirm();
            await publisher.PublishPaymentConfirmedAsync(payment, cancellationToken);
            historyEvent = "confirmed";
        }
        else if (FailedStatuses.Contains(order.Status))
        {
            payment.Fail($"Mercado Pago order {order.Status}" +
                         (string.IsNullOrWhiteSpace(order.StatusDetail) ? "" : $" ({order.StatusDetail})"));
            await publisher.PublishPaymentFailedAsync(payment, cancellationToken);
            historyEvent = "failed";
        }
        else
        {
            // Any other status (created, action_required, …) means still pending: nothing to record.
            return;
        }

        await db.SaveChangesAsync(cancellationToken);

        await history.RecordAsync(Entry(payment, historyEvent, order), cancellationToken);
    }

    internal static PaymentHistoryEntry Entry(Payment payment, string historyEvent, MercadoPagoOrder? order = null) =>
        new(
            payment.Id,
            payment.ServiceOrderId,
            payment.BudgetId,
            historyEvent,
            payment.Status.ToString(),
            payment.Amount.Cents,
            payment.ProviderOrderId,
            order?.Status,
            order?.RawJson,
            payment.FailureReason,
            payment.FailedAt ?? payment.ConfirmedAt ?? payment.CreatedAt);

    internal static PaymentProviderException MissingQrData(string orderId) =>
        new($"Mercado Pago order '{orderId}' was created without qr_data.");
}
