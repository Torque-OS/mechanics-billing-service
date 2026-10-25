using System.Diagnostics.CodeAnalysis;
using MechanicsSoftware.BillingService.Application.UseCases.Budgets.Handlers;
using Microsoft.Extensions.DependencyInjection;

namespace MechanicsSoftware.BillingService.Application;

[ExcludeFromCodeCoverage]
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Budgets
        services.AddScoped<GenerateBudgetHandler>();
        services.AddScoped<DecideBudgetHandler>();
        services.AddScoped<GetBudgetHandler>();
        services.AddScoped<GetBudgetByServiceOrderHandler>();

        return services;
    }
}
