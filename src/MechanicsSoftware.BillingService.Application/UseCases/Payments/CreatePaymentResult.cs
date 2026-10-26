namespace MechanicsSoftware.BillingService.Application.UseCases.Payments;

/// <summary>Creation is idempotent per budget: a repeated request returns the existing payment (<see cref="Created"/> = false).</summary>
public sealed record CreatePaymentResult(PaymentResponse Payment, bool Created);
