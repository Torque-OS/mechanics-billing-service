using System.Diagnostics.CodeAnalysis;
using MechanicsSoftware.BillingService.Application.UseCases.Budgets.Handlers;
using MechanicsSoftware.BillingService.Application.UseCases.Payments;
using MechanicsSoftware.BillingService.Application.UseCases.Payments.Handlers;
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
        services.AddScoped<CancelBudgetHandler>();
        services.AddScoped<GetBudgetHandler>();
        services.AddScoped<GetBudgetByServiceOrderHandler>();

        // Payments
        services.AddScoped<PaymentSynchronizer>();
        services.AddScoped<CreatePaymentHandler>();
        services.AddScoped<GetPaymentHandler>();
        services.AddScoped<VerifyPaymentHandler>();
        services.AddScoped<HandleMercadoPagoNotificationHandler>();

        return services;
    }
}
