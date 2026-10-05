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
/// Walks the EndpointDataSource and checks the response contract declared with Produces on each of
/// the seven routes against table 4.6.1 of the plan. Both data sources produced by the group have to
/// be flattened, otherwise the endpoints of one of them are never inspected.
/// </summary>
public class UserEndpointsMetadataTests
{
    private static readonly Lazy<IReadOnlyList<RouteEndpoint>> LazyEndpoints = new(MaterializeEndpoints);

    private static readonly (string Route, string Method, int[] StatusCodes)[] ExpectedContract =
    [
        ("/user/", "POST", [201, 400, 401, 403, 500]),
        ("/user/{id}", "GET", [200, 400, 401, 403, 404]),
        ("/users/", "GET", [200, 401, 403]),
        ("/user/{id}", "PUT", [204, 400, 401, 403, 404, 500]),
        ("/userv/{id}", "PUT", [204, 400, 401, 403, 404]),
        ("/login/", "POST", [200, 400, 404]),
        ("/adminv/", "POST", [200, 400, 401, 403, 404])
    ];

    private static IReadOnlyList<RouteEndpoint> MaterializeEndpoints()
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

    private static IReadOnlyList<RouteEndpoint> Endpoints()
    {
        return LazyEndpoints.Value;
    }

    private static RouteEndpoint Find(string route, string method)
    {
        return Endpoints().Single(endpoint =>
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
    public void AllSevenRoutesAreDeclaredWithTheirExpectedMethods()
    {
        Assert.Equal(7, Endpoints().Count);

        foreach (var (route, method, _) in ExpectedContract)
        {
            Assert.True(Find(route, method) is not null, $"Falta el endpoint {method} {route}.");
        }
    }

    [Theory]
    [InlineData("/user/", "POST")]
    [InlineData("/user/{id}", "GET")]
    [InlineData("/users/", "GET")]
    [InlineData("/user/{id}", "PUT")]
    [InlineData("/userv/{id}", "PUT")]
    [InlineData("/login/", "POST")]
    [InlineData("/adminv/", "POST")]
    public void EveryRoute_DeclaresExactlyTheStatusCodesOfThePlan(string route, string method)
    {
        var expected = ExpectedContract.Single(contract => contract.Route == route && contract.Method == method);

        var declared = DeclaredStatusCodes(Find(route, method));

        Assert.Equal(expected.StatusCodes.OrderBy(statusCode => statusCode), declared);
    }

    [Fact]
    public void Login_DoesNotDeclareUnauthorizedNorForbidden()
    {
        var statusCodes = DeclaredStatusCodes(Find("/login/", "POST"));

        Assert.DoesNotContain(401, statusCodes);
        Assert.DoesNotContain(403, statusCodes);
    }

    [Theory]
    [InlineData("/user/", "POST")]
    [InlineData("/user/{id}", "GET")]
    [InlineData("/users/", "GET")]
    [InlineData("/user/{id}", "PUT")]
    [InlineData("/userv/{id}", "PUT")]
    [InlineData("/adminv/", "POST")]
    public void EveryProtectedRoute_DeclaresUnauthorizedAndForbidden(string route, string method)
    {
        var statusCodes = DeclaredStatusCodes(Find(route, method));

        Assert.Contains(401, statusCodes);
        Assert.Contains(403, statusCodes);
    }

    [Fact]
    public void ProtectedRoutes_RequireTheAdminOnlyPolicy()
    {
        foreach (var (route, method, _) in ExpectedContract.Where(contract => contract.Route != "/login/"))
        {
            var authorizeData = Find(route, method).Metadata.GetOrderedMetadata<IAuthorizeData>().ToList();

            Assert.NotEmpty(authorizeData);
            Assert.Contains(authorizeData, data => data.Policy == AdminAuthorization.PolicyName);
        }
    }

    [Fact]
    public void Login_RequiresNoAuthorizationAtAll()
    {
        Assert.Empty(Find("/login/", "POST").Metadata.GetOrderedMetadata<IAuthorizeData>());
    }

    [Theory]
    [InlineData("/user/", "POST")]
    [InlineData("/user/{id}", "PUT")]
    [InlineData("/userv/{id}", "PUT")]
    public void RoutesWithoutBody_DeclareNoResponseType(string route, string method)
    {
        var typedCodes = Find(route, method).Metadata
            .GetOrderedMetadata<IProducesResponseTypeMetadata>()
            .Where(metadata => metadata.Type is not null && metadata.Type != typeof(void))
            .Select(metadata => metadata.StatusCode)
            .Distinct()
            .ToList();

        Assert.Empty(typedCodes);
    }

    [Theory]
    [InlineData("/user/{id}", "GET", typeof(UserDTO))]
    [InlineData("/users/", "GET", typeof(IEnumerable<UserDTO>))]
    [InlineData("/login/", "POST", typeof(TokenResponse))]
    [InlineData("/adminv/", "POST", typeof(AdminConfirmation))]
    public void RoutesWithBody_DeclareTheirResponseType(string route, string method, Type expectedType)
    {
        var declaredType = Assert.Single(Find(route, method).Metadata
            .GetOrderedMetadata<IProducesResponseTypeMetadata>()
            .Where(metadata => metadata.StatusCode == StatusCodes.Status200OK && metadata.Type is not null && metadata.Type != typeof(void))
            .Select(metadata => metadata.Type));

        Assert.Equal(expectedType, declaredType);
    }

    [Fact]
    public void NoRouteDeclaresProblemDetails()
    {
        var problemDetailsNames = Endpoints()
            .SelectMany(endpoint => endpoint.Metadata.GetOrderedMetadata<IProducesResponseTypeMetadata>())
            .Select(metadata => metadata.Type?.Name)
            .Where(name => string.Equals(name, "ProblemDetails", StringComparison.Ordinal))
            .ToList();

        Assert.Empty(problemDetailsNames);
    }

    [Theory]
    [InlineData(nameof(UserHandlers.GetUserAsync))]
    [InlineData(nameof(UserHandlers.UpdateUserAsync))]
    [InlineData(nameof(UserHandlers.UpdateUserVisibilityAsync))]
    public void RoutesWithIdentifier_ReceiveItAsAnIntegerSoANonNumericValueIsRejected(string handlerName)
    {
        var identifier = Assert.Single(
            typeof(UserHandlers)
                .GetMethod(handlerName)!
                .GetParameters(),
            parameter => parameter.Name == "id");

        Assert.Equal(typeof(int), identifier.ParameterType);
    }

    [Theory]
    [InlineData("/user/{id}", "GET")]
    [InlineData("/user/{id}", "PUT")]
    [InlineData("/userv/{id}", "PUT")]
    public void RoutesWithIdentifier_DeclareTheIdentifierAsARequiredRouteParameter(string route, string method)
    {
        var parameter = Assert.Single(Find(route, method).RoutePattern.Parameters, candidate => candidate.Name == "id");

        Assert.False(parameter.IsOptional);
    }

    [Fact]
    public void CredentialsAreNeverBoundFromTheQueryString()
    {
        var queryBoundParameters = Endpoints()
            .SelectMany(endpoint => endpoint.Metadata.GetOrderedMetadata<IFromQueryMetadata>())
            .ToList();

        Assert.Empty(queryBoundParameters);
    }

    [Theory]
    [InlineData(nameof(UserHandlers.LoginAsync))]
    [InlineData(nameof(UserHandlers.AdminVerificationAsync))]
    public void CredentialRoutes_DeclareTheCredentialsOnlyInTheBodyDto(string handlerName)
    {
        var parameters = typeof(UserHandlers)
            .GetMethod(handlerName)!
            .GetParameters()
            .ToList();

        Assert.Contains(parameters, parameter => parameter.ParameterType == typeof(UserDTO));
        Assert.DoesNotContain(parameters, parameter => parameter.ParameterType == typeof(string) || parameter.ParameterType.IsPrimitive);
        Assert.DoesNotContain(parameters, parameter => parameter.Name is "nickname" or "password");
    }
}