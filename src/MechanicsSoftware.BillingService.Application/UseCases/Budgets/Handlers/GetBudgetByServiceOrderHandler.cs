using MechanicsSoftware.BillingService.Application.Abstractions;
using MechanicsSoftware.BillingService.Application.Exceptions;
using MechanicsSoftware.BillingService.Application.UseCases.Budgets.Queries;
using MechanicsSoftware.BillingService.Domain.Entities;

namespace MechanicsSoftware.BillingService.Application.UseCases.Budgets.Handlers;

public sealed class GetBudgetByServiceOrderHandler(IBillingDbContext db)
{
    public async Task<BudgetResponse> ExecuteAsync(
        GetBudgetByServiceOrderQuery query, CancellationToken cancellationToken = default)
    {
        var budget = await db.Budgets.FindByServiceOrderAsync(query.ServiceOrderId, cancellationToken)
            ?? throw new NotFoundException($"{nameof(Budget)} for service order", query.ServiceOrderId);

        return BudgetResponse.From(budget);
    }
}
