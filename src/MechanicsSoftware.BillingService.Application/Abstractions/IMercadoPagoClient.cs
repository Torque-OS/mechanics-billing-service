using MechanicsSoftware.BillingService.Domain.ValueObjects;

namespace MechanicsSoftware.BillingService.Application.Abstractions;

public interface IMercadoPagoClient
{
    Task<MercadoPagoOrder> CreateQrOrderAsync(
        MercadoPagoQrOrderRequest request, string idempotencyKey, CancellationToken cancellationToken = default);

    Task<MercadoPagoOrder> GetOrderAsync(string orderId, CancellationToken cancellationToken = default);

    Task<MercadoPagoOrder> RefundOrderAsync(string orderId, string idempotencyKey, CancellationToken cancellationToken = default);

    Task<MercadoPagoOrder> CancelOrderAsync(string orderId, string idempotencyKey, CancellationToken cancellationToken = default);
}

public sealed record MercadoPagoQrOrderRequest(string ExternalReference, Money Amount, string Description);

public sealed record MercadoPagoOrder(string Id, string Status, string? StatusDetail, string? QrData, string? RawJson = null);
