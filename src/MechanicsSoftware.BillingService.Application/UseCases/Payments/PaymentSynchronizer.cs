using MechanicsSoftware.BillingService.Application.Abstractions;
using MechanicsSoftware.BillingService.Application.Exceptions;
using MechanicsSoftware.BillingService.Domain.Entities;
using MechanicsSoftware.BillingService.Domain.Exceptions;

namespace MechanicsSoftware.BillingService.Application.UseCases.Payments;

/// <summary>
/// Reads the order from Mercado Pago and records the outcome — the provider, not the webhook body, is the
/// source of truth. Applies at most once: a payment that already reached a final status is left untouched,
/// so repeated or out-of-order notifications never publish a second Saga event.
/// </summary>
public sealed class PaymentSynchronizer(
    IBillingDbContext db, IMercadoPagoClient mercadoPago, IPaymentEventPublisher publisher)
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

        if (string.Equals(order.Status, "processed", StringComparison.OrdinalIgnoreCase))
        {
            payment.Confirm();
            await db.SaveChangesAsync(cancellationToken);
            await publisher.PublishPaymentConfirmedAsync(payment, cancellationToken);
        }
        else if (FailedStatuses.Contains(order.Status))
        {
            payment.Fail($"Mercado Pago order {order.Status}" +
                         (string.IsNullOrWhiteSpace(order.StatusDetail) ? "" : $" ({order.StatusDetail})"));
            await db.SaveChangesAsync(cancellationToken);
            await publisher.PublishPaymentFailedAsync(payment, cancellationToken);
        }
        // Any other status (created, action_required, …) means still pending: nothing to record.
    }

    internal static PaymentProviderException MissingQrData(string orderId) =>
        new($"Mercado Pago order '{orderId}' was created without qr_data.");
}
