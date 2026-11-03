using MechanicsSoftware.BillingService.Application.Abstractions;
using MechanicsSoftware.BillingService.Application.UseCases.Budgets.Commands;
using MechanicsSoftware.BillingService.Domain.Entities;
using MechanicsSoftware.BillingService.Domain.Exceptions;
using MechanicsSoftware.BillingService.Domain.ValueObjects;

namespace MechanicsSoftware.BillingService.Application.UseCases.Budgets.Handlers;

public sealed class GenerateBudgetHandler(IBillingDbContext db, IBudgetEventPublisher publisher)
{
    public async Task<GenerateBudgetResult> ExecuteAsync(
        GenerateBudgetCommand command, CancellationToken cancellationToken = default)
    {
        var existing = await db.Budgets.FindByServiceOrderAsync(command.ServiceOrderId, cancellationToken);
        if (existing is not null)
            return GenerateBudgetResult.Existing(BudgetResponse.From(existing));

        Budget budget;
        try
        {
            budget = Budget.Create(command.ServiceOrderId, command.CustomerId, ToDrafts(command.Items));
        }
        catch (DomainException exception)
        {
            // Events go through the transactional outbox: published before SaveChanges, sent only once it commits.
            await publisher.PublishBudgetFailedAsync(command.ServiceOrderId, exception.Message, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            return GenerateBudgetResult.Rejected(exception.Message);
        }

        db.Budgets.Add(budget);
        await publisher.PublishBudgetGeneratedAsync(budget, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return GenerateBudgetResult.Generated(BudgetResponse.From(budget));
    }

    private static List<BudgetItemDraft> ToDrafts(IReadOnlyList<GenerateBudgetItem>? items) =>
        items?
            .Select(item => new BudgetItemDraft(
                item.Type,
                item.ReferenceId,
                item.Description,
                new Money(item.UnitPriceInCents),
                item.Quantity))
            .ToList()
        ?? [];
}
