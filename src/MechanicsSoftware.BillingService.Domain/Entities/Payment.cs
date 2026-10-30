using MechanicsSoftware.BillingService.Domain.Exceptions;
using MechanicsSoftware.BillingService.Domain.ValueObjects;

namespace MechanicsSoftware.BillingService.Domain.Entities;

/// <summary>
/// Charge of an approved budget through a payment provider (Mercado Pago QR). The provider is the
/// source of truth for the outcome; this aggregate records it once: Pending → Confirmed | Failed.
/// Saga compensation (F4-17) closes it on the provider first: an open QR is Cancelled, a confirmed payment Refunded.
/// </summary>
public sealed class Payment : Entity<Guid>
{
    public Guid BudgetId { get; private set; }
    public Guid ServiceOrderId { get; private set; }
    public Guid CustomerId { get; private set; }
    public Money Amount { get; private set; } = null!;
    public PaymentStatus Status { get; private set; } = null!;
    public string? ProviderOrderId { get; private set; }
    public string? QrData { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ConfirmedAt { get; private set; }
    public DateTime? FailedAt { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    public DateTime? RefundedAt { get; private set; }

    private Payment() { }

    public static Payment CreateFor(Budget budget)
    {
        ArgumentNullException.ThrowIfNull(budget);

        if (budget.Status.Value != BudgetStatus.Status.Approved)
            throw new ConflictException($"Only approved budgets can be paid; budget is '{budget.Status}'.");

        if (budget.Total.Cents == 0)
            throw new DomainException("A budget with zero total cannot be charged.");

        return new Payment
        {
            Id = Guid.NewGuid(),
            BudgetId = budget.Id,
            ServiceOrderId = budget.ServiceOrderId,
            CustomerId = budget.CustomerId,
            Amount = budget.Total,
            Status = PaymentStatus.CreatePending(),
            CreatedAt = DateTime.UtcNow
        };
    }

    public bool BelongsTo(Guid customerId) => CustomerId == customerId;

    public void AttachProviderOrder(string providerOrderId, string qrData)
    {
        if (string.IsNullOrWhiteSpace(providerOrderId))
            throw new DomainException("Provider order id is required.");

        if (string.IsNullOrWhiteSpace(qrData))
            throw new DomainException("QR data is required.");

        if (ProviderOrderId is not null)
            throw new ConflictException("Payment already has a provider order.");

        ProviderOrderId = providerOrderId;
        QrData = qrData;
    }

    public void Confirm()
    {
        Status = Status.TransitionTo(PaymentStatus.Status.Confirmed);
        ConfirmedAt = DateTime.UtcNow;
    }

    public void Fail(string reason)
    {
        Status = Status.TransitionTo(PaymentStatus.Status.Failed);
        FailureReason = string.IsNullOrWhiteSpace(reason) ? "unknown" : reason.Trim();
        FailedAt = DateTime.UtcNow;
    }

    /// <summary>The QR was withdrawn before being paid (call only after the provider cancelled the order).</summary>
    public void Cancel()
    {
        Status = Status.TransitionTo(PaymentStatus.Status.Cancelled);
        CancelledAt = DateTime.UtcNow;
    }

    /// <summary>The money went back to the buyer (call only after the provider accepted the refund).</summary>
    public void Refund()
    {
        Status = Status.TransitionTo(PaymentStatus.Status.Refunded);
        RefundedAt = DateTime.UtcNow;
    }
}
