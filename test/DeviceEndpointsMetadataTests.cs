using HexArch.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using SosMVP.Handlers;
using SosMVP.Security;
using test.Fakes;
using test.Support;

namespace test;

/// <summary>
/// Walks the EndpointDataSource produced by <c>MapDeviceEndpoints</c> and checks the public name, the
/// response contract declared with <c>Produces</c>, the shape of the route parameters and the
/// authorization metadata of the six routes against section 4 of the plan. Both data sources created
/// by the two groups have to be flattened, otherwise the endpoints of one of them are never inspected.
/// </summary>
public class DeviceEndpointsMetadataTests
{
    private static readonly (string Route, string Method, string Name, int[] StatusCodes)[] ExpectedContract =
    [
        ("/device/", "POST", "InsertDevice", [201, 400, 401, 500]),
        ("/device/{id}", "GET", "GetDevice", [200, 400, 401, 404]),
        ("/devicesent/{enterpriseId}", "GET", "GetDevicesByEnterprise", [200, 400, 401, 403]),
        ("/device/{id}", "PUT", "UpdateDevices", [204, 400, 401, 403, 404]),
        ("/devicev/{id}", "PUT", "UpdateDevicesVisibility", [204, 400, 401, 403, 404]),
        ("/devicesentsct/{enterpriseId}", "GET", "GetDevicesByEnterpriseForSelect", [200, 400, 401])
    ];

    private static async Task<IReadOnlyList<RouteEndpoint>> MaterializeAsync()
    {
        await using var host = DeviceEndpointTestHost.Start(new FakeDeviceChildrenService());

        return host.Endpoints();
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
    public async Task AllSixRoutesAreMappedWithTheirExpectedMethods()
    {
        var endpoints = await MaterializeAsync();

        Assert.Equal(6, endpoints.Count);

        foreach (var (route, method, _, _) in ExpectedContract)
        {
            Assert.True(Find(endpoints, route, method) is not null, $"Falta el endpoint {method} {route}.");
        }
    }

    [Theory]
    [InlineData("/device/", "POST")]
    [InlineData("/device/{id}", "GET")]
    [InlineData("/devicesent/{enterpriseId}", "GET")]
    [InlineData("/device/{id}", "PUT")]
    [InlineData("/devicev/{id}", "PUT")]
    [InlineData("/devicesentsct/{enterpriseId}", "GET")]
    public async Task EveryRoute_DeclaresExactlyTheStatusCodesOfThePlan(string route, string method)
    {
        var expected = ExpectedContract.Single(contract => contract.Route == route && contract.Method == method);

        var declared = DeclaredStatusCodes(Find(await MaterializeAsync(), route, method));

        Assert.Equal(expected.StatusCodes.OrderBy(statusCode => statusCode), declared);
    }

    [Theory]
    [InlineData("/device/", "POST", "InsertDevice")]
    [InlineData("/device/{id}", "GET", "GetDevice")]
    [InlineData("/devicesent/{enterpriseId}", "GET", "GetDevicesByEnterprise")]
    [InlineData("/device/{id}", "PUT", "UpdateDevices")]
    [InlineData("/devicev/{id}", "PUT", "UpdateDevicesVisibility")]
    [InlineData("/devicesentsct/{enterpriseId}", "GET", "GetDevicesByEnterpriseForSelect")]
    public async Task EveryRoute_DeclaresItsExactPublicName(string route, string method, string expectedName)
    {
        var declaredName = Find(await MaterializeAsync(), route, method).Metadata.GetMetadata<IEndpointNameMetadata>();

        Assert.NotNull(declaredName);
        Assert.Equal(expectedName, declaredName.EndpointName);
    }

    [Theory]
    [InlineData("/device/", "POST")]
    [InlineData("/device/{id}", "GET")]
    [InlineData("/devicesent/{enterpriseId}", "GET")]
    [InlineData("/device/{id}", "PUT")]
    [InlineData("/devicev/{id}", "PUT")]
    [InlineData("/devicesentsct/{enterpriseId}", "GET")]
    public async Task EveryRoute_DeclaresUnauthorizedBecauseNoEndpointIsPublic(string route, string method)
    {
        var endpoints = await MaterializeAsync();

        Assert.Contains(401, DeclaredStatusCodes(Find(endpoints, route, method)));
        Assert.NotEmpty(Find(endpoints, route, method).Metadata.GetOrderedMetadata<IAuthorizeData>());
        Assert.Null(Find(endpoints, route, method).Metadata.GetMetadata<IAllowAnonymous>());
    }

    [Theory]
    [InlineData("/device/", "POST")]
    [InlineData("/device/{id}", "GET")]
    [InlineData("/devicesentsct/{enterpriseId}", "GET")]
    public async Task AuthenticatedRoutes_RequireASessionWithoutTheAdminRole(string route, string method)
    {
        var authorizeData = Find(await MaterializeAsync(), route, method)
            .Metadata.GetOrderedMetadata<IAuthorizeData>()
            .ToList();

        Assert.NotEmpty(authorizeData);
        Assert.DoesNotContain(authorizeData, data => data.Policy == AdminAuthorization.PolicyName);
    }

    [Theory]
    [InlineData("/devicesent/{enterpriseId}", "GET")]
    [InlineData("/device/{id}", "PUT")]
    [InlineData("/devicev/{id}", "PUT")]
    public async Task AdminRoutes_RequireTheAdminOnlyPolicy(string route, string method)
    {
        var authorizeData = Find(await MaterializeAsync(), route, method)
            .Metadata.GetOrderedMetadata<IAuthorizeData>()
            .ToList();

        Assert.NotEmpty(authorizeData);
        Assert.Contains(authorizeData, data => data.Policy == AdminAuthorization.PolicyName);
    }

    [Theory]
    [InlineData("/device/{id}", "GET", "GetDeviceAsync", "id")]
    [InlineData("/devicesent/{enterpriseId}", "GET", "GetDevicesByEnterpriseAsync", "enterpriseId")]
    [InlineData("/device/{id}", "PUT", "UpdateDevicesAsync", "id")]
    [InlineData("/devicev/{id}", "PUT", "UpdateDevicesVisibilityAsync", "id")]
    [InlineData("/devicesentsct/{enterpriseId}", "GET", "GetDevicesByEnterpriseForSelectAsync", "enterpriseId")]
    public async Task RoutesWithIdentifier_ReceiveItAsAnIntegerSoANonNumericValueIsRejected(
        string route,
        string method,
        string handlerName,
        string parameterName)
    {
        var endpoints = await MaterializeAsync();

        var endpoint = Find(endpoints, route, method);

        var routeParameter = Assert.Single(
            endpoint.RoutePattern.Parameters,
            candidate => candidate.Name == parameterName);

        Assert.False(routeParameter.IsOptional);

        var identifier = Assert.Single(
            typeof(DeviceHandlers)
                .GetMethod(handlerName)!
                .GetParameters(),
            parameter => parameter.Name == parameterName);

        Assert.Equal(typeof(int), identifier.ParameterType);
    }

    [Theory]
    [InlineData("/device/{id}", "GET", "id")]
    [InlineData("/devicesent/{enterpriseId}", "GET", "enterpriseId")]
    [InlineData("/device/{id}", "PUT", "id")]
    [InlineData("/devicev/{id}", "PUT", "id")]
    [InlineData("/devicesentsct/{enterpriseId}", "GET", "enterpriseId")]
    public async Task RoutesWithIdentifier_DeclareTheIdentifierAsARequiredRouteParameter(
        string route,
        string method,
        string parameterName)
    {
        var endpoints = await MaterializeAsync();

        var parameter = Assert.Single(Find(endpoints, route, method).RoutePattern.Parameters, candidate => candidate.Name == parameterName);

        Assert.False(parameter.IsOptional);
    }

    [Theory]
    [InlineData("/device/{id}", "GET", typeof(DeviceDTO))]
    [InlineData("/devicesent/{enterpriseId}", "GET", typeof(IEnumerable<DeviceDTO>))]
    [InlineData("/devicesentsct/{enterpriseId}", "GET", typeof(IEnumerable<DeviceDTO>))]
    public async Task RoutesWithBody_DeclareTheirResponseType(string route, string method, Type expectedType)
    {
        var declaredType = Assert.Single(Find(await MaterializeAsync(), route, method).Metadata
            .GetOrderedMetadata<IProducesResponseTypeMetadata>()
            .Where(metadata => metadata.StatusCode == StatusCodes.Status200OK && metadata.Type is not null && metadata.Type != typeof(void))
            .Select(metadata => metadata.Type));

        Assert.Equal(expectedType, declaredType);
    }

    [Fact]
    public async Task RoutesWithoutBody_DeclareNoResponseType()
    {
        var endpoints = await MaterializeAsync();

        var typedCodes = new[] { ("/device/", "POST"), ("/device/{id}", "PUT"), ("/devicev/{id}", "PUT") }
            .SelectMany(route => Find(endpoints, route.Item1, route.Item2).Metadata
                .GetOrderedMetadata<IProducesResponseTypeMetadata>()
                .Where(metadata => metadata.Type is not null && metadata.Type != typeof(void))
                .Select(metadata => metadata.StatusCode))
            .Distinct()
            .ToList();

        Assert.Empty(typedCodes);
    }

    [Fact]
    public async Task NoRouteDeclaresProblemDetails()
    {
        var problemDetailsNames = (await MaterializeAsync())
            .SelectMany(endpoint => endpoint.Metadata.GetOrderedMetadata<IProducesResponseTypeMetadata>())
            .Select(metadata => metadata.Type?.Name)
            .Where(name => string.Equals(name, "ProblemDetails", StringComparison.Ordinal))
            .ToList();

        Assert.Empty(problemDetailsNames);
    }
}
