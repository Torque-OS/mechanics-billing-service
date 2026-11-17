using MechanicsSoftware.BillingService.Application.Abstractions;
using MechanicsSoftware.BillingService.Application.Exceptions;

namespace MechanicsSoftware.BillingService.Application.UseCases.Payments.Handlers;

public enum NotificationOutcome
{
    Ignored,
    Synchronized
}

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
