namespace MechanicsSoftware.BillingService.Application.UseCases.Budgets.Commands;

public sealed record BudgetRequester(Guid UserId, bool IsStaff)
{
    public static BudgetRequester Staff(Guid userId) => new(userId, IsStaff: true);

    public static BudgetRequester Customer(Guid customerId) => new(customerId, IsStaff: false);
}
