namespace TorqueOS.Contracts.Billing;

public sealed record BudgetCancelled(
    Guid ServiceOrderId,
    Guid BudgetId,
    DateTime OccurredAt);
