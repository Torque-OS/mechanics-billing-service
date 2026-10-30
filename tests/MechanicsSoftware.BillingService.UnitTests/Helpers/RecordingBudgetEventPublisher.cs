using System.Collections.Concurrent;
using MechanicsSoftware.BillingService.Application.Abstractions;
using MechanicsSoftware.BillingService.Domain.Entities;

namespace MechanicsSoftware.BillingService.UnitTests.Helpers;

public sealed record RecordedBudgetEvent(string Name, Guid ServiceOrderId);

public sealed class RecordingBudgetEventPublisher : IBudgetEventPublisher
{
    private readonly ConcurrentQueue<RecordedBudgetEvent> _events = new();

    public IReadOnlyList<RecordedBudgetEvent> For(Guid serviceOrderId) =>
        _events.Where(e => e.ServiceOrderId == serviceOrderId).ToList();

    public Task PublishBudgetGeneratedAsync(Budget budget, CancellationToken cancellationToken = default) =>
        Record("BudgetGenerated", budget.ServiceOrderId);

    public Task PublishBudgetFailedAsync(Guid serviceOrderId, string reason, CancellationToken cancellationToken = default) =>
        Record("BudgetFailed", serviceOrderId);

    public Task PublishBudgetApprovedAsync(Budget budget, CancellationToken cancellationToken = default) =>
        Record("BudgetApproved", budget.ServiceOrderId);

    public Task PublishBudgetRejectedAsync(Budget budget, CancellationToken cancellationToken = default) =>
        Record("BudgetRejected", budget.ServiceOrderId);

    public Task PublishBudgetCancelledAsync(Budget budget, CancellationToken cancellationToken = default) =>
        Record("BudgetCancelled", budget.ServiceOrderId);

    private Task Record(string name, Guid serviceOrderId)
    {
        _events.Enqueue(new RecordedBudgetEvent(name, serviceOrderId));
        return Task.CompletedTask;
    }
}
