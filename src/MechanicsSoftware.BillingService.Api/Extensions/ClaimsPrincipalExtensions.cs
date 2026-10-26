using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using MechanicsSoftware.BillingService.Application.UseCases.Budgets.Commands;
using MechanicsSoftware.BillingService.Domain.Authorization;

namespace MechanicsSoftware.BillingService.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Requester from the platform JWT: <c>sub</c> as the user (customer id for CUSTOMER tokens) and the
    /// <c>role</c> claim to tell customers from staff. Null when <c>sub</c> is not a GUID.
    /// </summary>
    public static BudgetRequester? ToRequester(this ClaimsPrincipal user)
    {
        if (!Guid.TryParse(user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var userId))
            return null;

        return user.IsInRole(Policies.CustomerRole)
            ? BudgetRequester.Customer(userId)
            : BudgetRequester.Staff(userId);
    }
}
