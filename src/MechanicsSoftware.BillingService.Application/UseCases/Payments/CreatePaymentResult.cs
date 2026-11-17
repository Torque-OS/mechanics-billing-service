namespace MechanicsSoftware.BillingService.Application.UseCases.Payments;

public sealed record CreatePaymentResult(PaymentResponse Payment, bool Created);
