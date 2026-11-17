namespace MechanicsSoftware.BillingService.Application.Abstractions;

public interface IWebhookSignatureValidator
{
    bool IsValid(string? signatureHeader, string? requestId, string? dataId);
}
