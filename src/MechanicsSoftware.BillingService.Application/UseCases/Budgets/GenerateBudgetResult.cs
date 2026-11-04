namespace MechanicsSoftware.BillingService.Application.UseCases.Budgets;

public sealed record GenerateBudgetResult(BudgetResponse? Budget, bool Created, string? FailureReason)
{
    public bool Failed => Budget is null;

    public static GenerateBudgetResult Generated(BudgetResponse budget) => new(budget, Created: true, null);

    public static GenerateBudgetResult Existing(BudgetResponse budget) => new(budget, Created: false, null);

    public static GenerateBudgetResult Rejected(string reason) => new(null, Created: false, reason);
}
