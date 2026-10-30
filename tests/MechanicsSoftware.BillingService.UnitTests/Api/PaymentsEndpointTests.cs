using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using MechanicsSoftware.BillingService.Application.UseCases.Budgets;
using MechanicsSoftware.BillingService.Application.UseCases.Payments;
using MechanicsSoftware.BillingService.Infrastructure.MercadoPago;
using MechanicsSoftware.BillingService.UnitTests.Helpers;

namespace MechanicsSoftware.BillingService.UnitTests.Api;

public class PaymentsEndpointTests(BillingApiFactory factory) : IClassFixture<BillingApiFactory>
{
    private HttpClient ClientAs(string? token)
    {
        var client = factory.CreateClient();
        if (token is not null)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>Generates a budget through the API and, unless told otherwise, approves it as its customer.</summary>
    private async Task<BudgetResponse> BudgetAsync(Guid customerId, bool approve = true)
    {
        var snapshot = new
        {
            serviceOrderId = Guid.NewGuid(),
            customerId,
            items = new[] { new { type = "Service", referenceId = Guid.NewGuid(), description = "Oil change", unitPriceInCents = 15000, quantity = 1 } },
        };
        var budget = (await (await ClientAs(TestTokens.Staff()).PostAsJsonAsync("/api/budgets", snapshot))
            .Content.ReadFromJsonAsync<BudgetResponse>())!;

        if (approve)
            await ClientAs(TestTokens.Customer(customerId))
                .PostAsJsonAsync($"/api/budgets/{budget.Id}/decision", new { decision = "approve" });

        return budget;
    }

    private async Task<PaymentResponse> PaymentAsync(Guid? customerId = null)
    {
        var budget = await BudgetAsync(customerId ?? Guid.NewGuid());
        var response = await ClientAs(TestTokens.Staff()).PostAsJsonAsync("/api/payments", new { budgetId = budget.Id });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<PaymentResponse>())!;
    }

    private Task<HttpResponseMessage> WebhookAsync(string orderId, string action, string? secret = BillingApiFactory.WebhookSecret)
    {
        const string requestId = "req-0001";
        var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
        var v1 = MercadoPagoSignatureValidator.ComputeSignature(
            secret ?? "wrong", MercadoPagoSignatureValidator.BuildManifest(orderId, requestId, ts));

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/payments/webhooks/mercadopago?data.id={orderId}&type=order")
        {
            Content = new StringContent(
                $$$"""{"action":"{{{action}}}","type":"order","live_mode":false,"data":{"id":"{{{orderId}}}"}}""",
                Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("x-signature", $"ts={ts},v1={v1}");
        request.Headers.Add("x-request-id", requestId);
        return factory.CreateClient().SendAsync(request);
    }

    private async Task<string> StatusOf(Guid paymentId) =>
        (await ClientAs(TestTokens.Staff()).GetFromJsonAsync<PaymentResponse>($"/api/payments/{paymentId}"))!.Status;

    // --- Create -------------------------------------------------------------------------

    [Fact]
    public async Task Create_ApprovedBudget_Returns201PendingWithQrData()
    {
        var budget = await BudgetAsync(Guid.NewGuid());

        var response = await ClientAs(TestTokens.Staff()).PostAsJsonAsync("/api/payments", new { budgetId = budget.Id });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var payment = await response.Content.ReadFromJsonAsync<PaymentResponse>();
        payment!.Status.Should().Be("PENDING");
        payment.AmountInCents.Should().Be(15000);
        payment.QrData.Should().NotBeNullOrWhiteSpace();
        response.Headers.Location!.ToString().Should().EndWith($"/api/payments/{payment.Id}");
    }

    [Fact]
    public async Task Create_Twice_Returns200WithSamePayment()
    {
        var budget = await BudgetAsync(Guid.NewGuid());
        var client = ClientAs(TestTokens.Staff());
        var first = await (await client.PostAsJsonAsync("/api/payments", new { budgetId = budget.Id }))
            .Content.ReadFromJsonAsync<PaymentResponse>();

        var second = await client.PostAsJsonAsync("/api/payments", new { budgetId = budget.Id });

        second.StatusCode.Should().Be(HttpStatusCode.OK);
        (await second.Content.ReadFromJsonAsync<PaymentResponse>())!.Id.Should().Be(first!.Id);
    }

    [Fact]
    public async Task Create_CustomerPaysOwnBudget_Returns201()
    {
        var customerId = Guid.NewGuid();
        var budget = await BudgetAsync(customerId);

        var response = await ClientAs(TestTokens.Customer(customerId)).PostAsJsonAsync("/api/payments", new { budgetId = budget.Id });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Create_CustomerPaysAnotherCustomersBudget_Returns403()
    {
        var budget = await BudgetAsync(Guid.NewGuid());

        var response = await ClientAs(TestTokens.Customer(Guid.NewGuid())).PostAsJsonAsync("/api/payments", new { budgetId = budget.Id });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_BudgetNotApproved_Returns409()
    {
        var budget = await BudgetAsync(Guid.NewGuid(), approve: false);

        var response = await ClientAs(TestTokens.Staff()).PostAsJsonAsync("/api/payments", new { budgetId = budget.Id });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_UnknownBudget_Returns404()
    {
        var response = await ClientAs(TestTokens.Staff()).PostAsJsonAsync("/api/payments", new { budgetId = Guid.NewGuid() });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_WithoutToken_Returns401()
    {
        var response = await ClientAs(null).PostAsJsonAsync("/api/payments", new { budgetId = Guid.NewGuid() });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_ProviderFails_Returns502WithoutLeakingDetails()
    {
        var budget = await BudgetAsync(Guid.NewGuid());
        factory.MercadoPago.FailNextCreate = true;

        var response = await ClientAs(TestTokens.Staff()).PostAsJsonAsync("/api/payments", new { budgetId = budget.Id });

        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Payment provider unavailable").And.NotContain("fake");
    }

    // --- Query / QR ---------------------------------------------------------------------

    [Fact]
    public async Task Get_AsCustomer_Returns403()
    {
        var customerId = Guid.NewGuid();
        var payment = await PaymentAsync(customerId);

        var response = await ClientAs(TestTokens.Customer(customerId)).GetAsync($"/api/payments/{payment.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task QrPng_WithoutToken_ReturnsPngImage()
    {
        var payment = await PaymentAsync();

        var response = await ClientAs(null).GetAsync($"/api/payments/{payment.Id}/qr.png");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("image/png");
        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes.Take(8).Should().Equal(0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A); // PNG signature
    }

    [Fact]
    public async Task QrPng_UnknownPayment_Returns404()
    {
        (await ClientAs(null).GetAsync($"/api/payments/{Guid.NewGuid()}/qr.png")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task QrPng_AfterConfirmation_Returns409()
    {
        var payment = await PaymentAsync();
        factory.MercadoPago.SetStatus(payment.ProviderOrderId!, "processed");
        await WebhookAsync(payment.ProviderOrderId!, "order.processed");

        var response = await ClientAs(null).GetAsync($"/api/payments/{payment.Id}/qr.png");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // --- Webhook ------------------------------------------------------------------------

    [Fact]
    public async Task Webhook_ValidSignatureProcessed_ConfirmsAndPublishesOnceEvenIfRepeated()
    {
        var payment = await PaymentAsync();
        factory.MercadoPago.SetStatus(payment.ProviderOrderId!, "processed");

        var first = await WebhookAsync(payment.ProviderOrderId!, "order.processed");
        var repeated = await WebhookAsync(payment.ProviderOrderId!, "order.processed");

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        repeated.StatusCode.Should().Be(HttpStatusCode.OK);
        (await StatusOf(payment.Id)).Should().Be("CONFIRMED");
        factory.PaymentEvents.For(payment.ServiceOrderId).Should().Equal("PaymentConfirmed");
        factory.Notifications.Saved.Count(n => n.DataId == payment.ProviderOrderId).Should().Be(2);
    }

    [Fact]
    public async Task Webhook_InvalidSignature_Returns401AndChangesNothing()
    {
        var payment = await PaymentAsync();
        factory.MercadoPago.SetStatus(payment.ProviderOrderId!, "processed");

        var response = await WebhookAsync(payment.ProviderOrderId!, "order.processed", secret: "attacker-secret");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await StatusOf(payment.Id)).Should().Be("PENDING");
        factory.Notifications.Saved.Should().NotContain(n => n.DataId == payment.ProviderOrderId);
    }

    [Fact]
    public async Task Webhook_WithoutSignatureHeader_Returns401()
    {
        var response = await factory.CreateClient().PostAsync(
            "/api/payments/webhooks/mercadopago?data.id=ORD1",
            new StringContent("{}", Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Webhook_Expired_FailsPaymentAndPublishesPaymentFailed()
    {
        var payment = await PaymentAsync();
        factory.MercadoPago.SetStatus(payment.ProviderOrderId!, "expired");

        await WebhookAsync(payment.ProviderOrderId!, "order.expired");

        (await StatusOf(payment.Id)).Should().Be("FAILED");
        factory.PaymentEvents.For(payment.ServiceOrderId).Should().Equal("PaymentFailed");
    }

    [Fact]
    public async Task Webhook_UnknownOrder_Returns200Ignored()
    {
        var response = await WebhookAsync("ORDNOTOURS", "order.processed");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Ignored");
    }

    // --- Verify -------------------------------------------------------------------------

    [Fact]
    public async Task Verify_ReflectsProviderStatus()
    {
        var payment = await PaymentAsync();
        var staff = ClientAs(TestTokens.Staff());

        var stillPending = await (await staff.PostAsync($"/api/payments/{payment.Id}/verify", null))
            .Content.ReadFromJsonAsync<PaymentResponse>();
        factory.MercadoPago.SetStatus(payment.ProviderOrderId!, "processed");
        var confirmed = await (await staff.PostAsync($"/api/payments/{payment.Id}/verify", null))
            .Content.ReadFromJsonAsync<PaymentResponse>();

        stillPending!.Status.Should().Be("PENDING");
        confirmed!.Status.Should().Be("CONFIRMED");
        factory.PaymentEvents.For(payment.ServiceOrderId).Should().Equal("PaymentConfirmed");
    }

    [Fact]
    public async Task Verify_AsCustomer_Returns403()
    {
        var customerId = Guid.NewGuid();
        var payment = await PaymentAsync(customerId);

        var response = await ClientAs(TestTokens.Customer(customerId)).PostAsync($"/api/payments/{payment.Id}/verify", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // --- Cancel budget (Saga compensation, F4-17) -----------------------------------------

    private Task<HttpResponseMessage> CancelAsync(string token, Guid budgetId) =>
        ClientAs(token).PostAsync($"/api/budgets/{budgetId}/cancel", null);

    [Fact]
    public async Task CancelBudget_PaidOrder_RefundsAndReturns200Cancelled()
    {
        var payment = await PaymentAsync();
        factory.MercadoPago.SetStatus(payment.ProviderOrderId!, "processed");
        await ClientAs(TestTokens.Staff()).PostAsync($"/api/payments/{payment.Id}/verify", null);

        var response = await CancelAsync(TestTokens.Staff(), payment.BudgetId);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<BudgetResponse>())!.Status.Should().Be("CANCELLED");
        (await StatusOf(payment.Id)).Should().Be("REFUNDED");
        factory.Events.For(payment.ServiceOrderId).Select(e => e.Name).Should().EndWith("BudgetCancelled");
    }

    [Fact]
    public async Task CancelBudget_OpenQr_CancelsPaymentWithoutRefund()
    {
        var payment = await PaymentAsync();

        var response = await CancelAsync(TestTokens.Staff(), payment.BudgetId);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await StatusOf(payment.Id)).Should().Be("CANCELLED");
        factory.MercadoPago.RefundCalls.Should().NotContain(call => call.OrderId == payment.ProviderOrderId);
    }

    [Fact]
    public async Task CancelBudget_AsCustomer_Returns403()
    {
        var customerId = Guid.NewGuid();
        var budget = await BudgetAsync(customerId);

        var response = await CancelAsync(TestTokens.Customer(customerId), budget.Id);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CancelBudget_RefundFails_Returns502AndKeepsPaymentConfirmed()
    {
        var payment = await PaymentAsync();
        factory.MercadoPago.SetStatus(payment.ProviderOrderId!, "processed");
        await ClientAs(TestTokens.Staff()).PostAsync($"/api/payments/{payment.Id}/verify", null);
        factory.MercadoPago.FailNextRefund = true;

        var response = await CancelAsync(TestTokens.Staff(), payment.BudgetId);

        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        (await StatusOf(payment.Id)).Should().Be("CONFIRMED");
    }

    [Fact]
    public async Task CancelBudget_RejectedBudget_Returns409()
    {
        var customerId = Guid.NewGuid();
        var budget = await BudgetAsync(customerId, approve: false);
        await ClientAs(TestTokens.Customer(customerId))
            .PostAsJsonAsync($"/api/budgets/{budget.Id}/decision", new { decision = "reject" });

        var response = await CancelAsync(TestTokens.Staff(), budget.Id);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CancelBudget_UnknownBudget_Returns404()
    {
        var response = await CancelAsync(TestTokens.Staff(), Guid.NewGuid());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
