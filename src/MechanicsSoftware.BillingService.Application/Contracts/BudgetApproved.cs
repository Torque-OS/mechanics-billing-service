// Saga contract published by the Billing Service (ADR-011, F4-16).
// The namespace is part of the message identity in MassTransit (urn:message:TorqueOS.Contracts.Billing:<Name>):
// consumers in other repositories must declare this exact namespace and type name.
// Evolution: add fields only; a breaking change gets a new type (e.g. <Name>V2). See the README.
namespace TorqueOS.Contracts.Billing;

/// <summary>Customer or staff approved the budget; the order moves to AWAITING_PAYMENT.</summary>
public sealed record BudgetApproved(
    Guid ServiceOrderId,
    Guid BudgetId,
    DateTime OccurredAt);
