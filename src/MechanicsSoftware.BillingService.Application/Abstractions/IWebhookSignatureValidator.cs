namespace MechanicsSoftware.BillingService.Application.Abstractions;

/// <summary>Checks the <c>x-signature</c> header Mercado Pago sends with every webhook.</summary>
public interface IWebhookSignatureValidator
{
    bool IsValid(string? signatureHeader, string? requestId, string? dataId);
}
