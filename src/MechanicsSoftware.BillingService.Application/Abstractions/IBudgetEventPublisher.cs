using MechanicsSoftware.BillingService.Domain.Entities;

namespace MechanicsSoftware.BillingService.Application.Abstractions;

public interface IBudgetEventPublisher
{
    Task PublishBudgetGeneratedAsync(Budget budget, CancellationToken cancellationToken = default);
    Task PublishBudgetFailedAsync(Guid serviceOrderId, string reason, CancellationToken cancellationToken = default);
    Task PublishBudgetApprovedAsync(Budget budget, CancellationToken cancellationToken = default);
    Task PublishBudgetRejectedAsync(Budget budget, CancellationToken cancellationToken = default);
    Task PublishBudgetCancelledAsync(Budget budget, CancellationToken cancellationToken = default);
}
