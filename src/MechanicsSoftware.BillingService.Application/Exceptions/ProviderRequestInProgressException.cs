namespace MechanicsSoftware.BillingService.Application.Exceptions;

public sealed class ProviderRequestInProgressException : PaymentProviderException
{
    public ProviderRequestInProgressException(string message)
        : base(message) { }
}
