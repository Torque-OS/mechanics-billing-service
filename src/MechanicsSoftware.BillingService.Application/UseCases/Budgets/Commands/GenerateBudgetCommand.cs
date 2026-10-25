namespace MechanicsSoftware.BillingService.Application.UseCases.Budgets.Commands;

/// <summary>
/// Item snapshot sent by the OS Service (ADR-010). <see cref="CustomerId"/> lets the Billing Service
/// check that a customer only decides their own budget.
/// </summary>
public sealed record GenerateBudgetCommand(
    Guid ServiceOrderId,
    Guid CustomerId,
    IReadOnlyList<GenerateBudgetItem> Items);
