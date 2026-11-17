using System.Text.Json.Serialization;
using MechanicsSoftware.BillingService.Application.UseCases.Budgets.Commands;

namespace MechanicsSoftware.BillingService.Api.Transport.Budgets;

public sealed record BudgetDecisionRequest(
    [property: JsonConverter(typeof(JsonStringEnumConverter<BudgetDecision>))] BudgetDecision Decision);
