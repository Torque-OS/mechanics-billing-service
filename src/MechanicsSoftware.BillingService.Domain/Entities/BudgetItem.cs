using MechanicsSoftware.BillingService.Domain.Enums;
using MechanicsSoftware.BillingService.Domain.Exceptions;
using MechanicsSoftware.BillingService.Domain.ValueObjects;

namespace MechanicsSoftware.BillingService.Domain.Entities;

public sealed class BudgetItem : Entity<Guid>
{
    public Guid BudgetId { get; private set; }
    public BudgetItemType Type { get; private set; }
    public Guid ReferenceId { get; private set; }
    public string Description { get; private set; } = null!;
    public Money UnitPrice { get; private set; } = null!;
    public int Quantity { get; private set; }
    public Money Total => UnitPrice.Multiply(Quantity);

    private BudgetItem() { }

    internal static BudgetItem Create(Guid budgetId, BudgetItemDraft draft)
    {
        if (draft is null)
            throw new DomainException("Budget item is required.");

        if (!Enum.IsDefined(draft.Type))
            throw new DomainException($"Budget item type '{draft.Type}' is invalid.");

        if (draft.ReferenceId == Guid.Empty)
            throw new DomainException("Budget item reference id is required.");

        if (string.IsNullOrWhiteSpace(draft.Description))
            throw new DomainException("Budget item description is required.");

        if (draft.UnitPrice is null)
            throw new DomainException("Budget item unit price is required.");

        if (draft.Quantity <= 0)
            throw new DomainException("Budget item quantity must be greater than zero.");

        return new BudgetItem
        {
            Id = Guid.NewGuid(),
            BudgetId = budgetId,
            Type = draft.Type,
            ReferenceId = draft.ReferenceId,
            Description = draft.Description.Trim(),
            UnitPrice = draft.UnitPrice,
            Quantity = draft.Quantity
        };
    }
}
