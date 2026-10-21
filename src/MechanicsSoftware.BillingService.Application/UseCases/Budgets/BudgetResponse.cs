using MechanicsSoftware.BillingService.Domain.Entities;

namespace MechanicsSoftware.BillingService.Application.UseCases.Budgets;

public sealed record BudgetResponse(
    Guid Id,
    Guid ServiceOrderId,
    string Status,
    int TotalInCents,
    string TotalFormatted,
    DateTime CreatedAt,
    DateTime? DecidedAt,
    IReadOnlyList<BudgetItemResponse> Items)
{
    public static BudgetResponse From(Budget budget) =>
        new(
            budget.Id,
            budget.ServiceOrderId,
            budget.Status.ToString(),
            budget.Total.Cents,
            budget.Total.ToFormatted(),
            budget.CreatedAt,
            budget.DecidedAt,
            budget.Items.Select(BudgetItemResponse.From).ToList());
}
