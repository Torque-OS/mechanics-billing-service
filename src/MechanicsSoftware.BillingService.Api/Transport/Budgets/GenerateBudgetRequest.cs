using System.Text.Json.Serialization;
using MechanicsSoftware.BillingService.Application.UseCases.Budgets.Commands;
using MechanicsSoftware.BillingService.Domain.Enums;

namespace MechanicsSoftware.BillingService.Api.Transport.Budgets;

public sealed record GenerateBudgetRequest(
    Guid ServiceOrderId,
    Guid CustomerId,
    IReadOnlyList<GenerateBudgetItemRequest> Items)
{
    public GenerateBudgetCommand ToCommand() =>
        new(ServiceOrderId, CustomerId, Items?.Select(item => item.ToItem()).ToList() ?? []);
}

public sealed record GenerateBudgetItemRequest(
    [property: JsonConverter(typeof(JsonStringEnumConverter<BudgetItemType>))] BudgetItemType Type,
    Guid ReferenceId,
    string Description,
    int UnitPriceInCents,
    int Quantity)
{
    public GenerateBudgetItem ToItem() => new(Type, ReferenceId, Description, UnitPriceInCents, Quantity);
}
