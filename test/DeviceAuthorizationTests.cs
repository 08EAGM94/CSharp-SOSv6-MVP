using HexArch.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SosMVP.Security;
using test.Fakes;
using test.Support;

namespace test;

/// <summary>
/// Subject: the authorization each of the six routes of <c>MapDeviceEndpoints</c> declares, run through
/// the pipeline that answers 401 and 403 in production: the JwtBearer handler validates the token and
/// the production evaluator decides the policy the route declares. Covers RF-1.1 to RF-1.7 and CE-7,
/// CE-8. The endpoint delegate sitting behind that gate is what makes "no function of the endpoint ran"
/// observable: the fake use case reports whether it was reached at all.
/// </summary>
public class DeviceAuthorizationTests
{
    private const string UserRole = "user";

    private const string InsertBody =
        """
        {
          "enterpriseId": 5,
          "typeId": 2,
          "brand": "Bosch",
          "model": "GBH 18V-26",
          "serialNumber": "SN-0001",
          "inventoryNumber": 120
        }
        """;

    private const string UpdateBody =
        """
        {
          "enterpriseId": 5,
          "typeId": 3,
          "brand": "Makita",
          "model": "DHP484",
          "serialNumber": "SN-0007",
          "inventoryNumber": 321
        }
        """;

    private const string VisibilityBody =
        """
        {
          "visibility": "DISABLED"
        }
        """;

    private static FakeDeviceChildrenService ServiceWithData()
    {
        return new FakeDeviceChildrenService
        {
            ChildResult = new DeviceDTO { Id = 5, EnterpriseId = 5, TypeId = 2, Brand = "Bosch", SerialNumber = "SN-0001" },
            ChildrenResult = [new DeviceDTO { Id = 5, EnterpriseId = 5, Brand = "Bosch", SerialNumber = "SN-0001", Visibility = "DISABLED" }],
            ForSelectResult = [new DeviceDTO { Id = 5, Brand = "Bosch", SerialNumber = "SN-0001" }]
        };
    }

    private static string BodyOf(string route, string method)
    {
        if (method != HttpMethods.Post && method != HttpMethods.Put)
        {
            return string.Empty;
        }

        if (route.StartsWith("/devicev/", StringComparison.Ordinal))
        {
            return VisibilityBody;
        }

        return method == HttpMethods.Post ? InsertBody : UpdateBody;
    }

    private static async Task<TestHttpResponse> DispatchAsync(
        DeviceEndpointTestHost host,
        string route,
        string method,
        string? bearerToken)
    {
        return await host.SendAsync(route, method, bearerToken, BodyOf(route, method));
    }

    [Fact]
    public async Task TheSixRoutesAreMappedAndNoneOfThemIsPublic()
    {
        await using var host = DeviceEndpointTestHost.Start(new FakeDeviceChildrenService());

        var endpoints = host.Endpoints();

        Assert.Equal(6, endpoints.Count);
        Assert.All(endpoints, endpoint =>
        {
            Assert.NotEmpty(endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>());
            Assert.Null(endpoint.Metadata.GetMetadata<IAllowAnonymous>());
        });
    }

    [Theory]
    [InlineData("/device/", "POST")]
    [InlineData("/device/{id}", "GET")]
    [InlineData("/devicesent/{enterpriseId}", "GET")]
    [InlineData("/device/{id}", "PUT")]
    [InlineData("/devicev/{id}", "PUT")]
    [InlineData("/devicesentsct/{enterpriseId}", "GET")]
    public async Task AllEndpoints_RequireValidSession_401WhenMissing(string route, string method)
    {
        var service = ServiceWithData();
        await using var host = DeviceEndpointTestHost.Start(service);

        Assert.NotEmpty(host.Find(route, method).Metadata.GetOrderedMetadata<IAuthorizeData>());

        var response = await DispatchAsync(host, route, method, bearerToken: null);

        Assert.Equal(StatusCodes.Status401Unauthorized, response.StatusCode);
        Assert.NotEqual(StatusCodes.Status403Forbidden, response.StatusCode);
        Assert.Equal(0, service.TotalCalls);
    }

    [Theory]
    [InlineData("/device/", "POST")]
    [InlineData("/device/{id}", "GET")]
    [InlineData("/devicesent/{enterpriseId}", "GET")]
    [InlineData("/device/{id}", "PUT")]
    [InlineData("/devicev/{id}", "PUT")]
    [InlineData("/devicesentsct/{enterpriseId}", "GET")]
    public async Task AllEndpoints_RequireValidSession_401WhenExpired(string route, string method)
    {
        var service = ServiceWithData();
        await using var host = DeviceEndpointTestHost.Start(service);

        var expiredSession = JwtTestTokens
            .Factory(-JwtTestTokens.SessionMinutes)
            .CreateToken(JwtTestTokens.FullDto());

        var response = await DispatchAsync(host, route, method, expiredSession);

        Assert.Equal(StatusCodes.Status401Unauthorized, response.StatusCode);
        Assert.NotEqual(StatusCodes.Status403Forbidden, response.StatusCode);
        Assert.Equal(0, service.TotalCalls);
    }

    [Theory]
    [InlineData("/devicesent/{enterpriseId}", "GET", StatusCodes.Status200OK)]
    [InlineData("/device/{id}", "PUT", StatusCodes.Status204NoContent)]
    [InlineData("/devicev/{id}", "PUT", StatusCodes.Status204NoContent)]
    public async Task AdminEndpoints_RequireAdminRole_403WhenTheSessionIsNotAdmin(string route, string method, int adminStatusCode)
    {
        var service = ServiceWithData();
        await using var host = DeviceEndpointTestHost.Start(service);

        Assert.Contains(
            host.Find(route, method).Metadata.GetOrderedMetadata<IAuthorizeData>(),
            data => data.Policy == AdminAuthorization.PolicyName);

        var response = await DispatchAsync(host, route, method, DeviceEndpointTestHost.SessionToken(UserRole));

        Assert.Equal(StatusCodes.Status403Forbidden, response.StatusCode);
        Assert.NotEqual(StatusCodes.Status401Unauthorized, response.StatusCode);
        Assert.Equal(0, service.TotalCalls);

        var asAdmin = await DispatchAsync(host, route, method, DeviceEndpointTestHost.SessionToken(AdminAuthorization.AdminRole));

        Assert.Equal(adminStatusCode, asAdmin.StatusCode);
        Assert.Equal(1, service.TotalCalls);
    }

    [Theory]
    [InlineData("/devicesent/{enterpriseId}", "GET")]
    [InlineData("/device/{id}", "PUT")]
    [InlineData("/devicev/{id}", "PUT")]
    public async Task AdminEndpoints_403WhenTheRoleClaimIsMissing(string route, string method)
    {
        var service = ServiceWithData();
        await using var host = DeviceEndpointTestHost.Start(service);

        var withoutRole = JwtTestTokens.FullDto();
        withoutRole.Role = null;

        var response = await DispatchAsync(host, route, method, JwtTestTokens.Factory().CreateToken(withoutRole));

        Assert.Equal(StatusCodes.Status403Forbidden, response.StatusCode);
        Assert.NotEqual(StatusCodes.Status401Unauthorized, response.StatusCode);
        Assert.Equal(0, service.TotalCalls);
    }

    [Theory]
    [InlineData("/device/", "POST", StatusCodes.Status201Created)]
    [InlineData("/device/{id}", "GET", StatusCodes.Status200OK)]
    [InlineData("/devicesentsct/{enterpriseId}", "GET", StatusCodes.Status200OK)]
    public async Task AuthenticatedEndpoints_AcceptAnyRole_Not403(string route, string method, int expectedStatusCode)
    {
        var service = ServiceWithData();
        await using var host = DeviceEndpointTestHost.Start(service);

        Assert.NotEmpty(host.Find(route, method).Metadata.GetOrderedMetadata<IAuthorizeData>());
        Assert.DoesNotContain(
            host.Find(route, method).Metadata.GetOrderedMetadata<IAuthorizeData>(),
            data => data.Policy == AdminAuthorization.PolicyName);

        var response = await DispatchAsync(host, route, method, DeviceEndpointTestHost.SessionToken(UserRole));

        Assert.NotEqual(StatusCodes.Status403Forbidden, response.StatusCode);
        Assert.NotEqual(StatusCodes.Status401Unauthorized, response.StatusCode);
        Assert.Equal(expectedStatusCode, response.StatusCode);
        Assert.Equal(1, service.TotalCalls);
    }
}
