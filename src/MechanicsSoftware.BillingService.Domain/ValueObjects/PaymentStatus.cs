using MechanicsSoftware.BillingService.Domain.Exceptions;

namespace MechanicsSoftware.BillingService.Domain.ValueObjects;

public sealed class PaymentStatus(PaymentStatus.Status value) : ValueObject
{
    public enum Status
    {
        Pending,
        Confirmed,
        Failed
    }

    public Status Value => value;

    public bool IsFinal => Value != Status.Pending;

    public static PaymentStatus CreatePending() => new(Status.Pending);

    public PaymentStatus TransitionTo(Status newStatus)
    {
        var target = new PaymentStatus(newStatus);

        if (IsFinal || !target.IsFinal)
            throw new InvalidPaymentStatusTransitionException(this, target);

        return target;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value switch
    {
        Status.Pending => "PENDING",
        Status.Confirmed => "CONFIRMED",
        Status.Failed => "FAILED",
        _ => Value.ToString()
    };
}
