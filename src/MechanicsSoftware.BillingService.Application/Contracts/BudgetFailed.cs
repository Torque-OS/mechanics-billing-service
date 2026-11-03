// Saga contract published by the Billing Service (ADR-011, F4-16).
// The namespace is part of the message identity in MassTransit (urn:message:TorqueOS.Contracts.Billing:<Name>):
// consumers in other repositories must declare this exact namespace and type name.
// Evolution: add fields only; a breaking change gets a new type (e.g. <Name>V2). See the README.
namespace TorqueOS.Contracts.Billing;

/// <summary>The snapshot could not become a budget (e.g. no service item); the order is cancelled.</summary>
public sealed record BudgetFailed(
    Guid ServiceOrderId,
    string Reason,
    DateTime OccurredAt);
