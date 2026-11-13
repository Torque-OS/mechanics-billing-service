using MechanicsSoftware.BillingService.Application.Abstractions;
using MechanicsSoftware.BillingService.Application.Exceptions;
using MechanicsSoftware.BillingService.Domain.Entities;
using MechanicsSoftware.BillingService.Domain.Exceptions;
using MechanicsSoftware.BillingService.Domain.ValueObjects;

namespace MechanicsSoftware.BillingService.Application.UseCases.Payments;

public sealed class PaymentSynchronizer(
    IBillingDbContext db, IMercadoPagoClient mercadoPago, IPaymentEventPublisher publisher, IPaymentHistory history)
{
    private static readonly HashSet<string> FailedStatuses =
        new(StringComparer.OrdinalIgnoreCase) { "canceled", "expired", "failed", "refunded" };

    public async Task SyncAsync(Payment payment, CancellationToken cancellationToken = default)
    {
        if (payment.ProviderOrderId is null)
            throw new ConflictException("Payment has no provider order to verify.");

        if (payment.Status.IsFinal)
            return;

        var order = await mercadoPago.GetOrderAsync(payment.ProviderOrderId, cancellationToken);

        string historyEvent;
        if (payment.Status.Value == PaymentStatus.Status.Confirmed)
        {
            if (!string.Equals(order.Status, "refunded", StringComparison.OrdinalIgnoreCase))
                return;

            payment.Refund();
            historyEvent = "refunded";
        }
        else if (string.Equals(order.Status, "processed", StringComparison.OrdinalIgnoreCase))
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
            return;
        }

        await db.SaveChangesAsync(cancellationToken);

        await history.RecordAsync(Entry(payment, historyEvent, order), cancellationToken);
    }

    internal static PaymentHistoryEntry Entry(
        Payment payment, string historyEvent, MercadoPagoOrder? order = null, string? failure = null) =>
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
            failure ?? payment.FailureReason,
            failure is not null
                ? DateTime.UtcNow
                : payment.RefundedAt ?? payment.CancelledAt ?? payment.FailedAt ?? payment.ConfirmedAt ?? payment.CreatedAt);

    internal static PaymentProviderException MissingQrData(string orderId) =>
        new($"Mercado Pago order '{orderId}' was created without qr_data.");
}
