using System.Text.Json;
using FluentAssertions;
using MechanicsSoftware.BillingService.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace MechanicsSoftware.BillingService.UnitTests.Api;

public class ExceptionHandlingMiddlewareTests
{
    [Fact]
    public async Task ConcurrentUpdate_Returns409WithRetryMessage()
    {
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new DbUpdateConcurrencyException("row changed"),
            NullLogger<ExceptionHandlingMiddleware>.Instance);
        var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        context.Response.Body.Position = 0;
        using var body = await JsonDocument.ParseAsync(context.Response.Body);
        body.RootElement.GetProperty("error").GetString().Should().Contain("changed by another request");
    }
}
