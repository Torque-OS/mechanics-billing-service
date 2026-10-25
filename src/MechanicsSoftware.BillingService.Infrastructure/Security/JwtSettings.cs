using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace MechanicsSoftware.BillingService.Infrastructure.Security;

/// <summary>
/// Validation side of the platform JWT. Secret, issuer and audience must be identical to
/// mechanics-software and mechanics-lambda, which issue the tokens.
/// </summary>
public sealed record JwtSettings(string Secret, string Issuer, string Audience)
{
    public const string DefaultIssuer = "torque-os";
    public const string DefaultAudience = "mechanics-software-api";

    public const string RoleClaimType = "role";

    public static JwtSettings FromConfiguration(IConfiguration configuration)
    {
        var secret = configuration["JWT_SECRET"];
        if (string.IsNullOrWhiteSpace(secret))
            throw new InvalidOperationException(
                "JWT secret not configured. Set the 'JWT_SECRET' environment variable.");

        return new JwtSettings(
            secret,
            Fallback(configuration["JWT_ISSUER"], DefaultIssuer),
            Fallback(configuration["JWT_AUDIENCE"], DefaultAudience));
    }

    public SymmetricSecurityKey SigningKey => new(Encoding.UTF8.GetBytes(Secret));

    public TokenValidationParameters ToValidationParameters() => new()
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = SigningKey,
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        ValidateIssuer = true,
        ValidIssuer = Issuer,
        ValidateAudience = true,
        ValidAudience = Audience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero,
        NameClaimType = JwtRegisteredClaimNames.Sub,
        RoleClaimType = RoleClaimType,
    };

    private static string Fallback(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value;
}
