using MassTransit;
using MechanicsSoftware.BillingService.Application.Abstractions;
using MechanicsSoftware.BillingService.Domain.Entities;
using TorqueOS.Contracts.Billing;

namespace MechanicsSoftware.BillingService.Infrastructure.Messaging;

/// <summary>
/// Publishes the payment Saga events through the EF Core bus outbox — see <see cref="MassTransitBudgetEventPublisher"/>.
/// </summary>
public sealed class MassTransitPaymentEventPublisher(IPublishEndpoint bus) : IPaymentEventPublisher
{
    public Task PublishPaymentConfirmedAsync(Payment payment, CancellationToken cancellationToken = default) =>
        bus.Publish(
            new PaymentConfirmed(payment.ServiceOrderId, payment.Id, payment.BudgetId, payment.Amount.Cents,
                payment.ConfirmedAt ?? DateTime.UtcNow),
            context => context.CorrelationId = payment.ServiceOrderId,
            cancellationToken);

    public Task PublishPaymentFailedAsync(Payment payment, CancellationToken cancellationToken = default) =>
        bus.Publish(
            new PaymentFailed(payment.ServiceOrderId, payment.Id, payment.BudgetId, payment.FailureReason ?? "unknown",
                payment.FailedAt ?? DateTime.UtcNow),
            context => context.CorrelationId = payment.ServiceOrderId,
            cancellationToken);
}
