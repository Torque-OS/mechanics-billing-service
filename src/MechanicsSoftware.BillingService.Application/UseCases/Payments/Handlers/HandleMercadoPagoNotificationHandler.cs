using MechanicsSoftware.BillingService.Application.Abstractions;
using MechanicsSoftware.BillingService.Application.Exceptions;

namespace MechanicsSoftware.BillingService.Application.UseCases.Payments.Handlers;

public enum NotificationOutcome
{
    /// <summary>Signature valid and stored, but it does not refer to a payment of ours (or carries no id).</summary>
    Ignored,
    /// <summary>The referenced payment was re-read from Mercado Pago.</summary>
    Synchronized
}

/// <summary>
/// Webhook entry point: rejects bad signatures, keeps the raw notification, then verifies the payment
/// against Mercado Pago instead of trusting the notification body.
/// </summary>
public sealed class HandleMercadoPagoNotificationHandler(
    IBillingDbContext db,
    IWebhookSignatureValidator signatureValidator,
    INotificationStore notificationStore,
    PaymentSynchronizer synchronizer)
{
    public async Task<NotificationOutcome> ExecuteAsync(
        MercadoPagoNotification notification, CancellationToken cancellationToken = default)
    {
        if (!signatureValidator.IsValid(notification.Signature, notification.RequestId, notification.DataId))
            throw new InvalidWebhookSignatureException();

        await notificationStore.SaveAsync(notification, cancellationToken);

        if (string.IsNullOrWhiteSpace(notification.DataId))
            return NotificationOutcome.Ignored;

        var payment = await db.Payments.FindByProviderOrderAsync(notification.DataId, cancellationToken);
        if (payment is null)
            return NotificationOutcome.Ignored;

        await synchronizer.SyncAsync(payment, cancellationToken);
        return NotificationOutcome.Synchronized;
    }
}
