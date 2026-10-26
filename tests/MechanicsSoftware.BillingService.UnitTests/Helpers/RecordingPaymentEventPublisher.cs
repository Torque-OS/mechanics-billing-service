using System.Collections.Concurrent;
using MechanicsSoftware.BillingService.Application.Abstractions;
using MechanicsSoftware.BillingService.Domain.Entities;

namespace MechanicsSoftware.BillingService.UnitTests.Helpers;

public sealed class RecordingPaymentEventPublisher : IPaymentEventPublisher
{
    private readonly ConcurrentQueue<RecordedBudgetEvent> _events = new();

    public IReadOnlyList<string> For(Guid serviceOrderId) =>
        _events.Where(e => e.ServiceOrderId == serviceOrderId).Select(e => e.Name).ToList();

    public Task PublishPaymentConfirmedAsync(Payment payment, CancellationToken cancellationToken = default) =>
        Record("PaymentConfirmed", payment.ServiceOrderId);

    public Task PublishPaymentFailedAsync(Payment payment, CancellationToken cancellationToken = default) =>
        Record("PaymentFailed", payment.ServiceOrderId);

    private Task Record(string name, Guid serviceOrderId)
    {
        _events.Enqueue(new RecordedBudgetEvent(name, serviceOrderId));
        return Task.CompletedTask;
    }
}
