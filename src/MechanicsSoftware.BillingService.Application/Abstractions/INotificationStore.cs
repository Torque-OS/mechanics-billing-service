namespace MechanicsSoftware.BillingService.Application.Abstractions;

public interface INotificationStore
{
    Task SaveAsync(MercadoPagoNotification notification, CancellationToken cancellationToken = default);
}

public sealed record MercadoPagoNotification(
    string? Signature,
    string? RequestId,
    string? DataId,
    string? Action,
    string RawBody,
    DateTime ReceivedAt);
