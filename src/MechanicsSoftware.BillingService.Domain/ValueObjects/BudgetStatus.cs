using MechanicsSoftware.BillingService.Domain.Exceptions;

namespace MechanicsSoftware.BillingService.Domain.ValueObjects;

/// <summary>
/// Pending → Approved | Rejected | Cancelled; Approved → Cancelled (Saga compensation). Rejected and Cancelled are final.
/// </summary>
public sealed class BudgetStatus(BudgetStatus.Status value) : ValueObject
{
    public enum Status
    {
        Pending,
        Approved,
        Rejected,
        Cancelled
    }

    public Status Value => value;

    public bool IsDecided => Value != Status.Pending;

    public static BudgetStatus CreatePending() => new(Status.Pending);

    public BudgetStatus TransitionTo(Status newStatus)
    {
        var target = new BudgetStatus(newStatus);

        if (!CanTransitionTo(newStatus))
            throw new InvalidStatusTransitionException(this, target);

        return target;
    }

    private bool CanTransitionTo(Status target) => (Value, target) switch
    {
        (Status.Pending, Status.Approved or Status.Rejected or Status.Cancelled) => true,
        (Status.Approved, Status.Cancelled) => true,
        _ => false
    };

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value switch
    {
        Status.Pending => "PENDING",
        Status.Approved => "APPROVED",
        Status.Rejected => "REJECTED",
        Status.Cancelled => "CANCELLED",
        _ => Value.ToString()
    };
}
