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
/// Hosts the real API pipeline (auth, policies, middleware, controllers, webhook signature validation)
/// over EF InMemory, an in-memory notification store, a fake Mercado Pago and recording publishers —
/// no PostgreSQL, MongoDB, broker or network needed.
/// </summary>
public sealed class BillingApiFactory : WebApplicationFactory<Program>
{
    public const string WebhookSecret = "test-webhook-secret";

    private readonly string _databaseName = Guid.NewGuid().ToString();

    public RecordingBudgetEventPublisher Events { get; } = new();
    public RecordingPaymentEventPublisher PaymentEvents { get; } = new();
    public FakeMercadoPagoClient MercadoPago { get; } = new();
    public InMemoryNotificationStore Notifications { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:DefaultConnection", "Host=unused-in-tests");
        builder.UseSetting("ConnectionStrings:Mongo", "mongodb://unused-in-tests:27017/billing");
        builder.UseSetting("JWT_SECRET", TestTokens.Secret);
        builder.UseSetting("MERCADOPAGO_WEBHOOK_SECRET", WebhookSecret);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<BillingDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<BillingDbContext>>();
            services.AddDbContext<BillingDbContext>(options => options.UseInMemoryDatabase(_databaseName));

            services.RemoveAll<IBudgetEventPublisher>();
            services.AddSingleton<IBudgetEventPublisher>(Events);

            services.RemoveAll<IPaymentEventPublisher>();
            services.AddSingleton<IPaymentEventPublisher>(PaymentEvents);

            services.RemoveAll<IMercadoPagoClient>();
            services.AddSingleton<IMercadoPagoClient>(MercadoPago);

            services.RemoveAll<INotificationStore>();
            services.AddSingleton<INotificationStore>(Notifications);
        });
    }
}
