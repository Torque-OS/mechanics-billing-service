using Microsoft.Extensions.Configuration;

namespace MechanicsSoftware.BillingService.Infrastructure.MercadoPago;

/// <summary>
/// Mercado Pago settings, read from environment variables only (never from versioned appsettings):
/// <c>MERCADOPAGO_ACCESS_TOKEN</c>, <c>MERCADOPAGO_WEBHOOK_SECRET</c>, <c>MERCADOPAGO_EXTERNAL_POS_ID</c>.
/// Missing values do not stop the service: the client fails per request and the webhook rejects everything.
/// </summary>
public sealed record MercadoPagoOptions(
    string? AccessToken,
    string? WebhookSecret,
    string? ExternalPosId,
    Uri BaseAddress,
    string ExpirationTime)
{
    public static readonly Uri DefaultBaseAddress = new("https://api.mercadopago.com/");

    /// <summary>Mercado Pago's own default validity for QR orders.</summary>
    public const string DefaultExpirationTime = "PT15M";

    public static MercadoPagoOptions FromConfiguration(IConfiguration configuration) =>
        new(
            Blank(configuration["MERCADOPAGO_ACCESS_TOKEN"]),
            Blank(configuration["MERCADOPAGO_WEBHOOK_SECRET"]),
            Blank(configuration["MERCADOPAGO_EXTERNAL_POS_ID"]),
            Uri.TryCreate(configuration["MERCADOPAGO_BASE_URL"], UriKind.Absolute, out var uri) ? uri : DefaultBaseAddress,
            Blank(configuration["MERCADOPAGO_QR_EXPIRATION"]) ?? DefaultExpirationTime);

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
