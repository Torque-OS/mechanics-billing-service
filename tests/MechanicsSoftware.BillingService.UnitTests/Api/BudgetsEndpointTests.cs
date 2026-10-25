using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using MechanicsSoftware.BillingService.Application.UseCases.Budgets;
using MechanicsSoftware.BillingService.UnitTests.Helpers;

namespace MechanicsSoftware.BillingService.UnitTests.Api;

public class BudgetsEndpointTests(BillingApiFactory factory) : IClassFixture<BillingApiFactory>
{
    private HttpClient ClientAs(string? token)
    {
        var client = factory.CreateClient();
        if (token is not null)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static object Snapshot(Guid serviceOrderId, Guid customerId, bool withService = true) => new
    {
        serviceOrderId,
        customerId,
        items = withService
            ? new object[]
            {
                new { type = "Service", referenceId = Guid.NewGuid(), description = "Oil change", unitPriceInCents = 15000, quantity = 1 },
                new { type = "Part", referenceId = Guid.NewGuid(), description = "Oil filter", unitPriceInCents = 4500, quantity = 2 },
            }
            : [new { type = "Part", referenceId = Guid.NewGuid(), description = "Oil filter", unitPriceInCents = 4500, quantity = 2 }],
    };

    private async Task<BudgetResponse> GenerateAsync(Guid customerId)
    {
        var response = await ClientAs(TestTokens.Staff())
            .PostAsJsonAsync("/api/budgets", Snapshot(Guid.NewGuid(), customerId));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<BudgetResponse>())!;
    }

    private Task<HttpResponseMessage> DecideAsync(string token, Guid budgetId, string decision) =>
        ClientAs(token).PostAsJsonAsync($"/api/budgets/{budgetId}/decision", new { decision });

    // --- Generate -----------------------------------------------------------------------

    [Fact]
    public async Task Generate_AsStaff_Returns201WithPendingBudgetAndPublishesBudgetGenerated()
    {
        var serviceOrderId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        var response = await ClientAs(TestTokens.Staff())
            .PostAsJsonAsync("/api/budgets", Snapshot(serviceOrderId, customerId));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var budget = await response.Content.ReadFromJsonAsync<BudgetResponse>();
        budget!.Status.Should().Be("PENDING");
        budget.TotalInCents.Should().Be(24000);
        budget.CustomerId.Should().Be(customerId);
        response.Headers.Location!.ToString().Should().EndWith($"/api/budgets/{budget.Id}");
        factory.Events.For(serviceOrderId).Select(e => e.Name).Should().Equal("BudgetGenerated");
    }

    [Fact]
    public async Task Generate_SameServiceOrderTwice_Returns200WithSameBudget()
    {
        var serviceOrderId = Guid.NewGuid();
        var client = ClientAs(TestTokens.Staff());

        var first = await (await client.PostAsJsonAsync("/api/budgets", Snapshot(serviceOrderId, Guid.NewGuid())))
            .Content.ReadFromJsonAsync<BudgetResponse>();
        var second = await client.PostAsJsonAsync("/api/budgets", Snapshot(serviceOrderId, Guid.NewGuid()));

        second.StatusCode.Should().Be(HttpStatusCode.OK);
        (await second.Content.ReadFromJsonAsync<BudgetResponse>())!.Id.Should().Be(first!.Id);
        factory.Events.For(serviceOrderId).Should().ContainSingle();
    }

    [Fact]
    public async Task Generate_WithoutServiceItem_Returns422AndPublishesBudgetFailed()
    {
        var serviceOrderId = Guid.NewGuid();

        var response = await ClientAs(TestTokens.Staff())
            .PostAsJsonAsync("/api/budgets", Snapshot(serviceOrderId, Guid.NewGuid(), withService: false));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await response.Content.ReadAsStringAsync()).Should().Contain("at least one service item");
        factory.Events.For(serviceOrderId).Select(e => e.Name).Should().Equal("BudgetFailed");
    }

    [Fact]
    public async Task Generate_AsCustomer_Returns403()
    {
        var response = await ClientAs(TestTokens.Customer(Guid.NewGuid()))
            .PostAsJsonAsync("/api/budgets", Snapshot(Guid.NewGuid(), Guid.NewGuid()));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Generate_WithoutToken_Returns401()
    {
        var response = await ClientAs(null).PostAsJsonAsync("/api/budgets", Snapshot(Guid.NewGuid(), Guid.NewGuid()));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Generate_TokenSignedWithAnotherSecret_Returns401()
    {
        var foreign = TestTokens.Create("ADMIN", Guid.NewGuid(), secret: "another-secret-that-is-at-least-32-chars");

        var response = await ClientAs(foreign).PostAsJsonAsync("/api/budgets", Snapshot(Guid.NewGuid(), Guid.NewGuid()));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Generate_ExpiredToken_Returns401()
    {
        var expired = TestTokens.Create("ADMIN", Guid.NewGuid(), expires: DateTime.UtcNow.AddMinutes(-1));

        var response = await ClientAs(expired).PostAsJsonAsync("/api/budgets", Snapshot(Guid.NewGuid(), Guid.NewGuid()));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // --- Query --------------------------------------------------------------------------

    [Fact]
    public async Task Get_ExistingAndUnknownId_Returns200And404()
    {
        var budget = await GenerateAsync(Guid.NewGuid());
        var client = ClientAs(TestTokens.Staff());

        (await client.GetAsync($"/api/budgets/{budget.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync($"/api/budgets/{Guid.NewGuid()}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetByServiceOrder_ExistingUnknownAndMissing_Returns200404And400()
    {
        var budget = await GenerateAsync(Guid.NewGuid());
        var client = ClientAs(TestTokens.Staff());

        var found = await client.GetAsync($"/api/budgets?serviceOrderId={budget.ServiceOrderId}");
        found.StatusCode.Should().Be(HttpStatusCode.OK);
        (await found.Content.ReadFromJsonAsync<BudgetResponse>())!.Id.Should().Be(budget.Id);
        (await client.GetAsync($"/api/budgets?serviceOrderId={Guid.NewGuid()}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.GetAsync("/api/budgets")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Get_AsCustomer_Returns403()
    {
        var customerId = Guid.NewGuid();
        var budget = await GenerateAsync(customerId);

        var response = await ClientAs(TestTokens.Customer(customerId)).GetAsync($"/api/budgets/{budget.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // --- Decision -----------------------------------------------------------------------

    [Fact]
    public async Task Decide_CustomerApprovesOwnBudget_Returns200AndPublishesBudgetApproved()
    {
        var customerId = Guid.NewGuid();
        var budget = await GenerateAsync(customerId);

        var response = await DecideAsync(TestTokens.Customer(customerId), budget.Id, "approve");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var decided = await response.Content.ReadFromJsonAsync<BudgetResponse>();
        decided!.Status.Should().Be("APPROVED");
        decided.DecidedAt.Should().NotBeNull();
        factory.Events.For(budget.ServiceOrderId).Select(e => e.Name).Should().Equal("BudgetGenerated", "BudgetApproved");
    }

    [Fact]
    public async Task Decide_StaffRejectsAnyBudget_Returns200AndPublishesBudgetRejected()
    {
        var budget = await GenerateAsync(Guid.NewGuid());

        var response = await DecideAsync(TestTokens.Staff(), budget.Id, "Reject");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<BudgetResponse>())!.Status.Should().Be("REJECTED");
        factory.Events.For(budget.ServiceOrderId).Select(e => e.Name).Should().Equal("BudgetGenerated", "BudgetRejected");
    }

    [Fact]
    public async Task Decide_SecondDecision_Returns409()
    {
        var customerId = Guid.NewGuid();
        var budget = await GenerateAsync(customerId);
        await DecideAsync(TestTokens.Customer(customerId), budget.Id, "approve");

        var response = await DecideAsync(TestTokens.Customer(customerId), budget.Id, "reject");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Decide_CustomerDecidesAnotherCustomersBudget_Returns403WithoutDeciding()
    {
        var budget = await GenerateAsync(Guid.NewGuid());

        var response = await DecideAsync(TestTokens.Customer(Guid.NewGuid()), budget.Id, "approve");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        factory.Events.For(budget.ServiceOrderId).Select(e => e.Name).Should().Equal("BudgetGenerated");
    }

    [Fact]
    public async Task Decide_UnknownBudget_Returns404()
    {
        var response = await DecideAsync(TestTokens.Staff(), Guid.NewGuid(), "approve");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Decide_InvalidDecisionValue_Returns400()
    {
        var budget = await GenerateAsync(Guid.NewGuid());

        var response = await DecideAsync(TestTokens.Staff(), budget.Id, "maybe");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Decide_WithoutToken_Returns401()
    {
        var response = await DecideAsync(null!, Guid.NewGuid(), "approve");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
