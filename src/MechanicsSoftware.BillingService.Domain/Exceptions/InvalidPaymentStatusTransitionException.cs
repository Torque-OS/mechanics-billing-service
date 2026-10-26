using MechanicsSoftware.BillingService.Domain.ValueObjects;

namespace MechanicsSoftware.BillingService.Domain.Exceptions;

public class InvalidPaymentStatusTransitionException : ConflictException
{
    public InvalidPaymentStatusTransitionException(PaymentStatus currentStatus, PaymentStatus attemptedStatus)
        : base($"Invalid payment transition from '{currentStatus}' to '{attemptedStatus}'.")
    {
    }
}
