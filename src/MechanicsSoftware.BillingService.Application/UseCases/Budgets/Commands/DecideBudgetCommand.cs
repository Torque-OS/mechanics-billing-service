namespace MechanicsSoftware.BillingService.Application.UseCases.Budgets.Commands;

public sealed record DecideBudgetCommand(
    Guid BudgetId,
    BudgetDecision Decision);
