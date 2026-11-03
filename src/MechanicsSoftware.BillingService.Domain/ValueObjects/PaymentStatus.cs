using MechanicsSoftware.BillingService.Domain.Exceptions;

namespace MechanicsSoftware.BillingService.Domain.ValueObjects;

/// <summary>
/// Pending → Confirmed | Failed | Cancelled; Confirmed → Refunded (Saga compensation).
/// Failed, Cancelled and Refunded are final.
/// </summary>
public sealed class PaymentStatus(PaymentStatus.Status value) : ValueObject
{
    public enum Status
    {
        Pending,
        Confirmed,
        Failed,
        Cancelled,
        Refunded
    }

    public Status Value => value;

    /// <summary>No further transition is possible (a confirmed payment can still be refunded).</summary>
    public bool IsFinal => Value is Status.Failed or Status.Cancelled or Status.Refunded;

    public static PaymentStatus CreatePending() => new(Status.Pending);

    public PaymentStatus TransitionTo(Status newStatus)
    {
        var target = new PaymentStatus(newStatus);

        if (!CanTransitionTo(newStatus))
            throw new InvalidPaymentStatusTransitionException(this, target);

        return target;
    }

    private bool CanTransitionTo(Status target) => (Value, target) switch
    {
        (Status.Pending, Status.Confirmed or Status.Failed or Status.Cancelled) => true,
        (Status.Confirmed, Status.Refunded) => true,
        _ => false
    };

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value switch
    {
        Status.Pending => "PENDING",
        Status.Confirmed => "CONFIRMED",
        Status.Failed => "FAILED",
        Status.Cancelled => "CANCELLED",
        Status.Refunded => "REFUNDED",
        _ => Value.ToString()
    };
}
