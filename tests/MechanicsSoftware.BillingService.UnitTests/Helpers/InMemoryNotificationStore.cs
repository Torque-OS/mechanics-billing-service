using System.Collections.Concurrent;
using MechanicsSoftware.BillingService.Application.Abstractions;

namespace MechanicsSoftware.BillingService.UnitTests.Helpers;

public sealed class InMemoryNotificationStore : INotificationStore
{
    private readonly ConcurrentQueue<MercadoPagoNotification> _saved = new();

    public IReadOnlyList<MercadoPagoNotification> Saved => _saved.ToList();

    public Task SaveAsync(MercadoPagoNotification notification, CancellationToken cancellationToken = default)
    {
        _saved.Enqueue(notification);
        return Task.CompletedTask;
    }
}
