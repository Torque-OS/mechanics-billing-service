using MechanicsSoftware.BillingService.Application.Abstractions;

namespace MechanicsSoftware.BillingService.Application.UseCases.Budgets.Handlers;

public sealed class GetBudgetHandler(IBillingDbContext db)
{
    public async Task<BudgetResponse> ExecuteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var budget = await db.Budgets.FindWithItemsAsync(id, cancellationToken);

        return BudgetResponse.From(budget);
    }
}
