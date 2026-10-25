using MechanicsSoftware.BillingService.Application.Abstractions;
using MechanicsSoftware.BillingService.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace MechanicsSoftware.BillingService.Infrastructure.Messaging;

/// <summary>
/// Interim <see cref="IBudgetEventPublisher"/> that records each Saga event as a structured log entry.
/// It is replaced by the MassTransit/RabbitMQ publisher (with transactional outbox) once the broker
/// (F4-08) and the consumers (F4-13) exist — see ADR-011.
/// </summary>
public sealed partial class LoggingBudgetEventPublisher(ILogger<LoggingBudgetEventPublisher> logger)
    : IBudgetEventPublisher
{
    public Task PublishBudgetGeneratedAsync(Budget budget, CancellationToken cancellationToken = default)
    {
        LogBudgetGenerated(budget.ServiceOrderId, budget.Id, budget.Total.Cents);
        return Task.CompletedTask;
    }

    public Task PublishBudgetFailedAsync(Guid serviceOrderId, string reason, CancellationToken cancellationToken = default)
    {
        LogBudgetFailed(serviceOrderId, reason);
        return Task.CompletedTask;
    }

    public Task PublishBudgetApprovedAsync(Budget budget, CancellationToken cancellationToken = default)
    {
        LogBudgetDecided("BudgetApproved", budget.ServiceOrderId, budget.Id);
        return Task.CompletedTask;
    }

    public Task PublishBudgetRejectedAsync(Budget budget, CancellationToken cancellationToken = default)
    {
        LogBudgetDecided("BudgetRejected", budget.ServiceOrderId, budget.Id);
        return Task.CompletedTask;
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information,
        Message = "Saga event BudgetGenerated: ServiceOrder {ServiceOrderId}, Budget {BudgetId}, total {BudgetTotalInCents} cents.")]
    private partial void LogBudgetGenerated(Guid serviceOrderId, Guid budgetId, int budgetTotalInCents);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning,
        Message = "Saga event BudgetFailed: ServiceOrder {ServiceOrderId}, reason: {Reason}")]
    private partial void LogBudgetFailed(Guid serviceOrderId, string reason);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information,
        Message = "Saga event {EventName}: ServiceOrder {ServiceOrderId}, Budget {BudgetId}.")]
    private partial void LogBudgetDecided(string eventName, Guid serviceOrderId, Guid budgetId);
}
