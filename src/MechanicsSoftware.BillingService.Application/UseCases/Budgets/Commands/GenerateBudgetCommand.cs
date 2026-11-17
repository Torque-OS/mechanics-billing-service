namespace MechanicsSoftware.BillingService.Application.UseCases.Budgets.Commands;

public sealed record GenerateBudgetCommand(
    Guid ServiceOrderId,
    Guid CustomerId,
    IReadOnlyList<GenerateBudgetItem> Items);
