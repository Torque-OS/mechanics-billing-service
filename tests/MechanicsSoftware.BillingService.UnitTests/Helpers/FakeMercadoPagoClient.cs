using System.Collections.Concurrent;
using MechanicsSoftware.BillingService.Application.Abstractions;
using MechanicsSoftware.BillingService.Application.Exceptions;

namespace MechanicsSoftware.BillingService.UnitTests.Helpers;

/// <summary>
/// In-memory Mercado Pago: creates orders with status <c>created</c> and lets a test move them to any
/// status, as the real provider would after the buyer pays or the QR expires. Refund and cancel follow the
/// provider's rules (refund only <c>processed</c>, cancel only <c>created</c>/<c>action_required</c>).
/// </summary>
public sealed class FakeMercadoPagoClient : IMercadoPagoClient
{
    private readonly ConcurrentDictionary<string, MercadoPagoOrder> _orders = new();

    public List<(MercadoPagoQrOrderRequest Request, string IdempotencyKey)> CreateCalls { get; } = [];
    public int GetCalls { get; private set; }
    public bool FailNextCreate { get; set; }
    public List<(string OrderId, string IdempotencyKey)> RefundCalls { get; } = [];
    public List<(string OrderId, string IdempotencyKey)> CancelCalls { get; } = [];
    public bool FailNextRefund { get; set; }
    public bool FailNextCancel { get; set; }

    public Task<MercadoPagoOrder> CreateQrOrderAsync(
        MercadoPagoQrOrderRequest request, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        if (FailNextCreate)
        {
            FailNextCreate = false;
            throw new PaymentProviderException("Mercado Pago rejected the order (fake).");
        }

        lock (CreateCalls) CreateCalls.Add((request, idempotencyKey));
        var id = "ORD" + Guid.NewGuid().ToString("N")[..26].ToUpperInvariant();
        var order = new MercadoPagoOrder(id, "created", "created", $"00020101021243650016com.mercadolibre-{id}", Raw(id, "created"));
        _orders[id] = order;
        return Task.FromResult(order);
    }

    public Task<MercadoPagoOrder> GetOrderAsync(string orderId, CancellationToken cancellationToken = default)
    {
        GetCalls++;
        return _orders.TryGetValue(orderId.ToUpperInvariant(), out var order)
            ? Task.FromResult(order)
            : throw new PaymentProviderException($"Order '{orderId}' not found (fake).");
    }

    public Task<MercadoPagoOrder> RefundOrderAsync(
        string orderId, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        if (FailNextRefund)
        {
            FailNextRefund = false;
            throw new PaymentProviderException("Mercado Pago refused the refund (fake).");
        }

        lock (RefundCalls) RefundCalls.Add((orderId, idempotencyKey));
        return Move(orderId, from: ["processed"], to: "refunded");
    }

    public Task<MercadoPagoOrder> CancelOrderAsync(
        string orderId, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        if (FailNextCancel)
        {
            FailNextCancel = false;
            throw new PaymentProviderException("Mercado Pago refused the cancellation (fake).");
        }

        lock (CancelCalls) CancelCalls.Add((orderId, idempotencyKey));
        return Move(orderId, from: ["created", "action_required"], to: "canceled");
    }

    public string StatusOf(string orderId) => _orders[orderId].Status;

    private Task<MercadoPagoOrder> Move(string orderId, string[] from, string to)
    {
        var order = _orders[orderId];
        if (!from.Contains(order.Status))
            throw new PaymentProviderException($"Order '{orderId}' is {order.Status}; cannot move to {to} (fake).");

        SetStatus(orderId, to);
        return Task.FromResult(_orders[orderId]);
    }

    public void SetStatus(string orderId, string status, string? statusDetail = null) =>
        _orders[orderId] = _orders[orderId] with { Status = status, StatusDetail = statusDetail ?? status, RawJson = Raw(orderId, status) };

    private static string Raw(string id, string status) => $"{{\"id\":\"{id}\",\"status\":\"{status}\"}}";
}
