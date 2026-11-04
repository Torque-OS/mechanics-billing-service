using System.Diagnostics.CodeAnalysis;
using MechanicsSoftware.BillingService.Api.Extensions;
using MechanicsSoftware.BillingService.Api.Middleware;
using MechanicsSoftware.BillingService.Application;
using MechanicsSoftware.BillingService.Infrastructure;
using MechanicsSoftware.BillingService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// Distributed tracing (ADR-011, F4-36): HTTP spans plus MassTransit's own ActivitySource for the
// Saga messages, so a ServiceOrderId's trace continues across the OS Service -> Billing Service
// hop (W3C trace context travels in both the HTTP Authorization-bearing requests and the
// RabbitMQ message headers). Same setup as mechanics-software's Program.cs.
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(
            serviceName: builder.Configuration["DD_SERVICE"] ?? builder.Environment.ApplicationName,
            serviceVersion: builder.Configuration["DD_VERSION"] ?? "unknown"))
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation(options =>
            {
                options.RecordException = true;
                options.Filter = httpContext =>
                    !httpContext.Request.Path.StartsWithSegments("/health");
            })
            .AddHttpClientInstrumentation(options =>
            {
                options.RecordException = true;
            })
            .AddSource("MassTransit");

        // No options callback: letting AddOtlpExporter() read OTEL_EXPORTER_OTLP_ENDPOINT/
        // OTEL_EXPORTER_OTLP_PROTOCOL itself is what makes it append the per-signal path
        // (/v1/traces) to the endpoint. Setting options.Endpoint here ourselves would
        // overwrite that already-suffixed URI with the bare host, which the collector
        // 404s on — silently, since OTel's own export failures aren't logged by default.
        // (Verified against a real OTLP collector while implementing F4-36 — see the
        // companion fix in mechanics-software's Program.cs, which had the same bug.)
        var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            tracing.AddOtlpExporter();
        }
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerDocumentation();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers(options =>
{
    var policy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
    options.Filters.Add(new AuthorizeFilter(policy));
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Migrations run on startup, as in mechanics-software (ADR-006). Skipped for non-relational
// providers (the EF InMemory provider used by the endpoint tests).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
    if (db.Database.IsRelational())
        await db.Database.MigrateAsync();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

// Liveness/readiness probe for Kubernetes (see k8s/deployment.yaml).
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "billing-service" }))
    .AllowAnonymous();

app.MapControllers();

await app.RunAsync();

// Exposed for WebApplicationFactory-based endpoint tests.
[ExcludeFromCodeCoverage]
public partial class Program
{
    protected Program() { }
}
