using System.Security.Claims;
using HexArch.Application.Abstractions.PrimaryPorts;
using HexArch.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SosMVP.Extensions;
using SosMVP.Handlers;
using SosMVP.Security;
using test.Support;

namespace test;

/// <summary>
/// Walks the EndpointDataSource produced by MapTypeEndpoints and checks the public name, the
/// response contract declared with Produces and the authorization metadata of the six routes
/// against table 4.3 of the plan. Both data sources created by the two groups have to be
/// flattened, otherwise the endpoints of one of them are never inspected.
/// </summary>
public class TypeEndpointsMetadataTests
{
    private static readonly Lazy<IReadOnlyList<RouteEndpoint>> LazyTypeEndpoints = new(MaterializeTypeEndpoints);

    private static readonly Lazy<IReadOnlyList<RouteEndpoint>> LazyUserEndpoints = new(MaterializeUserEndpoints);

    private static readonly (string Route, string Method, string Name, int[] StatusCodes)[] ExpectedContract =
    [
        ("/type/", "POST", "InsertType", [201, 400, 401, 500]),
        ("/type/{id}", "GET", "GetType", [200, 400, 401, 404]),
        ("/type/", "GET", "GetTypes", [200, 401]),
        ("/type/{id}", "PUT", "UpdateType", [204, 400, 401, 403, 404]),
        ("/typev/{id}", "PUT", "UpdateTypeVisibility", [204, 400, 401, 403, 404]),
        ("/typesct/", "GET", "GetTypesForSelects", [200, 401])
    ];

    private static IReadOnlyList<RouteEndpoint> MaterializeTypeEndpoints()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddScoped<ICommonService<TypeDTO>>(_ => null!);
        builder.Services.AddScoped<ISelectService<TypeDTO>>(_ => null!);
        builder.Services.AddSingleton(JwtTestTokens.Factory());

        var app = builder.Build();

        app.MapTypeEndpoints();

        return ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .ToList();
    }

    private static IReadOnlyList<RouteEndpoint> MaterializeUserEndpoints()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddScoped<ICommonService<UserDTO>>(_ => null!);
        builder.Services.AddScoped<IUserService>(_ => null!);
        builder.Services.AddSingleton(JwtTestTokens.Factory());

        var app = builder.Build();

        app.MapUserEndpoints();

        return ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .ToList();
    }

    private static IReadOnlyList<RouteEndpoint> TypeEndpoints()
    {
        return LazyTypeEndpoints.Value;
    }

    private static RouteEndpoint FindType(string route, string method)
    {
        return TypeEndpoints().Single(endpoint =>
            endpoint.RoutePattern.RawText == route &&
            endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Contains(method, StringComparer.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<int> DeclaredStatusCodes(RouteEndpoint endpoint)
    {
        return endpoint.Metadata
            .GetOrderedMetadata<IProducesResponseTypeMetadata>()
            .Select(metadata => metadata.StatusCode)
            .Distinct()
            .OrderBy(statusCode => statusCode)
            .ToList();
    }

    [Fact]
    public void AllSixRoutesAreDeclaredWithTheirExpectedMethods()
    {
        Assert.Equal(6, TypeEndpoints().Count);

        foreach (var (route, method, _, _) in ExpectedContract)
        {
            Assert.True(FindType(route, method) is not null, $"Falta el endpoint {method} {route}.");
        }
    }

    [Theory]
    [InlineData("/type/", "POST")]
    [InlineData("/type/{id}", "GET")]
    [InlineData("/type/", "GET")]
    [InlineData("/type/{id}", "PUT")]
    [InlineData("/typev/{id}", "PUT")]
    [InlineData("/typesct/", "GET")]
    public void EveryRoute_DeclaresExactlyTheStatusCodesOfThePlan(string route, string method)
    {
        var expected = ExpectedContract.Single(contract => contract.Route == route && contract.Method == method);

        var declared = DeclaredStatusCodes(FindType(route, method));

        Assert.Equal(expected.StatusCodes.OrderBy(statusCode => statusCode), declared);
    }

    [Theory]
    [InlineData("/type/", "POST", "InsertType")]
    [InlineData("/type/{id}", "GET", "GetType")]
    [InlineData("/type/", "GET", "GetTypes")]
    [InlineData("/type/{id}", "PUT", "UpdateType")]
    [InlineData("/typev/{id}", "PUT", "UpdateTypeVisibility")]
    [InlineData("/typesct/", "GET", "GetTypesForSelects")]
    public void EveryRoute_DeclaresItsExactPublicName(string route, string method, string expectedName)
    {
        var declaredName = FindType(route, method).Metadata.GetMetadata<IEndpointNameMetadata>();

        Assert.NotNull(declaredName);
        Assert.Equal(expectedName, declaredName.EndpointName);
    }

    [Theory]
    [InlineData("/type/", "POST")]
    [InlineData("/type/{id}", "GET")]
    [InlineData("/type/", "GET")]
    [InlineData("/type/{id}", "PUT")]
    [InlineData("/typev/{id}", "PUT")]
    [InlineData("/typesct/", "GET")]
    public void EveryRoute_DeclaresUnauthorizedBecauseNoEndpointIsPublic(string route, string method)
    {
        var statusCodes = DeclaredStatusCodes(FindType(route, method));

        Assert.Contains(401, statusCodes);
    }

    [Theory]
    [InlineData("/type/", "POST")]
    [InlineData("/type/{id}", "GET")]
    [InlineData("/type/", "GET")]
    [InlineData("/typesct/", "GET")]
    public void AuthenticatedRoutes_RequireASessionWithoutTheAdminRole(string route, string method)
    {
        var authorizeData = FindType(route, method).Metadata.GetOrderedMetadata<IAuthorizeData>().ToList();

        // A valid session is enough for these four routes: they declare authorization but never the
        // admin policy, so the role of the session is never compared (RF-1.6).
        Assert.NotEmpty(authorizeData);
        Assert.DoesNotContain(authorizeData, data => data.Policy == AdminAuthorization.PolicyName);
    }

    [Theory]
    [InlineData("/type/{id}", "PUT")]
    [InlineData("/typev/{id}", "PUT")]
    public void AdminRoutes_RequireTheAdminOnlyPolicy(string route, string method)
    {
        var authorizeData = FindType(route, method).Metadata.GetOrderedMetadata<IAuthorizeData>().ToList();

        Assert.NotEmpty(authorizeData);
        Assert.Contains(authorizeData, data => data.Policy == AdminAuthorization.PolicyName);
    }

    [Fact]
    public async Task AdminOnlyPolicy_RejectsEverySessionWhoseRoleIsNotAdmin()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services
            .AddAuthorizationBuilder()
            .AddPolicy(AdminAuthorization.PolicyName, AdminAuthorization.BuildPolicy());

        var authorizationService = services.BuildServiceProvider().GetRequiredService<IAuthorizationService>();

        // The policy reads the role of the session only, so a missing, non admin or corrupted role
        // ends in 403 for both update routes (RF-1.3, RF-1.4, RF-1.5).
        Assert.True(await AuthorizeAsync(authorizationService, PrincipalWithRole(AdminAuthorization.AdminRole)));
        Assert.False(await AuthorizeAsync(authorizationService, PrincipalWithRole("user")));
        Assert.False(await AuthorizeAsync(authorizationService, PrincipalWithRole(string.Empty)));
        Assert.False(await AuthorizeAsync(authorizationService, new ClaimsPrincipal(new ClaimsIdentity())));
    }

    [Theory]
    [InlineData(nameof(TypeHandlers.GetTypeAsync))]
    [InlineData(nameof(TypeHandlers.UpdateTypeAsync))]
    [InlineData(nameof(TypeHandlers.UpdateTypeVisibilityAsync))]
    public void RoutesWithIdentifier_ReceiveItAsAnIntegerSoANonNumericValueIsRejected(string handlerName)
    {
        var identifier = Assert.Single(typeof(TypeHandlers)
            .GetMethod(handlerName)!
            .GetParameters()
            .Where(parameter => parameter.Name == "id"));

        Assert.Equal(typeof(int), identifier.ParameterType);
    }

    [Theory]
    [InlineData("/type/{id}", "GET")]
    [InlineData("/type/{id}", "PUT")]
    [InlineData("/typev/{id}", "PUT")]
    public void RoutesWithIdentifier_DeclareTheIdentifierAsARequiredRouteParameter(string route, string method)
    {
        var parameter = Assert.Single(FindType(route, method).RoutePattern.Parameters, candidate => candidate.Name == "id");

        Assert.False(parameter.IsOptional);
    }

    [Fact]
    public void RoutesWithoutBody_DeclareNoResponseType()
    {
        var typedCodes = new[] { ("/type/", "POST"), ("/type/{id}", "PUT"), ("/typev/{id}", "PUT") }
            .SelectMany(route => FindType(route.Item1, route.Item2).Metadata
                .GetOrderedMetadata<IProducesResponseTypeMetadata>()
                .Where(metadata => metadata.Type is not null && metadata.Type != typeof(void))
                .Select(metadata => metadata.StatusCode))
            .Distinct()
            .ToList();

        Assert.Empty(typedCodes);
    }

    [Theory]
    [InlineData("/type/{id}", "GET", typeof(TypeDTO))]
    [InlineData("/type/", "GET", typeof(IEnumerable<TypeDTO>))]
    [InlineData("/typesct/", "GET", typeof(IEnumerable<TypeDTO>))]
    public void RoutesWithBody_DeclareTheirResponseType(string route, string method, Type expectedType)
    {
        var declaredType = Assert.Single(FindType(route, method).Metadata
            .GetOrderedMetadata<IProducesResponseTypeMetadata>()
            .Where(metadata => metadata.StatusCode == StatusCodes.Status200OK && metadata.Type is not null && metadata.Type != typeof(void))
            .Select(metadata => metadata.Type));

        Assert.Equal(expectedType, declaredType);
    }

    [Fact]
    public void NoRouteDeclaresProblemDetails()
    {
        var problemDetailsNames = TypeEndpoints()
            .SelectMany(endpoint => endpoint.Metadata.GetOrderedMetadata<IProducesResponseTypeMetadata>())
            .Select(metadata => metadata.Type?.Name)
            .Where(name => string.Equals(name, "ProblemDetails", StringComparison.Ordinal))
            .ToList();

        Assert.Empty(problemDetailsNames);
    }

    [Fact]
    public void UserEndpointsMetadataRemainIntact()
    {
        var userEndpoints = LazyUserEndpoints.Value;

        Assert.Equal(7, userEndpoints.Count);

        var login = userEndpoints.Single(endpoint => endpoint.RoutePattern.RawText == "/login/");

        Assert.Empty(login.Metadata.GetOrderedMetadata<IAuthorizeData>());
    }

    private static async Task<bool> AuthorizeAsync(
        IAuthorizationService authorizationService,
        ClaimsPrincipal principal)
    {
        var result = await authorizationService.AuthorizeAsync(principal, resource: null, AdminAuthorization.PolicyName);

        return result.Succeeded;
    }

    private static ClaimsPrincipal PrincipalWithRole(string role)
    {
        return new ClaimsPrincipal(
            new ClaimsIdentity(
                [new Claim(JwtTokenFactory.RoleClaimType, role)],
                authenticationType: "TestSession"));
    }
}