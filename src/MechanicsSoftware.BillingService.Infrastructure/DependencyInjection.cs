using System.Diagnostics.CodeAnalysis;
using MechanicsSoftware.BillingService.Application.Abstractions;
using MechanicsSoftware.BillingService.Infrastructure.Messaging;
using MechanicsSoftware.BillingService.Infrastructure.Persistence;
using MechanicsSoftware.BillingService.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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

        var jwt = JwtSettings.FromConfiguration(configuration);

        services.AddDbContext<BillingDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IBillingDbContext>(sp => sp.GetRequiredService<BillingDbContext>());
        services.AddScoped<IBudgetEventPublisher, LoggingBudgetEventPublisher>();

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
