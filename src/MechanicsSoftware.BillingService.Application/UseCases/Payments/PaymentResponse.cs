using MechanicsSoftware.BillingService.Domain.Entities;

namespace MechanicsSoftware.BillingService.Application.UseCases.Payments;

public sealed record PaymentResponse(
    Guid Id,
    Guid BudgetId,
    Guid ServiceOrderId,
    string Status,
    int AmountInCents,
    string AmountFormatted,
    string? ProviderOrderId,
    string? QrData,
    string? FailureReason,
    DateTime CreatedAt,
    DateTime? ConfirmedAt,
    DateTime? FailedAt)
{
    public static PaymentResponse From(Payment payment) =>
        new(
            payment.Id,
            payment.BudgetId,
            payment.ServiceOrderId,
            payment.Status.ToString(),
            payment.Amount.Cents,
            payment.Amount.ToFormatted(),
            payment.ProviderOrderId,
            payment.QrData,
            payment.FailureReason,
            payment.CreatedAt,
            payment.ConfirmedAt,
            payment.FailedAt);
}
