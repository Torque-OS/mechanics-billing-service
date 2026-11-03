namespace MechanicsSoftware.BillingService.Application.Abstractions;

/// <summary>Append-only store of raw provider notifications (MongoDB, ADR-012).</summary>
public interface INotificationStore
{
    Task SaveAsync(MercadoPagoNotification notification, CancellationToken cancellationToken = default);
}

/// <summary>A webhook call as received: headers and query that identify it, plus the untouched body.</summary>
public sealed record MercadoPagoNotification(
    string? Signature,
    string? RequestId,
    string? DataId,
    string? Action,
    string RawBody,
    DateTime ReceivedAt);
