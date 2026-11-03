using System.Security.Cryptography;
using System.Text;
using MechanicsSoftware.BillingService.Application.Abstractions;

namespace MechanicsSoftware.BillingService.Infrastructure.MercadoPago;

/// <summary>
/// Mercado Pago webhook signature: header <c>x-signature: ts=&lt;ts&gt;,v1=&lt;hex&gt;</c>; manifest
/// <c>id:&lt;data.id&gt;;request-id:&lt;x-request-id&gt;;ts:&lt;ts&gt;;</c> (absent pairs are omitted);
/// <c>v1</c> = HMAC-SHA256(secret, manifest) in hex. Fails closed: no secret configured means nothing is valid.
/// <para>
/// The docs say to lower-case an alphanumeric <c>data.id</c>, but Order notifications captured in the sandbox
/// (2026-10-26) are signed with the id exactly as received (<c>ORD…</c> upper-case). Both forms are accepted;
/// either one still requires the secret.
/// </para>
/// </summary>
public sealed class MercadoPagoSignatureValidator(MercadoPagoOptions options) : IWebhookSignatureValidator
{
    public bool IsValid(string? signatureHeader, string? requestId, string? dataId)
    {
        if (options.WebhookSecret is null || string.IsNullOrWhiteSpace(signatureHeader))
            return false;

        string? ts = null, v1 = null;
        foreach (var part in signatureHeader.Split(','))
        {
            var pair = part.Split('=', 2, StringSplitOptions.TrimEntries);
            if (pair.Length != 2) continue;
            if (pair[0] == "ts") ts = pair[1];
            else if (pair[0] == "v1") v1 = pair[1];
        }

        if (string.IsNullOrEmpty(ts) || string.IsNullOrEmpty(v1))
            return false;

        var received = Encoding.ASCII.GetBytes(v1.ToLowerInvariant());
        var lowerCaseId = dataId?.ToLowerInvariant();

        return Matches(options.WebhookSecret, BuildManifest(dataId, requestId, ts), received)
               || (lowerCaseId is not null && !string.Equals(lowerCaseId, dataId, StringComparison.Ordinal)
                   && Matches(options.WebhookSecret, BuildManifest(lowerCaseId, requestId, ts), received));
    }

    private static bool Matches(string secret, string manifest, byte[] received) =>
        CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(ComputeSignature(secret, manifest)), received);

    internal static string BuildManifest(string? dataId, string? requestId, string ts)
    {
        var manifest = new StringBuilder();
        if (!string.IsNullOrEmpty(dataId)) manifest.Append("id:").Append(dataId).Append(';');
        if (!string.IsNullOrEmpty(requestId)) manifest.Append("request-id:").Append(requestId).Append(';');
        manifest.Append("ts:").Append(ts).Append(';');
        return manifest.ToString();
    }

    internal static string ComputeSignature(string secret, string manifest) =>
        Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(manifest)))
            .ToLowerInvariant();
}
