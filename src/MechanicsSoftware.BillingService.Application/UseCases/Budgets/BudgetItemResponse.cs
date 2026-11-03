using MechanicsSoftware.BillingService.Domain.Entities;

namespace MechanicsSoftware.BillingService.Application.UseCases.Budgets;

public sealed record BudgetItemResponse(
    Guid Id,
    string Type,
    Guid ReferenceId,
    string Description,
    int UnitPriceInCents,
    int Quantity,
    int TotalInCents)
{
    public static BudgetItemResponse From(BudgetItem item) =>
        new(
            item.Id,
            item.Type.ToString(),
            item.ReferenceId,
            item.Description,
            item.UnitPrice.Cents,
            item.Quantity,
            item.Total.Cents);
}
