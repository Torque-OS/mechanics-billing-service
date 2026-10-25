using System.Net;
using FluentAssertions;
using MechanicsSoftware.BillingService.UnitTests.Helpers;

namespace MechanicsSoftware.BillingService.UnitTests;

public class HealthEndpointTests
{
    [Fact]
    public async Task Health_WithoutToken_ReturnsOk()
    {
        await using var factory = new BillingApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
