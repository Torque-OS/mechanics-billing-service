using System.Diagnostics.CodeAnalysis;
using MassTransit;
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

        var rabbitMqConnectionString = configuration["RABBITMQ_CONNECTION_STRING"];
        if (string.IsNullOrWhiteSpace(rabbitMqConnectionString))
            throw new InvalidOperationException(
                "RabbitMQ not configured. Set 'RABBITMQ_CONNECTION_STRING' (amqp://user:password@host:5672/).");

        var jwt = JwtSettings.FromConfiguration(configuration);

        services.AddDbContext<BillingDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IBillingDbContext>(sp => sp.GetRequiredService<BillingDbContext>());

        // Saga messaging (ADR-011): MassTransit v8 over RabbitMQ with the EF Core bus outbox, so a published
        // event is committed with the state change and delivered after SaveChanges. Contracts: TorqueOS.Contracts.Billing.
        services.AddMassTransit(bus =>
        {
            bus.AddConsumer<CancelBudgetConsumer, CancelBudgetConsumerDefinition>();

            bus.AddEntityFrameworkOutbox<BillingDbContext>(outbox =>
            {
                outbox.UsePostgres();
                outbox.UseBusOutbox();
            });

            bus.UsingRabbitMq((context, rabbit) =>
            {
                rabbit.Host(new Uri(rabbitMqConnectionString));
                rabbit.ConfigureEndpoints(context);
            });
        });
        services.AddScoped<IBudgetEventPublisher, MassTransitBudgetEventPublisher>();
        services.AddScoped<IPaymentEventPublisher, MassTransitPaymentEventPublisher>();

        // MongoDB (ADR-012): raw provider notifications and the payment history. The client connects lazily.
        var mongoUrl = MongoUrl.Create(mongoConnectionString);
        services.AddSingleton<IMongoClient>(_ => new MongoClient(mongoUrl));
        services.AddSingleton(sp => sp.GetRequiredService<IMongoClient>().GetDatabase(mongoUrl.DatabaseName ?? "billing"));
        services.AddScoped<INotificationStore, MongoNotificationStore>();
        services.AddScoped<IPaymentHistory, MongoPaymentHistory>();

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
