namespace MechanicsSoftware.BillingService.Application.Exceptions;

public sealed class InvalidWebhookSignatureException : Exception
{
    public InvalidWebhookSignatureException()
        : base("Invalid webhook signature.") { }
}
