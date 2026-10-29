using MassTransit;
using MechanicsSoftware.BillingService.Application.Abstractions;
using MechanicsSoftware.BillingService.Domain.Entities;
using TorqueOS.Contracts.Billing;

namespace MechanicsSoftware.BillingService.Infrastructure.Messaging;

/// <summary>
/// Publishes the budget Saga events (ADR-011). <see cref="IPublishEndpoint"/> is scoped to the request and backed by
/// the EF Core bus outbox: messages are stored with the next <c>SaveChanges</c> and delivered to RabbitMQ after it
/// commits. The service order id is the correlation id of every message.
/// </summary>
public sealed class MassTransitBudgetEventPublisher(IPublishEndpoint bus) : IBudgetEventPublisher
{
    public Task PublishBudgetGeneratedAsync(Budget budget, CancellationToken cancellationToken = default) =>
        Publish(new BudgetGenerated(budget.ServiceOrderId, budget.Id, budget.Total.Cents, DateTime.UtcNow),
            budget.ServiceOrderId, cancellationToken);

    public Task PublishBudgetFailedAsync(Guid serviceOrderId, string reason, CancellationToken cancellationToken = default) =>
        Publish(new BudgetFailed(serviceOrderId, reason, DateTime.UtcNow), serviceOrderId, cancellationToken);

    public Task PublishBudgetApprovedAsync(Budget budget, CancellationToken cancellationToken = default) =>
        Publish(new BudgetApproved(budget.ServiceOrderId, budget.Id, budget.DecidedAt ?? DateTime.UtcNow),
            budget.ServiceOrderId, cancellationToken);

    public Task PublishBudgetRejectedAsync(Budget budget, CancellationToken cancellationToken = default) =>
        Publish(new BudgetRejected(budget.ServiceOrderId, budget.Id, budget.DecidedAt ?? DateTime.UtcNow),
            budget.ServiceOrderId, cancellationToken);

    private Task Publish<T>(T message, Guid serviceOrderId, CancellationToken cancellationToken) where T : class =>
        bus.Publish(message, context => context.CorrelationId = serviceOrderId, cancellationToken);
}
