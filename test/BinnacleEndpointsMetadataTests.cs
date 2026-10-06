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
/// Walks the EndpointDataSource produced by <c>MapBinnacleEndpoints</c> and checks the routes, the
/// public names, the response contract declared with <c>Produces</c> and the authorization groups of
/// the ten routes against the tables of RF-13 and plan §3.1. Both data sources created by the two
/// groups have to be flattened, otherwise the endpoints of one of them are never inspected.
/// </summary>
public class BinnacleEndpointsMetadataTests
{
    private static readonly Lazy<IReadOnlyList<RouteEndpoint>> LazyBinnacleEndpoints = new(MaterializeBinnacleEndpoints);

    private static readonly (string Route, string Method, string Name, int[] StatusCodes)[] ExpectedContract =
    [
        ("/binnacle/", "POST", "InsertBinnacle", [201, 400, 401, 500]),
        ("/binnacle/{id}", "GET", "GetBinnacle", [200, 400, 401, 404]),
        ("/binnaclesfu/{page}/{elemsKey}", "GET", "FollowupList", [200, 400, 401]),
        ("/binnaclesr/", "POST", "BinnaclesReport", [200, 400, 401, 403]),
        ("/binnacle/{id}", "PUT", "UpdateBinnacle", [204, 400, 401, 403, 404]),
        ("/binnaclev/{id}", "PUT", "UpdateBinnacleVisibility", [204, 400, 401, 403, 404]),
        ("/binnaclefup/{id}", "PUT", "FollowupPartial", [204, 400, 401, 404]),
        ("/binnaclera/{id}", "PUT", "ResetActivities", [204, 400, 401, 404]),
        ("/binnaclecb/{id}", "PUT", "CancelBinnacle", [204, 400, 401, 404]),
        ("/binnaclefsh/{id}", "PUT", "FinishBinnacle", [204, 400, 401, 404])
    ];

    private static IReadOnlyList<RouteEndpoint> MaterializeBinnacleEndpoints()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddScoped<IBinnacleService>(_ => null!);
        builder.Services.AddSingleton(JwtTestTokens.Factory());

        var app = builder.Build();

        app.MapBinnacleEndpoints();

        return ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .ToList();
    }

    private static RouteEndpoint Find(IReadOnlyList<RouteEndpoint> endpoints, string route, string method)
    {
        return endpoints.Single(endpoint =>
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
    public void AllTenRoutesAreMappedWithTheirExpectedMethods()
    {
        var endpoints = LazyBinnacleEndpoints.Value;

        Assert.Equal(10, endpoints.Count);

        foreach (var (route, method, _, _) in ExpectedContract)
        {
            Assert.True(Find(endpoints, route, method) is not null, $"Falta el endpoint {method} {route}.");
        }
    }

    [Theory]
    [InlineData("/binnacle/", "POST")]
    [InlineData("/binnacle/{id}", "GET")]
    [InlineData("/binnaclesfu/{page}/{elemsKey}", "GET")]
    [InlineData("/binnaclesr/", "POST")]
    [InlineData("/binnacle/{id}", "PUT")]
    [InlineData("/binnaclev/{id}", "PUT")]
    [InlineData("/binnaclefup/{id}", "PUT")]
    [InlineData("/binnaclera/{id}", "PUT")]
    [InlineData("/binnaclecb/{id}", "PUT")]
    [InlineData("/binnaclefsh/{id}", "PUT")]
    public void EveryRoute_DeclaresExactlyTheStatusCodesOfTheTable(string route, string method)
    {
        var expected = ExpectedContract.Single(contract => contract.Route == route && contract.Method == method);

        var declared = DeclaredStatusCodes(Find(LazyBinnacleEndpoints.Value, route, method));

        Assert.Equal(expected.StatusCodes.OrderBy(statusCode => statusCode), declared);
    }

    [Theory]
    [InlineData("/binnacle/", "POST", "InsertBinnacle")]
    [InlineData("/binnacle/{id}", "GET", "GetBinnacle")]
    [InlineData("/binnaclesfu/{page}/{elemsKey}", "GET", "FollowupList")]
    [InlineData("/binnaclesr/", "POST", "BinnaclesReport")]
    [InlineData("/binnacle/{id}", "PUT", "UpdateBinnacle")]
    [InlineData("/binnaclev/{id}", "PUT", "UpdateBinnacleVisibility")]
    [InlineData("/binnaclefup/{id}", "PUT", "FollowupPartial")]
    [InlineData("/binnaclera/{id}", "PUT", "ResetActivities")]
    [InlineData("/binnaclecb/{id}", "PUT", "CancelBinnacle")]
    [InlineData("/binnaclefsh/{id}", "PUT", "FinishBinnacle")]
    public void EveryRoute_DeclaresItsExactPublicName(string route, string method, string expectedName)
    {
        var declaredName = Find(LazyBinnacleEndpoints.Value, route, method).Metadata.GetMetadata<IEndpointNameMetadata>();

        Assert.NotNull(declaredName);
        Assert.Equal(expectedName, declaredName.EndpointName);
    }

    [Theory]
    [InlineData("/binnacle/", "POST")]
    [InlineData("/binnacle/{id}", "GET")]
    [InlineData("/binnaclesfu/{page}/{elemsKey}", "GET")]
    [InlineData("/binnaclesr/", "POST")]
    [InlineData("/binnacle/{id}", "PUT")]
    [InlineData("/binnaclev/{id}", "PUT")]
    [InlineData("/binnaclefup/{id}", "PUT")]
    [InlineData("/binnaclera/{id}", "PUT")]
    [InlineData("/binnaclecb/{id}", "PUT")]
    [InlineData("/binnaclefsh/{id}", "PUT")]
    public void EveryRoute_DeclaresUnauthorizedBecauseNoEndpointIsPublic(string route, string method)
    {
        var endpoints = LazyBinnacleEndpoints.Value;

        Assert.Contains(401, DeclaredStatusCodes(Find(endpoints, route, method)));
        Assert.NotEmpty(Find(endpoints, route, method).Metadata.GetOrderedMetadata<IAuthorizeData>());
        Assert.Null(Find(endpoints, route, method).Metadata.GetMetadata<IAllowAnonymous>());
    }

    [Theory]
    [InlineData("/binnacle/", "POST")]
    [InlineData("/binnacle/{id}", "GET")]
    [InlineData("/binnaclesfu/{page}/{elemsKey}", "GET")]
    [InlineData("/binnaclefup/{id}", "PUT")]
    [InlineData("/binnaclera/{id}", "PUT")]
    [InlineData("/binnaclecb/{id}", "PUT")]
    [InlineData("/binnaclefsh/{id}", "PUT")]
    public void AuthenticatedRoutes_RequireASessionWithoutTheAdminRole(string route, string method)
    {
        var authorizeData = Find(LazyBinnacleEndpoints.Value, route, method)
            .Metadata.GetOrderedMetadata<IAuthorizeData>()
            .ToList();

        Assert.NotEmpty(authorizeData);
        Assert.DoesNotContain(authorizeData, data => data.Policy == AdminAuthorization.PolicyName);
    }

    [Theory]
    [InlineData("/binnaclesr/", "POST")]
    [InlineData("/binnacle/{id}", "PUT")]
    [InlineData("/binnaclev/{id}", "PUT")]
    public void AdminRoutes_RequireTheAdminOnlyPolicy(string route, string method)
    {
        var authorizeData = Find(LazyBinnacleEndpoints.Value, route, method)
            .Metadata.GetOrderedMetadata<IAuthorizeData>()
            .ToList();

        Assert.NotEmpty(authorizeData);
        Assert.Contains(authorizeData, data => data.Policy == AdminAuthorization.PolicyName);
    }

    [Theory]
    [InlineData("/binnacle/{id}", "GET", "GetBinnacleAsync", "id")]
    [InlineData("/binnaclesfu/{page}/{elemsKey}", "GET", "FollowupListAsync", "page")]
    [InlineData("/binnaclesfu/{page}/{elemsKey}", "GET", "FollowupListAsync", "elemsKey")]
    [InlineData("/binnacle/{id}", "PUT", "UpdateBinnacleAsync", "id")]
    [InlineData("/binnaclev/{id}", "PUT", "UpdateBinnacleVisibilityAsync", "id")]
    [InlineData("/binnaclefup/{id}", "PUT", "FollowupPartialAsync", "id")]
    [InlineData("/binnaclera/{id}", "PUT", "ResetActivitiesAsync", "id")]
    [InlineData("/binnaclecb/{id}", "PUT", "CancelBinnacleAsync", "id")]
    [InlineData("/binnaclefsh/{id}", "PUT", "FinishBinnacleAsync", "id")]
    public void RoutesWithIdentifier_ReceiveItAsAnIntegerSoANonNumericValueIsRejected(
        string route,
        string method,
        string handlerName,
        string parameterName)
    {
        var routeParameter = Assert.Single(
            Find(LazyBinnacleEndpoints.Value, route, method).RoutePattern.Parameters,
            candidate => candidate.Name == parameterName);

        Assert.False(routeParameter.IsOptional);

        var identifier = Assert.Single(
            typeof(BinnacleHandlers)
                .GetMethod(handlerName)!
                .GetParameters(),
            parameter => parameter.Name == parameterName);

        Assert.Equal(typeof(int), identifier.ParameterType);
    }

    [Fact]
    public void RoutesWithIdentifier_DeclareTheIdentifierAsARequiredRouteParameter()
    {
        (string Route, string Method, string ParameterName)[] stated =
        [
            ("/binnacle/{id}", "GET", "id"),
            ("/binnaclesfu/{page}/{elemsKey}", "GET", "page"),
            ("/binnaclesfu/{page}/{elemsKey}", "GET", "elemsKey"),
            ("/binnaclev/{id}", "PUT", "id"),
            ("/binnaclefup/{id}", "PUT", "id"),
            ("/binnaclera/{id}", "PUT", "id"),
            ("/binnaclecb/{id}", "PUT", "id"),
            ("/binnaclefsh/{id}", "PUT", "id")
        ];

        foreach (var (route, method, parameterName) in stated)
        {
            var parameter = Assert.Single(
                Find(LazyBinnacleEndpoints.Value, route, method).RoutePattern.Parameters,
                candidate => candidate.Name == parameterName);

            Assert.False(parameter.IsOptional);
        }
    }

    [Theory]
    [InlineData("/binnacle/{id}", "GET", typeof(BinnacleDTO))]
    [InlineData("/binnaclesfu/{page}/{elemsKey}", "GET", typeof(PaginationResult))]
    [InlineData("/binnaclesr/", "POST", typeof(PaginationResult))]
    public void RoutesThatAnswerARead_DeclareTheirResponseType(string route, string method, Type expectedType)
    {
        var declaredType = Assert.Single(Find(LazyBinnacleEndpoints.Value, route, method).Metadata
            .GetOrderedMetadata<IProducesResponseTypeMetadata>()
            .Where(metadata => metadata.StatusCode == StatusCodes.Status200OK && metadata.Type is not null && metadata.Type != typeof(void))
            .Select(metadata => metadata.Type));

        Assert.Equal(expectedType, declaredType);
    }

    [Fact]
    public void RoutesAnsweredWithNoContentOrCreation_DeclareNoResponseType()
    {
        var typedCodes = new[] { ("/binnacle/", "POST"), ("/binnacle/{id}", "PUT"), ("/binnaclev/{id}", "PUT"),
                ("/binnaclefup/{id}", "PUT"), ("/binnaclera/{id}", "PUT"), ("/binnaclecb/{id}", "PUT"),
                ("/binnaclefsh/{id}", "PUT") }
            .SelectMany(route => Find(LazyBinnacleEndpoints.Value, route.Item1, route.Item2).Metadata
                .GetOrderedMetadata<IProducesResponseTypeMetadata>()
                .Where(metadata => metadata.Type is not null && metadata.Type != typeof(void))
                .Select(metadata => metadata.StatusCode))
            .Distinct()
            .ToList();

        Assert.Empty(typedCodes);
    }

    [Fact]
    public void NoRouteDeclaresProblemDetails()
    {
        var problemDetailsNames = LazyBinnacleEndpoints.Value
            .SelectMany(endpoint => endpoint.Metadata.GetOrderedMetadata<IProducesResponseTypeMetadata>())
            .Select(metadata => metadata.Type?.Name)
            .Where(name => string.Equals(name, "ProblemDetails", StringComparison.Ordinal))
            .ToList();

        Assert.Empty(problemDetailsNames);
    }
}