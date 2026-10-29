using MechanicsSoftware.BillingService.Application.Abstractions;
using MechanicsSoftware.BillingService.Application.Exceptions;
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

        if (!command.Requester.IsStaff && !budget.BelongsTo(command.Requester.UserId))
            throw new ForbiddenException("Customers can only decide their own budgets.");

        if (command.Decision == BudgetDecision.Approve)
            budget.Approve();
        else
            budget.Reject();

        // Transactional outbox: the event is stored with the decision and sent only once SaveChanges commits.
        if (command.Decision == BudgetDecision.Approve)
            await publisher.PublishBudgetApprovedAsync(budget, cancellationToken);
        else
            await publisher.PublishBudgetRejectedAsync(budget, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        return BudgetResponse.From(budget);
    }
}
