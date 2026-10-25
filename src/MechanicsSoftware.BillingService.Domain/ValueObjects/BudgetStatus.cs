using MechanicsSoftware.BillingService.Domain.Exceptions;

namespace MechanicsSoftware.BillingService.Domain.ValueObjects;

public sealed class BudgetStatus(BudgetStatus.Status value) : ValueObject
{
    public enum Status
    {
        Pending,
        Approved,
        Rejected
    }

    public Status Value => value;

    public bool IsDecided => Value != Status.Pending;

    public static BudgetStatus CreatePending() => new(Status.Pending);

    public BudgetStatus TransitionTo(Status newStatus)
    {
        var target = new BudgetStatus(newStatus);

        if (IsDecided || !target.IsDecided)
            throw new InvalidStatusTransitionException(this, target);

        return target;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value switch
    {
        Status.Pending => "PENDING",
        Status.Approved => "APPROVED",
        Status.Rejected => "REJECTED",
        _ => Value.ToString()
    };
}
