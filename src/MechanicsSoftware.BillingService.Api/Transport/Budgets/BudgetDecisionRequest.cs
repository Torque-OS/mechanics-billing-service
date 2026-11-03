using System.Text.Json.Serialization;
using MechanicsSoftware.BillingService.Application.UseCases.Budgets.Commands;

namespace MechanicsSoftware.BillingService.Api.Transport.Budgets;

/// <summary><c>{ "decision": "approve" | "reject" }</c> (case-insensitive).</summary>
public sealed record BudgetDecisionRequest(
    [property: JsonConverter(typeof(JsonStringEnumConverter<BudgetDecision>))] BudgetDecision Decision);
