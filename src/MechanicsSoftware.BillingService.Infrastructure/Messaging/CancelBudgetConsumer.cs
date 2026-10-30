using System.Diagnostics.CodeAnalysis;
using MassTransit;
using MechanicsSoftware.BillingService.Application.Exceptions;
using MechanicsSoftware.BillingService.Application.UseCases.Budgets.Handlers;
using MechanicsSoftware.BillingService.Domain.Exceptions;
using MechanicsSoftware.BillingService.Infrastructure.Persistence;
using TorqueOS.Contracts.Billing;

namespace MechanicsSoftware.BillingService.Infrastructure.Messaging;

/// <summary>Saga compensation command from the OS Service (ADR-011, F4-17). Answered with <c>BudgetCancelled</c>.</summary>
public sealed class CancelBudgetConsumer(CancelBudgetHandler handler) : IConsumer<CancelBudget>
{
    public Task Consume(ConsumeContext<CancelBudget> context) =>
        handler.ExecuteForServiceOrderAsync(context.Message.ServiceOrderId, context.CancellationToken);
}

/// <summary>
/// Queue <c>billing.cancel-budget</c>, bound to the <c>CancelBudget</c> exchange. Mercado Pago or the database being
/// briefly unavailable is retried; business conflicts (e.g. a REJECTED budget) and unknown orders are not. A message that
/// still fails goes to <c>billing.cancel-budget_error</c> and can be moved back once the cause is fixed.
/// The EF Core inbox drops redeliveries of an already handled <c>MessageId</c>, and the outbox commits
/// <c>BudgetCancelled</c> together with the cancellation.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Endpoint wiring needs PostgreSQL and RabbitMQ; exercised manually against the compose stack.")]
public sealed class CancelBudgetConsumerDefinition : ConsumerDefinition<CancelBudgetConsumer>
{
    public const string QueueName = "billing.cancel-budget";

    public CancelBudgetConsumerDefinition() => EndpointName = QueueName;

    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<CancelBudgetConsumer> consumerConfigurator,
        IRegistrationContext context)
    {
        endpointConfigurator.UseMessageRetry(retry =>
        {
            retry.Intervals(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15));
            retry.Ignore<DomainException>();
            retry.Ignore<NotFoundException>();
        });

        endpointConfigurator.UseEntityFrameworkOutbox<BillingDbContext>(context);
    }
}
