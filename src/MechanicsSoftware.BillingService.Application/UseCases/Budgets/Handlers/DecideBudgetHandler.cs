using MechanicsSoftware.BillingService.Application.Abstractions;
using MechanicsSoftware.BillingService.Application.UseCases.Budgets.Commands;
using MechanicsSoftware.BillingService.Domain.Exceptions;

namespace MechanicsSoftware.BillingService.Application.UseCases.Budgets.Handlers;

public sealed class DecideBudgetHandler(IBillingDbContext db, IBudgetEventPublisher publisher)
{
    public async Task<BudgetResponse> ExecuteAsync(
        DecideBudgetCommand command, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(command.Decision))
            throw new DomainException($"Budget decision '{command.Decision}' is invalid.");

        var budget = await db.Budgets.FindWithItemsAsync(command.BudgetId, cancellationToken);

        if (command.Decision == BudgetDecision.Approve)
            budget.Approve();
        else
            budget.Reject();

        await db.SaveChangesAsync(cancellationToken);

        if (command.Decision == BudgetDecision.Approve)
            await publisher.PublishBudgetApprovedAsync(budget, cancellationToken);
        else
            await publisher.PublishBudgetRejectedAsync(budget, cancellationToken);

        return BudgetResponse.From(budget);
    }
}
