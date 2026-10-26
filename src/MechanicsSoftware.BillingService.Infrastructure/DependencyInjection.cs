using System.Diagnostics.CodeAnalysis;
using MechanicsSoftware.BillingService.Application.Abstractions;
using MechanicsSoftware.BillingService.Infrastructure.MercadoPago;
using MechanicsSoftware.BillingService.Infrastructure.Messaging;
using MechanicsSoftware.BillingService.Infrastructure.Persistence;
using MechanicsSoftware.BillingService.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace MechanicsSoftware.BillingService.Infrastructure;

[ExcludeFromCodeCoverage]
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                "Connection string not found. Set 'ConnectionStrings:DefaultConnection'.");

        var mongoConnectionString = configuration.GetConnectionString("Mongo");
        if (string.IsNullOrWhiteSpace(mongoConnectionString))
            throw new InvalidOperationException(
                "Connection string not found. Set 'ConnectionStrings:Mongo'.");

        var jwt = JwtSettings.FromConfiguration(configuration);

        services.AddDbContext<BillingDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IBillingDbContext>(sp => sp.GetRequiredService<BillingDbContext>());
        services.AddScoped<IBudgetEventPublisher, LoggingBudgetEventPublisher>();
        services.AddScoped<IPaymentEventPublisher, LoggingPaymentEventPublisher>();

        // MongoDB: raw provider notifications (ADR-012). The client connects lazily, on first use.
        var mongoUrl = MongoUrl.Create(mongoConnectionString);
        services.AddSingleton<IMongoClient>(_ => new MongoClient(mongoUrl));
        services.AddSingleton(sp => sp.GetRequiredService<IMongoClient>().GetDatabase(mongoUrl.DatabaseName ?? "billing"));
        services.AddScoped<INotificationStore, MongoNotificationStore>();

        // Mercado Pago (Orders API, dynamic QR).
        services.AddSingleton(MercadoPagoOptions.FromConfiguration(configuration));
        services.AddSingleton<IWebhookSignatureValidator, MercadoPagoSignatureValidator>();
        services.AddHttpClient<IMercadoPagoClient, MercadoPagoClient>(client => client.Timeout = TimeSpan.FromSeconds(15));

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = jwt.ToValidationParameters();
            });

        services.AddPlatformAuthorization();

        return services;
    }
}
