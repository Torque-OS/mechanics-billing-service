namespace MechanicsSoftware.BillingService.Application.Exceptions;

/// <summary>A webhook whose <c>x-signature</c> does not match. Mapped to 401.</summary>
public sealed class InvalidWebhookSignatureException : Exception
{
    public InvalidWebhookSignatureException()
        : base("Invalid webhook signature.") { }
}
