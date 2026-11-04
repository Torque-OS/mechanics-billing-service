using System.Diagnostics.CodeAnalysis;
using MassTransit;
using MechanicsSoftware.BillingService.Application.Exceptions;
using MechanicsSoftware.BillingService.Application.UseCases.Budgets.Handlers;
using MechanicsSoftware.BillingService.Domain.Exceptions;
using MechanicsSoftware.BillingService.Infrastructure.Persistence;
using TorqueOS.Contracts.Billing;

namespace MechanicsSoftware.BillingService.Infrastructure.Messaging;

public sealed class CancelBudgetConsumer(CancelBudgetHandler handler) : IConsumer<CancelBudget>
{
    public Task Consume(ConsumeContext<CancelBudget> context) =>
        handler.ExecuteForServiceOrderAsync(context.Message.ServiceOrderId, context.CancellationToken);
}

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
