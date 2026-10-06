using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SosMVP.Security;
using test.Fakes;
using test.Support;

namespace test;

/// <summary>
/// Subject: the authorization each of the ten routes of <c>MapBinnacleEndpoints</c> declares, run
/// through the pipeline that answers 401 and 403 in production: the JwtBearer handler validates the
/// token and the production evaluator decides the policy the route declares. Covers RF-1.1 to RF-1.8,
/// CE-6, CE-8, CE-9, CE-10, CE-11 and CE-18. The endpoint delegate sitting behind that gate is what
/// makes "no function of the endpoint ran" observable: the fake reports whether it was reached.
/// </summary>
public class BinnacleAuthorizationTests
{
    private const string UserRole = "user";

    private const string InsertBody =
        """
        {
          "userId": 5,
          "contactId": 5,
          "service": "Mantenimiento preventivo de voltaje"
        }
        """;

    private const string ReportBody =
        """
        {
          "page": 5,
          "elemsKey": 5,
          "binnFilter": {}
        }
        """;

    private const string UpdateBody =
        """
        {
          "id": 999,
          "userId": 5,
          "contactId": 5,
          "service": "Mantenimiento preventivo",
          "status": "en proceso",
          "visibility": "ENABLED"
        }
        """;

    private const string VisibilityBody =
        """
        {
          "id": 999,
          "visibility": "DISABLED"
        }
        """;

    private const string FollowupPartialBody =
        """
        {
          "id": 999,
          "userId": 5,
          "activitiesDone": "Cambio de tarjeta de red inalámbrica completado con éxito."
        }
        """;

    private const string CancelBody =
        """
        {
          "id": 999,
          "userId": 5,
          "cancelDesc": "El cliente canceló el servicio el día de hoy."
        }
        """;

    private const string FinishBody =
        """
        {
          "id": 999,
          "userId": 5,
          "customerSignature": "Firma digital del cliente verificada por el sistema."
        }
        """;

    private static string BodyOf(string route, string method)
    {
        if (method != HttpMethods.Post && method != HttpMethods.Put)
        {
            return string.Empty;
        }

        return route switch
        {
            "/binnacle/" => InsertBody,
            "/binnaclesr/" => ReportBody,
            "/binnaclev/{id}" => VisibilityBody,
            "/binnaclefup/{id}" => FollowupPartialBody,
            "/binnaclecb/{id}" => CancelBody,
            "/binnaclefsh/{id}" => FinishBody,
            _ => UpdateBody
        };
    }

    private static async Task<TestHttpResponse> DispatchAsync(
        BinnacleEndpointTestHost host,
        string route,
        string method,
        string? bearerToken)
    {
        return await host.SendAsync(route, method, bearerToken, BodyOf(route, method));
    }

    [Fact]
    public async Task TheTenRoutesAreMappedAndNoneOfThemIsPublic()
    {
        await using var host = BinnacleEndpointTestHost.Start(new FakeBinnacleService());

        var endpoints = host.Endpoints();

        Assert.Equal(10, endpoints.Count);
        Assert.All(endpoints, endpoint =>
        {
            Assert.NotEmpty(endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>());
            Assert.Null(endpoint.Metadata.GetMetadata<IAllowAnonymous>());
        });
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
    public async Task AllEndpoints_RequireValidSession_401WhenMissing(string route, string method)
    {
        var service = new FakeBinnacleService();
        await using var host = BinnacleEndpointTestHost.Start(service);

        Assert.NotEmpty(host.Find(route, method).Metadata.GetOrderedMetadata<IAuthorizeData>());

        var response = await DispatchAsync(host, route, method, bearerToken: null);

        Assert.Equal(StatusCodes.Status401Unauthorized, response.StatusCode);
        Assert.NotEqual(StatusCodes.Status403Forbidden, response.StatusCode);
        AssertNothingReachedTheUseCase(service);
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
    public async Task AllEndpoints_RequireValidSession_401WhenExpired(string route, string method)
    {
        var service = new FakeBinnacleService();
        await using var host = BinnacleEndpointTestHost.Start(service);

        var expiredSession = JwtTestTokens
            .Factory(-JwtTestTokens.SessionMinutes)
            .CreateToken(JwtTestTokens.FullDto());

        var response = await DispatchAsync(host, route, method, expiredSession);

        Assert.Equal(StatusCodes.Status401Unauthorized, response.StatusCode);
        Assert.NotEqual(StatusCodes.Status403Forbidden, response.StatusCode);
        AssertNothingReachedTheUseCase(service);
    }

    [Theory]
    [InlineData("/binnaclesr/", "POST", StatusCodes.Status200OK)]
    [InlineData("/binnacle/{id}", "PUT", StatusCodes.Status204NoContent)]
    [InlineData("/binnaclev/{id}", "PUT", StatusCodes.Status204NoContent)]
    public async Task AdminEndpoints_RequireAdminRole_403WhenTheSessionIsNotAdmin(string route, string method, int adminStatusCode)
    {
        var service = new FakeBinnacleService();
        await using var host = BinnacleEndpointTestHost.Start(service);

        Assert.Contains(
            host.Find(route, method).Metadata.GetOrderedMetadata<IAuthorizeData>(),
            data => data.Policy == AdminAuthorization.PolicyName);

        var response = await DispatchAsync(host, route, method, BinnacleEndpointTestHost.SessionToken(UserRole));

        Assert.Equal(StatusCodes.Status403Forbidden, response.StatusCode);
        Assert.NotEqual(StatusCodes.Status401Unauthorized, response.StatusCode);
        AssertNothingReachedTheUseCase(service);

        var asAdmin = await DispatchAsync(host, route, method, BinnacleEndpointTestHost.SessionToken(AdminAuthorization.AdminRole));

        Assert.Equal(adminStatusCode, asAdmin.StatusCode);
        Assert.Equal(1, service.TotalCalls);
    }

    [Theory]
    [InlineData("/binnaclesr/", "POST")]
    [InlineData("/binnacle/{id}", "PUT")]
    [InlineData("/binnaclev/{id}", "PUT")]
    public async Task AdminEndpoints_403WhenTheRoleClaimIsMissing(string route, string method)
    {
        var service = new FakeBinnacleService();
        await using var host = BinnacleEndpointTestHost.Start(service);

        var withoutRole = JwtTestTokens.FullDto();
        withoutRole.Role = null;

        var response = await DispatchAsync(host, route, method, JwtTestTokens.Factory().CreateToken(withoutRole));

        Assert.Equal(StatusCodes.Status403Forbidden, response.StatusCode);
        Assert.NotEqual(StatusCodes.Status401Unauthorized, response.StatusCode);
        AssertNothingReachedTheUseCase(service);
    }

    [Theory]
    [InlineData("/binnaclesr/", "POST")]
    [InlineData("/binnacle/{id}", "PUT")]
    [InlineData("/binnaclev/{id}", "PUT")]
    public async Task AdminEndpoints_403WhenTheRoleClaimHoldsAnUnknownValue(string route, string method)
    {
        var service = new FakeBinnacleService();
        await using var host = BinnacleEndpointTestHost.Start(service);

        var response = await DispatchAsync(host, route, method, BinnacleEndpointTestHost.SessionToken("supervisor"));

        Assert.Equal(StatusCodes.Status403Forbidden, response.StatusCode);
        Assert.NotEqual(StatusCodes.Status401Unauthorized, response.StatusCode);
        AssertNothingReachedTheUseCase(service);
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
    public async Task AllEndpoints_401WhenTheTokenSignatureWasAltered(string route, string method)
    {
        var service = new FakeBinnacleService();
        await using var host = BinnacleEndpointTestHost.Start(service);

        var response = await DispatchAsync(
            host,
            route,
            method,
            TokenWithBrokenSignature(BinnacleEndpointTestHost.SessionToken(AdminAuthorization.AdminRole)));

        Assert.Equal(StatusCodes.Status401Unauthorized, response.StatusCode);
        Assert.NotEqual(StatusCodes.Status403Forbidden, response.StatusCode);
        AssertNothingReachedTheUseCase(service);
    }

    [Theory]
    [InlineData("/binnacle/", "POST", StatusCodes.Status201Created)]
    [InlineData("/binnacle/{id}", "GET", StatusCodes.Status200OK)]
    [InlineData("/binnaclesfu/{page}/{elemsKey}", "GET", StatusCodes.Status200OK)]
    [InlineData("/binnaclefup/{id}", "PUT", StatusCodes.Status204NoContent)]
    [InlineData("/binnaclera/{id}", "PUT", StatusCodes.Status204NoContent)]
    [InlineData("/binnaclecb/{id}", "PUT", StatusCodes.Status204NoContent)]
    [InlineData("/binnaclefsh/{id}", "PUT", StatusCodes.Status204NoContent)]
    public async Task AuthenticatedEndpoints_AcceptAnyRole_Not403(string route, string method, int expectedStatusCode)
    {
        var service = new FakeBinnacleService();
        await using var host = BinnacleEndpointTestHost.Start(service);

        Assert.NotEmpty(host.Find(route, method).Metadata.GetOrderedMetadata<IAuthorizeData>());
        Assert.DoesNotContain(
            host.Find(route, method).Metadata.GetOrderedMetadata<IAuthorizeData>(),
            data => data.Policy == AdminAuthorization.PolicyName);

        var response = await DispatchAsync(host, route, method, BinnacleEndpointTestHost.SessionToken(UserRole));

        Assert.NotEqual(StatusCodes.Status403Forbidden, response.StatusCode);
        Assert.NotEqual(StatusCodes.Status401Unauthorized, response.StatusCode);
        Assert.Equal(expectedStatusCode, response.StatusCode);
        Assert.Equal(1, service.TotalCalls);
    }

    [Theory]
    [InlineData("/binnacle/", "POST")]
    [InlineData("/binnacle/{id}", "GET")]
    [InlineData("/binnaclesfu/{page}/{elemsKey}", "GET")]
    [InlineData("/binnaclefup/{id}", "PUT")]
    [InlineData("/binnaclera/{id}", "PUT")]
    [InlineData("/binnaclecb/{id}", "PUT")]
    [InlineData("/binnaclefsh/{id}", "PUT")]
    public async Task AuthenticatedEndpoints_401WhenTheSessionHasNoIdClaim(string route, string method)
    {
        var service = new FakeBinnacleService();
        await using var host = BinnacleEndpointTestHost.Start(service);

        var response = await DispatchAsync(host, route, method, BinnacleEndpointTestHost.SessionTokenWithoutIdClaim(UserRole));

        Assert.Equal(StatusCodes.Status401Unauthorized, response.StatusCode);
        Assert.NotEqual(StatusCodes.Status403Forbidden, response.StatusCode);
        AssertNothingReachedTheUseCase(service);
    }

    private static string TokenWithBrokenSignature(string token)
    {
        var signatureStart = token.LastIndexOf('.') + 1;
        var signature = token[signatureStart..];
        var broken = (signature[^1] == 'A' ? 'B' : 'A') + signature[..^1];

        return string.Concat(token.AsSpan(0, signatureStart), broken);
    }

    private static void AssertNothingReachedTheUseCase(FakeBinnacleService service)
    {
        Assert.Equal(0, service.TotalCalls);
    }
}