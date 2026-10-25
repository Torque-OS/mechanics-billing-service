namespace MechanicsSoftware.BillingService.Application.UseCases.Budgets;

/// <summary>
/// Outcome of a generation request. Generation is idempotent per service order, so a repeated
/// request returns the existing budget (<see cref="Created"/> = false). A rejected snapshot has
/// already been reported as <c>BudgetFailed</c>; <see cref="FailureReason"/> carries the reason.
/// </summary>
public sealed record GenerateBudgetResult(BudgetResponse? Budget, bool Created, string? FailureReason)
{
    public bool Failed => Budget is null;

    public static GenerateBudgetResult Generated(BudgetResponse budget) => new(budget, Created: true, null);

    public static GenerateBudgetResult Existing(BudgetResponse budget) => new(budget, Created: false, null);

    public static GenerateBudgetResult Rejected(string reason) => new(null, Created: false, reason);
}
