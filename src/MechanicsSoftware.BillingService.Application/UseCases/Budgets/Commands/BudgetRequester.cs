namespace MechanicsSoftware.BillingService.Application.UseCases.Budgets.Commands;

/// <summary>
/// Who is acting on a budget, taken from the JWT: <see cref="UserId"/> is the <c>sub</c> claim
/// (the customer id for CUSTOMER tokens issued by mechanics-lambda).
/// </summary>
public sealed record BudgetRequester(Guid UserId, bool IsStaff)
{
    public static BudgetRequester Staff(Guid userId) => new(userId, IsStaff: true);

    public static BudgetRequester Customer(Guid customerId) => new(customerId, IsStaff: false);
}
