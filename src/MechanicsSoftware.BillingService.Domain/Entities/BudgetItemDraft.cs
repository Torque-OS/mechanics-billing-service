using MechanicsSoftware.BillingService.Domain.Enums;
using MechanicsSoftware.BillingService.Domain.ValueObjects;

namespace MechanicsSoftware.BillingService.Domain.Entities;

public sealed record BudgetItemDraft(
    BudgetItemType Type,
    Guid ReferenceId,
    string Description,
    Money UnitPrice,
    int Quantity);
