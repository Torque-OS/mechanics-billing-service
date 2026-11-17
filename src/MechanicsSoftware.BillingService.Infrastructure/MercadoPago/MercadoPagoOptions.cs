using Microsoft.Extensions.Configuration;

namespace MechanicsSoftware.BillingService.Infrastructure.MercadoPago;

public sealed record MercadoPagoOptions(
    string? AccessToken,
    string? WebhookSecret,
    string? ExternalPosId,
    Uri BaseAddress,
    string ExpirationTime)
{
    public static readonly Uri DefaultBaseAddress = new("https://api.mercadopago.com/");

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
