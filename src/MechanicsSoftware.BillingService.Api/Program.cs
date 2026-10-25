using System.Diagnostics.CodeAnalysis;
using MechanicsSoftware.BillingService.Api.Extensions;
using MechanicsSoftware.BillingService.Api.Middleware;
using MechanicsSoftware.BillingService.Application;
using MechanicsSoftware.BillingService.Infrastructure;
using MechanicsSoftware.BillingService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

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
