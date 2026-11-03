using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MechanicsSoftware.BillingService.Application.Abstractions;
using MechanicsSoftware.BillingService.Application.Exceptions;

namespace MechanicsSoftware.BillingService.Infrastructure.MercadoPago;

/// <summary>
/// Orders API client for dynamic QR (<c>POST /v1/orders</c>, <c>GET /v1/orders/{id}</c>, refund and cancel).
/// Every failure — missing configuration, HTTP error, unreadable body — becomes a <see cref="PaymentProviderException"/>.
/// </summary>
public sealed class MercadoPagoClient(HttpClient http, MercadoPagoOptions options) : IMercadoPagoClient
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public async Task<MercadoPagoOrder> CreateQrOrderAsync(
        MercadoPagoQrOrderRequest request, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        var posId = options.ExternalPosId
            ?? throw new PaymentProviderException("Mercado Pago is not configured: set MERCADOPAGO_EXTERNAL_POS_ID.");

        var amount = (request.Amount.Cents / 100m).ToString("0.00", CultureInfo.InvariantCulture);
        var body = new CreateOrderBody(
            Type: "qr",
            TotalAmount: amount,
            ExternalReference: request.ExternalReference,
            Description: request.Description,
            ExpirationTime: options.ExpirationTime,
            Config: new OrderConfig(new QrConfig(posId, "dynamic")),
            Transactions: new OrderTransactions([new PaymentAmount(amount)]));

        using var message = NewRequest(HttpMethod.Post, "v1/orders");
        message.Headers.Add("X-Idempotency-Key", idempotencyKey);
        message.Content = JsonContent.Create(body, options: Json);

        return await SendAsync(message, "create order", cancellationToken);
    }

    public async Task<MercadoPagoOrder> GetOrderAsync(string orderId, CancellationToken cancellationToken = default)
    {
        using var message = NewRequest(HttpMethod.Get, $"v1/orders/{Uri.EscapeDataString(orderId)}");
        return await SendAsync(message, $"get order {orderId}", cancellationToken);
    }

    public Task<MercadoPagoOrder> RefundOrderAsync(
        string orderId, string idempotencyKey, CancellationToken cancellationToken = default) =>
        PostOrderActionAsync(orderId, "refund", idempotencyKey, cancellationToken);

    public Task<MercadoPagoOrder> CancelOrderAsync(
        string orderId, string idempotencyKey, CancellationToken cancellationToken = default) =>
        PostOrderActionAsync(orderId, "cancel", idempotencyKey, cancellationToken);

    // POST /v1/orders/{id}/refund (no body = full refund) and /v1/orders/{id}/cancel; both answer with the order.
    private async Task<MercadoPagoOrder> PostOrderActionAsync(
        string orderId, string action, string idempotencyKey, CancellationToken cancellationToken)
    {
        using var message = NewRequest(HttpMethod.Post, $"v1/orders/{Uri.EscapeDataString(orderId)}/{action}");
        message.Headers.Add("X-Idempotency-Key", idempotencyKey);
        return await SendAsync(message, $"{action} order {orderId}", cancellationToken);
    }

    private HttpRequestMessage NewRequest(HttpMethod method, string path)
    {
        var token = options.AccessToken
            ?? throw new PaymentProviderException("Mercado Pago is not configured: set MERCADOPAGO_ACCESS_TOKEN.");

        var message = new HttpRequestMessage(method, new Uri(options.BaseAddress, path));
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return message;
    }

    private async Task<MercadoPagoOrder> SendAsync(
        HttpRequestMessage message, string operation, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(message, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            throw new PaymentProviderException($"Mercado Pago unreachable ({operation}).", exception);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new PaymentProviderException(
                    $"Mercado Pago {operation} failed with {(int)response.StatusCode}: {Truncate(error)}");
            }

            var rawJson = await response.Content.ReadAsStringAsync(cancellationToken);
            OrderBody? order;
            try
            {
                order = JsonSerializer.Deserialize<OrderBody>(rawJson, Json);
            }
            catch (JsonException exception)
            {
                throw new PaymentProviderException($"Mercado Pago {operation} returned an unreadable body.", exception);
            }

            if (order?.Id is null || order.Status is null)
                throw new PaymentProviderException($"Mercado Pago {operation} returned no order id/status.");

            return new MercadoPagoOrder(order.Id, order.Status, order.StatusDetail, order.TypeResponse?.QrData, rawJson);
        }
    }

    private static string Truncate(string value) => value.Length <= 300 ? value : value[..300] + "…";

    private sealed record CreateOrderBody(
        string Type,
        string TotalAmount,
        string ExternalReference,
        string Description,
        string ExpirationTime,
        OrderConfig Config,
        OrderTransactions Transactions);

    private sealed record OrderConfig(QrConfig Qr);

    private sealed record QrConfig(string ExternalPosId, string Mode);

    private sealed record OrderTransactions(IReadOnlyList<PaymentAmount> Payments);

    private sealed record PaymentAmount(string Amount);

    private sealed record OrderBody(string? Id, string? Status, string? StatusDetail, TypeResponse? TypeResponse);

    private sealed record TypeResponse(string? QrData);
}
