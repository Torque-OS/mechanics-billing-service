using MechanicsSoftware.BillingService.Domain.Entities;

namespace MechanicsSoftware.BillingService.Application.UseCases.Budgets;

public sealed record BudgetResponse(
    Guid Id,
    Guid ServiceOrderId,
    Guid CustomerId,
    string Status,
    int TotalInCents,
    string TotalFormatted,
    DateTime CreatedAt,
    DateTime? DecidedAt,
    IReadOnlyList<BudgetItemResponse> Items,
    DateTime? CancelledAt)
{
    public static BudgetResponse From(Budget budget) =>
        new(
            budget.Id,
            budget.ServiceOrderId,
            budget.CustomerId,
            budget.Status.ToString(),
            budget.Total.Cents,
            budget.Total.ToFormatted(),
            budget.CreatedAt,
            budget.DecidedAt,
            budget.Items.Select(BudgetItemResponse.From).ToList(),
            budget.CancelledAt);
}
