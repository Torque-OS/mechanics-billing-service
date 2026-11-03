using MechanicsSoftware.BillingService.Domain.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace MechanicsSoftware.BillingService.Infrastructure.Security;

public static class AuthorizationSetup
{
    public static IServiceCollection AddPlatformAuthorization(this IServiceCollection services) =>
        services.AddAuthorization(options =>
        {
            options.AddPolicy(Policies.Staff, policy => policy.RequireRole(Policies.StaffRoles));
            options.AddPolicy(Policies.CustomerOrStaff, policy => policy.RequireRole(Policies.AllRoles));
        });
}
