using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace SosMVP.Security;

public static class AdminAuthorization
{
    public const string PolicyName = "AdminOnly";
    public const string AdminRole = "admin";

    public static AuthorizationPolicy BuildPolicy()
    {
        return new AuthorizationPolicyBuilder()
            .RequireClaim(JwtTokenFactory.RoleClaimType, AdminRole)
            .Build();
    }

    public static string? ReadRoleClaim(ClaimsPrincipal? principal)
    {
        return principal?.FindFirst(JwtTokenFactory.RoleClaimType)?.Value;
    }

    public static int? ReadUserIdClaim(ClaimsPrincipal? principal)
    {
        var raw = principal?.FindFirst(JwtTokenFactory.IdClaimType)?.Value;

        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)
            ? id
            : null;
    }
}
