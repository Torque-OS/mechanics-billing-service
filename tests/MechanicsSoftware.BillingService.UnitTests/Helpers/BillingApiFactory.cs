using MechanicsSoftware.BillingService.Application.Abstractions;
using MechanicsSoftware.BillingService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MechanicsSoftware.BillingService.UnitTests.Helpers;

/// <summary>
/// Hosts the real API pipeline (auth, policies, middleware, controllers) over the EF InMemory
/// provider and a <see cref="RecordingBudgetEventPublisher"/>, so no PostgreSQL or broker is needed.
/// </summary>
public sealed class BillingApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = Guid.NewGuid().ToString();

    public RecordingBudgetEventPublisher Events { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:DefaultConnection", "Host=unused-in-tests");
        builder.UseSetting("JWT_SECRET", TestTokens.Secret);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<BillingDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<BillingDbContext>>();
            services.AddDbContext<BillingDbContext>(options => options.UseInMemoryDatabase(_databaseName));

            services.RemoveAll<IBudgetEventPublisher>();
            services.AddSingleton<IBudgetEventPublisher>(Events);
        });
    }
}
