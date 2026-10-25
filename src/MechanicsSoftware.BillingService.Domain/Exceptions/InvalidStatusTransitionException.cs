using MechanicsSoftware.BillingService.Domain.ValueObjects;

namespace MechanicsSoftware.BillingService.Domain.Exceptions;

public class InvalidStatusTransitionException : ConflictException
{
    public InvalidStatusTransitionException(BudgetStatus currentStatus, BudgetStatus attemptedStatus)
        : base($"Invalid budget transition from '{currentStatus}' to '{attemptedStatus}'.")
    {
    }
}
