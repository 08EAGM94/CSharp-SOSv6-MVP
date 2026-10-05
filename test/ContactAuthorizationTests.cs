using HexArch.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SosMVP.Security;
using test.Fakes;
using test.Support;

namespace test;

/// <summary>
/// Subject: the authorization each of the seven routes of <c>MapContactEndpoints</c> declares, run
/// through the pipeline that answers 401 and 403 in production: the JwtBearer handler validates the
/// token and the production evaluator decides the policy the route declares. Covers RF-1.1 to
/// RF-1.7 and CE-7, CE-8, CE-9. The endpoint delegate sitting behind that gate is what makes "no
/// function of the endpoint ran" observable: the fakes report whether they were reached at all.
/// </summary>
public class ContactAuthorizationTests
{
    private const string UserRole = "user";

    private const string InsertBody =
        """
        {
          "enterpriseId": 5,
          "fullName": "Maria Fernandez Soto"
        }
        """;

    private const string UpdateBody =
        """
        {
          "id": 999,
          "enterpriseId": 5,
          "fullName": "Luis Ocampo Vela"
        }
        """;

    private const string VisibilityBody =
        """
        {
          "id": 999,
          "visibility": "DISABLED"
        }
        """;

    private static (FakeContactChildrenService Children, FakeContactSelectService Select) ServicesWithData()
    {
        return (
            new FakeContactChildrenService
            {
                ChildResult = new ContactDTO { Id = 5, EnterpriseId = 5, FullName = "Maria Fernandez Soto" },
                ChildrenResult = [new ContactDTO { Id = 5, EnterpriseId = 5, FullName = "Maria Fernandez Soto", Visibility = "DISABLED" }],
                ForSelectResult = [new ContactDTO { Id = 5, FullName = "Maria Fernandez Soto" }]
            },
            new FakeContactSelectService
            {
                SelectResult = [new ContactDTO { Id = 5, FullName = "Maria Fernandez Soto" }]
            });
    }

    private static string BodyOf(string route, string method)
    {
        if (method != HttpMethods.Post && method != HttpMethods.Put)
        {
            return string.Empty;
        }

        if (route.StartsWith("/contactv/", StringComparison.Ordinal))
        {
            return VisibilityBody;
        }

        return method == HttpMethods.Post ? InsertBody : UpdateBody;
    }

    private static async Task<TestHttpResponse> DispatchAsync(
        ContactEndpointTestHost host,
        string route,
        string method,
        string? bearerToken)
    {
        return await host.SendAsync(route, method, bearerToken, BodyOf(route, method));
    }

    [Fact]
    public async Task TheSevenRoutesAreMappedAndNoneOfThemIsPublic()
    {
        await using var host = ContactEndpointTestHost.Start(new FakeContactChildrenService(), new FakeContactSelectService());

        var endpoints = host.Endpoints();

        Assert.Equal(7, endpoints.Count);
        Assert.All(endpoints, endpoint =>
        {
            Assert.NotEmpty(endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>());
            Assert.Null(endpoint.Metadata.GetMetadata<IAllowAnonymous>());
        });
    }

    [Theory]
    [InlineData("/contact/", "POST")]
    [InlineData("/contact/{id}", "GET")]
    [InlineData("/contactsent/{enterpriseId}", "GET")]
    [InlineData("/contact/{id}", "PUT")]
    [InlineData("/contactv/{id}", "PUT")]
    [InlineData("/contactsentsct/{enterpriseId}", "GET")]
    [InlineData("/contactsct/", "GET")]
    public async Task AllEndpoints_RequireValidSession_401WhenMissing(string route, string method)
    {
        var (childrenService, selectService) = ServicesWithData();
        await using var host = ContactEndpointTestHost.Start(childrenService, selectService);

        Assert.NotEmpty(host.Find(route, method).Metadata.GetOrderedMetadata<IAuthorizeData>());

        var response = await DispatchAsync(host, route, method, bearerToken: null);

        Assert.Equal(StatusCodes.Status401Unauthorized, response.StatusCode);
        Assert.NotEqual(StatusCodes.Status403Forbidden, response.StatusCode);
        AssertNothingReachedTheUseCase(childrenService, selectService);
    }

    [Theory]
    [InlineData("/contact/", "POST")]
    [InlineData("/contact/{id}", "GET")]
    [InlineData("/contactsent/{enterpriseId}", "GET")]
    [InlineData("/contact/{id}", "PUT")]
    [InlineData("/contactv/{id}", "PUT")]
    [InlineData("/contactsentsct/{enterpriseId}", "GET")]
    [InlineData("/contactsct/", "GET")]
    public async Task AllEndpoints_RequireValidSession_401WhenExpired(string route, string method)
    {
        var (childrenService, selectService) = ServicesWithData();
        await using var host = ContactEndpointTestHost.Start(childrenService, selectService);

        var expiredSession = JwtTestTokens
            .Factory(-JwtTestTokens.SessionMinutes)
            .CreateToken(JwtTestTokens.FullDto());

        var response = await DispatchAsync(host, route, method, expiredSession);

        Assert.Equal(StatusCodes.Status401Unauthorized, response.StatusCode);
        Assert.NotEqual(StatusCodes.Status403Forbidden, response.StatusCode);
        AssertNothingReachedTheUseCase(childrenService, selectService);
    }

    [Theory]
    [InlineData("/contactsent/{enterpriseId}", "GET", StatusCodes.Status200OK)]
    [InlineData("/contact/{id}", "PUT", StatusCodes.Status204NoContent)]
    [InlineData("/contactv/{id}", "PUT", StatusCodes.Status204NoContent)]
    [InlineData("/contactsct/", "GET", StatusCodes.Status200OK)]
    public async Task AdminEndpoints_RequireAdminRole_403WhenTheSessionIsNotAdmin(string route, string method, int adminStatusCode)
    {
        var (childrenService, selectService) = ServicesWithData();
        await using var host = ContactEndpointTestHost.Start(childrenService, selectService);

        Assert.Contains(
            host.Find(route, method).Metadata.GetOrderedMetadata<IAuthorizeData>(),
            data => data.Policy == AdminAuthorization.PolicyName);

        var response = await DispatchAsync(host, route, method, ContactEndpointTestHost.SessionToken(UserRole));

        Assert.Equal(StatusCodes.Status403Forbidden, response.StatusCode);
        Assert.NotEqual(StatusCodes.Status401Unauthorized, response.StatusCode);
        AssertNothingReachedTheUseCase(childrenService, selectService);

        var asAdmin = await DispatchAsync(host, route, method, ContactEndpointTestHost.SessionToken(AdminAuthorization.AdminRole));

        Assert.Equal(adminStatusCode, asAdmin.StatusCode);
        Assert.Equal(1, SelectCalls(childrenService, selectService));
    }

    [Theory]
    [InlineData("/contactsent/{enterpriseId}", "GET")]
    [InlineData("/contact/{id}", "PUT")]
    [InlineData("/contactv/{id}", "PUT")]
    [InlineData("/contactsct/", "GET")]
    public async Task AdminEndpoints_403WhenTheRoleClaimIsMissing(string route, string method)
    {
        var (childrenService, selectService) = ServicesWithData();
        await using var host = ContactEndpointTestHost.Start(childrenService, selectService);

        var withoutRole = JwtTestTokens.FullDto();
        withoutRole.Role = null;

        var response = await DispatchAsync(host, route, method, JwtTestTokens.Factory().CreateToken(withoutRole));

        Assert.Equal(StatusCodes.Status403Forbidden, response.StatusCode);
        Assert.NotEqual(StatusCodes.Status401Unauthorized, response.StatusCode);
        AssertNothingReachedTheUseCase(childrenService, selectService);
    }

    [Theory]
    [InlineData("/contact/", "POST", StatusCodes.Status201Created)]
    [InlineData("/contact/{id}", "GET", StatusCodes.Status200OK)]
    [InlineData("/contactsentsct/{enterpriseId}", "GET", StatusCodes.Status200OK)]
    public async Task AuthenticatedEndpoints_AcceptAnyRole_Not403(string route, string method, int expectedStatusCode)
    {
        var (childrenService, selectService) = ServicesWithData();
        await using var host = ContactEndpointTestHost.Start(childrenService, selectService);

        Assert.NotEmpty(host.Find(route, method).Metadata.GetOrderedMetadata<IAuthorizeData>());
        Assert.DoesNotContain(
            host.Find(route, method).Metadata.GetOrderedMetadata<IAuthorizeData>(),
            data => data.Policy == AdminAuthorization.PolicyName);

        var response = await DispatchAsync(host, route, method, ContactEndpointTestHost.SessionToken(UserRole));

        Assert.NotEqual(StatusCodes.Status403Forbidden, response.StatusCode);
        Assert.NotEqual(StatusCodes.Status401Unauthorized, response.StatusCode);
        Assert.Equal(expectedStatusCode, response.StatusCode);
        Assert.Equal(1, SelectCalls(childrenService, selectService));
    }

    private static int SelectCalls(FakeContactChildrenService childrenService, FakeContactSelectService selectService)
    {
        return childrenService.TotalCalls + selectService.GetAsyncInfoForSelectsCalls;
    }

    private static void AssertNothingReachedTheUseCase(FakeContactChildrenService childrenService, FakeContactSelectService selectService)
    {
        Assert.Equal(0, childrenService.TotalCalls);
        Assert.Equal(0, selectService.GetAsyncInfoForSelectsCalls);
    }
}