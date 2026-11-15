using System.Diagnostics.CodeAnalysis;
using MassTransit;
using MechanicsSoftware.BillingService.Application.UseCases.Budgets.Commands;
using MechanicsSoftware.BillingService.Application.UseCases.Budgets.Handlers;
using MechanicsSoftware.BillingService.Domain.Enums;
using MechanicsSoftware.BillingService.Infrastructure.Persistence;
using TorqueOS.Contracts.Billing;

namespace MechanicsSoftware.BillingService.Infrastructure.Messaging;

public sealed class RequestBudgetConsumer(GenerateBudgetHandler handler) : IConsumer<RequestBudget>
{
    public Task Consume(ConsumeContext<RequestBudget> context) =>
        handler.ExecuteAsync(ToCommand(context.Message), context.CancellationToken);

    private static GenerateBudgetCommand ToCommand(RequestBudget message) =>
        new(
            message.ServiceOrderId,
            message.CustomerId,
            (message.Items ?? [])
                .Select(item => new GenerateBudgetItem(
                    ParseType(item.Type), item.ReferenceId, item.Description, item.UnitPriceInCents, item.Quantity))
                .ToList());

    private static BudgetItemType ParseType(string? type) =>
        Enum.TryParse<BudgetItemType>(type, ignoreCase: true, out var parsed) ? parsed : (BudgetItemType)(-1);
}

[ExcludeFromCodeCoverage(Justification = "Endpoint wiring needs PostgreSQL and RabbitMQ; exercised manually against the compose stack.")]
public sealed class RequestBudgetConsumerDefinition : ConsumerDefinition<RequestBudgetConsumer>
{
    public const string QueueName = "billing.request-budget";

    public RequestBudgetConsumerDefinition() => EndpointName = QueueName;

    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<RequestBudgetConsumer> consumerConfigurator,
        IRegistrationContext context)
    {
        endpointConfigurator.UseMessageRetry(SagaRetryPolicy.Configure);
        endpointConfigurator.UseEntityFrameworkOutbox<BillingDbContext>(context);
    }
}
