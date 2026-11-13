using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using MechanicsSoftware.BillingService.Application.UseCases.Budgets.Commands;
using MechanicsSoftware.BillingService.Domain.Authorization;

namespace MechanicsSoftware.BillingService.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static BudgetRequester? ToRequester(this ClaimsPrincipal user)
    {
        if (!Guid.TryParse(user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var userId))
            return null;

        return user.IsInRole(Policies.CustomerRole)
            ? BudgetRequester.Customer(userId)
            : BudgetRequester.Staff(userId);
    }
}
