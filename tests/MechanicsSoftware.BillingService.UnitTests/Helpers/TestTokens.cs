using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using MechanicsSoftware.BillingService.Infrastructure.Security;
using Microsoft.IdentityModel.Tokens;

namespace MechanicsSoftware.BillingService.UnitTests.Helpers;

/// <summary>Issues tokens shaped like the platform's: HS256, <c>sub</c>, <c>role</c>, iss/aud defaults.</summary>
public static class TestTokens
{
    public const string Secret = "test-secret-for-billing-endpoint-tests-32+";

    public static string Staff(Guid? userId = null) => Create("ATTENDANT", userId ?? Guid.NewGuid());

    public static string Customer(Guid customerId) => Create("CUSTOMER", customerId);

    public static string Create(string role, Guid subject, string secret = Secret, DateTime? expires = null)
    {
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)), SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: JwtSettings.DefaultIssuer,
            audience: JwtSettings.DefaultAudience,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, subject.ToString()),
                new Claim(JwtSettings.RoleClaimType, role),
            ],
            notBefore: (expires ?? DateTime.UtcNow.AddHours(1)).AddHours(-2),
            expires: expires ?? DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
