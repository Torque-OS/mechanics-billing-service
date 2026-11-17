namespace MechanicsSoftware.BillingService.Application.Exceptions;

public class PaymentProviderException : Exception
{
    public PaymentProviderException(string message, Exception? innerException = null)
        : base(message, innerException) { }
}
