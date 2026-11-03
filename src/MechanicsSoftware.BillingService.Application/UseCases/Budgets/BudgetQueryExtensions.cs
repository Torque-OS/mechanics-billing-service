using MechanicsSoftware.BillingService.Application.Exceptions;
using MechanicsSoftware.BillingService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MechanicsSoftware.BillingService.Application.UseCases.Budgets;

internal static class BudgetQueryExtensions
{
    internal static async Task<Budget> FindWithItemsAsync(
        this IQueryable<Budget> query,
        Guid id,
        CancellationToken cancellationToken = default) =>
        await query
            .Include(b => b.Items)
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken)
        ?? throw new NotFoundException(nameof(Budget), id);

    internal static Task<Budget?> FindByServiceOrderAsync(
        this IQueryable<Budget> query,
        Guid serviceOrderId,
        CancellationToken cancellationToken = default) =>
        query
            .Include(b => b.Items)
            .FirstOrDefaultAsync(b => b.ServiceOrderId == serviceOrderId, cancellationToken);
}
