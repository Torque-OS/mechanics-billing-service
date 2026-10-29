using MechanicsSoftware.BillingService.Domain.ValueObjects;

namespace MechanicsSoftware.BillingService.Application.Abstractions;

/// <summary>Mercado Pago Orders API (QR). Failures surface as <c>PaymentProviderException</c>.</summary>
public interface IMercadoPagoClient
{
    /// <param name="idempotencyKey">Sent as <c>X-Idempotency-Key</c>; the payment id, so a retry never creates a second order.</param>
    Task<MercadoPagoOrder> CreateQrOrderAsync(
        MercadoPagoQrOrderRequest request, string idempotencyKey, CancellationToken cancellationToken = default);

    Task<MercadoPagoOrder> GetOrderAsync(string orderId, CancellationToken cancellationToken = default);
}

/// <param name="ExternalReference">Our payment id — lets an order be traced back without a lookup table.</param>
public sealed record MercadoPagoQrOrderRequest(string ExternalReference, Money Amount, string Description);

/// <param name="Status">Order status as returned by Mercado Pago (e.g. <c>created</c>, <c>processed</c>, <c>expired</c>).</param>
/// <param name="QrData">EMVCo payload to render as a QR image; present on dynamic QR creation.</param>
/// <param name="RawJson">Response body as received, kept for the payment history.</param>
public sealed record MercadoPagoOrder(string Id, string Status, string? StatusDetail, string? QrData, string? RawJson = null);
