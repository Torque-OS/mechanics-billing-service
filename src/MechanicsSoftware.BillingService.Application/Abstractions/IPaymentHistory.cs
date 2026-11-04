namespace MechanicsSoftware.BillingService.Application.Abstractions;

public interface IPaymentHistory
{
    Task RecordAsync(PaymentHistoryEntry entry, CancellationToken cancellationToken = default);
}

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
