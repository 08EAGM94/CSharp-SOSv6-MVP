using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using SosMVP.Security;

namespace test;

public class AdminAuthorizationTests
{
    private static ClaimsPrincipal CreatePrincipal(params Claim[] claims)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "TestSession"));
    }

    private static IAuthorizationService BuildAuthorizationService()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services
            .AddAuthorizationBuilder()
            .AddPolicy(AdminAuthorization.PolicyName, AdminAuthorization.BuildPolicy());

        return services.BuildServiceProvider().GetRequiredService<IAuthorizationService>();
    }

    private static async Task<bool> IsAllowedAsync(ClaimsPrincipal principal)
    {
        var authorizationService = BuildAuthorizationService();
        var result = await authorizationService.AuthorizeAsync(principal, resource: null, AdminAuthorization.PolicyName);

        return result.Succeeded;
    }

    [Fact]
    public async Task AdminRole_SatisfiesAdminOnlyPolicy()
    {
        var principal = CreatePrincipal(new Claim(JwtTokenFactory.RoleClaimType, AdminAuthorization.AdminRole));

        Assert.True(await IsAllowedAsync(principal));
    }

    [Fact]
    public async Task NonAdminRole_DoesNotSatisfyAdminOnlyPolicy()
    {
        var principal = CreatePrincipal(new Claim(JwtTokenFactory.RoleClaimType, "user"));

        Assert.False(await IsAllowedAsync(principal));
    }

    [Fact]
    public async Task SessionWithoutRoleClaim_DoesNotSatisfyAdminOnlyPolicy()
    {
        var principal = CreatePrincipal(new Claim(JwtTokenFactory.IdClaimType, "1"));

        Assert.False(await IsAllowedAsync(principal));
    }

    [Fact]
    public async Task CorruptedSession_DoesNotSatisfyAdminOnlyPolicy()
    {
        var principal = CreatePrincipal(
            new Claim(JwtTokenFactory.IdClaimType, "1"),
            new Claim(JwtTokenFactory.RoleClaimType, string.Empty));

        Assert.False(await IsAllowedAsync(principal));
    }

    [Fact]
    public async Task AnonymousSession_DoesNotSatisfyAdminOnlyPolicy()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity());

        Assert.False(await IsAllowedAsync(principal));
    }

    [Fact]
    public async Task RoleDecision_DependsOnTheRoleClaimAlone()
    {
        var claims = new[]
        {
            new Claim(JwtTokenFactory.RoleClaimType, AdminAuthorization.AdminRole),
            new Claim("Visibility", "DISABLED")
        };

        Assert.True(await IsAllowedAsync(CreatePrincipal(claims)));
    }

    [Fact]
    public void Policy_RequiresTheAdminRoleClaimAndNothingElse()
    {
        var requirements = AdminAuthorization.BuildPolicy().Requirements
            .OfType<ClaimsAuthorizationRequirement>()
            .ToList();

        var requirement = Assert.Single(requirements);

        Assert.Equal(JwtTokenFactory.RoleClaimType, requirement.ClaimType);
        Assert.Equal([AdminAuthorization.AdminRole], requirement.AllowedValues);
    }

    [Fact]
    public void Policy_DeclaresNoRequirementReferencingVisibilityOrTheStore()
    {
        var requirementDescriptions = AdminAuthorization.BuildPolicy().Requirements
            .Select(requirement => requirement.GetType().FullName ?? string.Empty)
            .ToList();

        Assert.DoesNotContain(requirementDescriptions, description => description.Contains("Visibility", StringComparison.Ordinal));
    }

    [Fact]
    public void ReadRoleClaim_ReturnsNullWhenTheSessionCarriesNoRole()
    {
        Assert.Null(AdminAuthorization.ReadRoleClaim(null));
        Assert.Null(AdminAuthorization.ReadRoleClaim(CreatePrincipal()));
        Assert.Null(AdminAuthorization.ReadRoleClaim(CreatePrincipal(new Claim("Visibility", "ENABLED"))));
    }

    [Fact]
    public void ReadUserIdClaim_ReadsTheSessionIdentifierOnly()
    {
        var principal = CreatePrincipal(new Claim(JwtTokenFactory.IdClaimType, "7"));

        Assert.Equal(7, AdminAuthorization.ReadUserIdClaim(principal));
        Assert.Null(AdminAuthorization.ReadUserIdClaim(CreatePrincipal(new Claim(JwtTokenFactory.IdClaimType, "not-a-number"))));
        Assert.Null(AdminAuthorization.ReadUserIdClaim(CreatePrincipal()));
    }
}
