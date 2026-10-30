using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using MechanicsSoftware.BillingService.Application.Abstractions;
using MechanicsSoftware.BillingService.Application.Exceptions;
using MechanicsSoftware.BillingService.Domain.ValueObjects;
using MechanicsSoftware.BillingService.Infrastructure.MercadoPago;
using Microsoft.Extensions.Configuration;

namespace MechanicsSoftware.BillingService.UnitTests.Infrastructure;

public class MercadoPagoClientTests
{
    private const string OrderJson = """
        {"id":"ORD01ABC","status":"created","status_detail":"created",
         "type_response":{"qr_data":"00020101021243650016com.mercadolibre"}}
        """;

    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? RequestBody { get; private set; }
        public Exception? Throw { get; init; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (Throw is not null) throw Throw;
            Request = request;
            RequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }

    private static MercadoPagoOptions Options(string? token = "TEST-token", string? posId = "TORQUEOSPOS001") =>
        new(token, "secret", posId, MercadoPagoOptions.DefaultBaseAddress, "PT15M");

    private static readonly MercadoPagoQrOrderRequest Request =
        new("4f1c0b2e-0000-0000-0000-000000000001", new Money(24050), "Torque-OS service order 123");

    [Fact]
    public async Task CreateQrOrder_SendsDocumentedRequestAndParsesResponse()
    {
        var handler = new StubHandler(HttpStatusCode.Created, OrderJson);
        var client = new MercadoPagoClient(new HttpClient(handler), Options());

        var order = await client.CreateQrOrderAsync(Request, "idem-key-1");

        order.Should().Be(new MercadoPagoOrder("ORD01ABC", "created", "created", "00020101021243650016com.mercadolibre", OrderJson));
        handler.Request!.Method.Should().Be(HttpMethod.Post);
        handler.Request.RequestUri.Should().Be(new Uri("https://api.mercadopago.com/v1/orders"));
        handler.Request.Headers.Authorization!.ToString().Should().Be("Bearer TEST-token");
        handler.Request.Headers.GetValues("X-Idempotency-Key").Should().Equal("idem-key-1");

        using var body = JsonDocument.Parse(handler.RequestBody!);
        var root = body.RootElement;
        root.GetProperty("type").GetString().Should().Be("qr");
        root.GetProperty("total_amount").GetString().Should().Be("240.50");
        root.GetProperty("external_reference").GetString().Should().Be(Request.ExternalReference);
        root.GetProperty("description").GetString().Should().Be(Request.Description);
        root.GetProperty("expiration_time").GetString().Should().Be("PT15M");
        root.GetProperty("config").GetProperty("qr").GetProperty("external_pos_id").GetString().Should().Be("TORQUEOSPOS001");
        root.GetProperty("config").GetProperty("qr").GetProperty("mode").GetString().Should().Be("dynamic");
        root.GetProperty("transactions").GetProperty("payments")[0].GetProperty("amount").GetString().Should().Be("240.50");
    }

    [Fact]
    public async Task GetOrder_CallsOrderEndpoint()
    {
        var handler = new StubHandler(HttpStatusCode.OK, """{"id":"ORD01ABC","status":"processed","status_detail":"accredited"}""");
        var client = new MercadoPagoClient(new HttpClient(handler), Options());

        var order = await client.GetOrderAsync("ORD01ABC");

        order.Status.Should().Be("processed");
        order.QrData.Should().BeNull();
        handler.Request!.Method.Should().Be(HttpMethod.Get);
        handler.Request.RequestUri.Should().Be(new Uri("https://api.mercadopago.com/v1/orders/ORD01ABC"));
    }

    [Fact]
    public async Task ErrorStatus_ThrowsProviderExceptionWithStatusCode()
    {
        var client = new MercadoPagoClient(
            new HttpClient(new StubHandler(HttpStatusCode.BadRequest, """{"error":"invalid_external_pos_id"}""")), Options());

        var act = () => client.CreateQrOrderAsync(Request, "k");

        await act.Should().ThrowAsync<PaymentProviderException>().WithMessage("*400*invalid_external_pos_id*");
    }

    [Fact]
    public async Task UnreadableBody_ThrowsProviderException()
    {
        var client = new MercadoPagoClient(new HttpClient(new StubHandler(HttpStatusCode.OK, "not json")), Options());

        var act = () => client.GetOrderAsync("ORD01ABC");

        await act.Should().ThrowAsync<PaymentProviderException>().WithMessage("*unreadable*");
    }

    [Fact]
    public async Task BodyWithoutId_ThrowsProviderException()
    {
        var client = new MercadoPagoClient(new HttpClient(new StubHandler(HttpStatusCode.OK, "{}")), Options());

        var act = () => client.GetOrderAsync("ORD01ABC");

        await act.Should().ThrowAsync<PaymentProviderException>().WithMessage("*no order id*");
    }

    [Fact]
    public async Task NetworkFailure_ThrowsProviderException()
    {
        var handler = new StubHandler(HttpStatusCode.OK, "") { Throw = new HttpRequestException("dns") };
        var client = new MercadoPagoClient(new HttpClient(handler), Options());

        var act = () => client.GetOrderAsync("ORD01ABC");

        await act.Should().ThrowAsync<PaymentProviderException>().WithMessage("*unreachable*");
    }

    [Fact]
    public async Task MissingAccessToken_ThrowsWithoutCallingProvider()
    {
        var handler = new StubHandler(HttpStatusCode.OK, OrderJson);
        var client = new MercadoPagoClient(new HttpClient(handler), Options(token: null));

        var act = () => client.GetOrderAsync("ORD01ABC");

        await act.Should().ThrowAsync<PaymentProviderException>().WithMessage("*MERCADOPAGO_ACCESS_TOKEN*");
        handler.Request.Should().BeNull();
    }

    [Fact]
    public async Task MissingExternalPosId_ThrowsWithoutCallingProvider()
    {
        var handler = new StubHandler(HttpStatusCode.Created, OrderJson);
        var client = new MercadoPagoClient(new HttpClient(handler), Options(posId: null));

        var act = () => client.CreateQrOrderAsync(Request, "k");

        await act.Should().ThrowAsync<PaymentProviderException>().WithMessage("*MERCADOPAGO_EXTERNAL_POS_ID*");
        handler.Request.Should().BeNull();
    }

    [Fact]
    public void Options_FromConfiguration_DefaultsAndBlanks()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["MERCADOPAGO_ACCESS_TOKEN"] = "  " })
            .Build();

        var options = MercadoPagoOptions.FromConfiguration(configuration);

        options.AccessToken.Should().BeNull();
        options.BaseAddress.Should().Be(MercadoPagoOptions.DefaultBaseAddress);
        options.ExpirationTime.Should().Be("PT15M");
    }

    [Theory]
    [InlineData("refund")]
    [InlineData("cancel")]
    public async Task RefundAndCancel_PostWithoutBodyAndWithIdempotencyKey(string action)
    {
        var handler = new StubHandler(HttpStatusCode.OK, """{"id":"ORD01ABC","status":"refunded","status_detail":"refunded"}""");
        var client = new MercadoPagoClient(new HttpClient(handler), Options());

        var order = action == "refund"
            ? await client.RefundOrderAsync("ORD01ABC", "refund-key")
            : await client.CancelOrderAsync("ORD01ABC", "refund-key");

        order.Status.Should().Be("refunded");
        handler.Request!.Method.Should().Be(HttpMethod.Post);
        handler.Request.RequestUri.Should().Be(new Uri($"https://api.mercadopago.com/v1/orders/ORD01ABC/{action}"));
        handler.Request.Headers.GetValues("X-Idempotency-Key").Should().Equal("refund-key");
        handler.Request.Headers.Authorization!.ToString().Should().Be("Bearer TEST-token");
        handler.RequestBody.Should().BeNull();
    }

    [Fact]
    public async Task Refund_Rejected_ThrowsProviderExceptionNamingTheOperation()
    {
        var client = new MercadoPagoClient(
            new HttpClient(new StubHandler(HttpStatusCode.Conflict, """{"error":"order_not_refundable"}""")), Options());

        var act = () => client.RefundOrderAsync("ORD01ABC", "k");

        await act.Should().ThrowAsync<PaymentProviderException>().WithMessage("*refund order ORD01ABC*409*order_not_refundable*");
    }
}
