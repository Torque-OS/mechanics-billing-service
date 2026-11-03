using System.Collections.Concurrent;
using MechanicsSoftware.BillingService.Application.Abstractions;

namespace MechanicsSoftware.BillingService.UnitTests.Helpers;

public sealed class InMemoryPaymentHistory : IPaymentHistory
{
    private readonly ConcurrentQueue<PaymentHistoryEntry> _entries = new();

    public IReadOnlyList<PaymentHistoryEntry> For(Guid paymentId) => _entries.Where(e => e.PaymentId == paymentId).ToList();

    public Task RecordAsync(PaymentHistoryEntry entry, CancellationToken cancellationToken = default)
    {
        _entries.Enqueue(entry);
        return Task.CompletedTask;
    }
}
