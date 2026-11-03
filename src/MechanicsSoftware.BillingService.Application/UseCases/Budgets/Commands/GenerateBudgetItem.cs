using MechanicsSoftware.BillingService.Domain.Enums;

namespace MechanicsSoftware.BillingService.Application.UseCases.Budgets.Commands;

public sealed record GenerateBudgetItem(
    BudgetItemType Type,
    Guid ReferenceId,
    string Description,
    int UnitPriceInCents,
    int Quantity);
