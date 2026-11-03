// Saga contract consumed by the Billing Service (ADR-011, F4-17).
// The namespace is part of the message identity in MassTransit (urn:message:TorqueOS.Contracts.Billing:<Name>):
// the OS Service must declare this exact namespace and type name when sending it.
// Evolution: add fields only; a breaking change gets a new type (e.g. <Name>V2). See the README.
namespace TorqueOS.Contracts.Billing;

/// <summary>
/// Compensation command from the OS Service: the order was cancelled, so cancel its budget and settle the payment —
/// an open QR is cancelled, a received payment is refunded. The OS only sends it when the order may still be
/// cancelled (never for a completed order). Answered with <see cref="BudgetCancelled"/>, also when repeated.
/// </summary>
public sealed record CancelBudget(Guid ServiceOrderId);
