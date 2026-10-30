namespace MechanicsSoftware.BillingService.Application.Abstractions;

/// <summary>
/// Append-only audit trail of each payment's lifecycle (MongoDB, ADR-012). Written after the change is committed;
/// implementations must not throw — a missing history entry never undoes or blocks a payment.
/// </summary>
public interface IPaymentHistory
{
    Task RecordAsync(PaymentHistoryEntry entry, CancellationToken cancellationToken = default);
}

/// <param name="Event">
/// <c>created</c>, <c>confirmed</c>, <c>failed</c>, <c>cancelled</c>, <c>refunded</c>; or <c>cancel_failed</c> /
/// <c>refund_failed</c> when Mercado Pago refused a cancellation (status unchanged, <c>Reason</c> = the error).
/// </param>
/// <param name="ProviderSnapshotJson">Mercado Pago order as read when the decision was taken (null on creation).</param>
public sealed record PaymentHistoryEntry(
    Guid PaymentId,
    Guid ServiceOrderId,
    Guid BudgetId,
    string Event,
    string Status,
    int AmountInCents,
    string? ProviderOrderId,
    string? ProviderStatus,
    string? ProviderSnapshotJson,
    string? Reason,
    DateTime OccurredAt);
