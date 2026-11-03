// Saga contract published by the Billing Service (ADR-011, F4-17).
// The namespace is part of the message identity in MassTransit (urn:message:TorqueOS.Contracts.Billing:<Name>):
// consumers in other repositories must declare this exact namespace and type name.
// Evolution: add fields only; a breaking change gets a new type (e.g. <Name>V2). See the README.
namespace TorqueOS.Contracts.Billing;

/// <summary>
/// Answer to <see cref="CancelBudget"/>: the budget is cancelled and nothing is left to charge — an open QR was
/// cancelled and a received payment was refunded. Published again when the command is repeated.
/// </summary>
public sealed record BudgetCancelled(
    Guid ServiceOrderId,
    Guid BudgetId,
    DateTime OccurredAt);
