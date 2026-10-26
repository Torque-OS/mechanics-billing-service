namespace MechanicsSoftware.BillingService.Application.Exceptions;

/// <summary>The payment provider (Mercado Pago) failed or answered something unusable. Mapped to 502.</summary>
public sealed class PaymentProviderException : Exception
{
    public PaymentProviderException(string message, Exception? innerException = null)
        : base(message, innerException) { }
}
