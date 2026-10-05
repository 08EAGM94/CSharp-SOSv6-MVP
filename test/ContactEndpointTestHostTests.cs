using HexArch.Application.DTOs;
using Microsoft.AspNetCore.Http;
using SosMVP.Security;
using test.Fakes;
using test.Support;

namespace test;

/// <summary>
/// Subject: the <c>ContactEndpointTestHost</c> support itself. It has to publish the seven routes of
/// <c>MapContactEndpoints</c> over the production pipeline (JwtBearer + admin policy), register both
/// contact fakes and answer a <c>TestHttpResponse</c> for a request with or without token, with or
/// without role and with or without body, so the endpoint suites can assert what happens before the
/// handler runs. RF-1.1, RF-1.2, RF-1.3, RF-1.4 and RF-1.5.
/// </summary>
public class ContactEndpointTestHostTests
{
    private const string InsertRoute = "/contact/";
    private const string GetByEnterpriseSelectRoute = "/contactsentsct/{enterpriseId}";
    private const string GetSelectRoute = "/contactsct/";
    private const string UpdateRoute = "/contact/{id}";

    [Fact]
    public async Task TheHostPublishesTheSevenRoutesOfTheModule()
    {
        await using var host = ContactEndpointTestHost.Start(new FakeContactChildrenService(), new FakeContactSelectService());

        var endpoints = host.Endpoints();

        Assert.Equal(7, endpoints.Count);

        Assert.NotNull(host.Find(InsertRoute, "POST"));
        Assert.NotNull(host.Find("/contact/{id}", "GET"));
        Assert.NotNull(host.Find("/contactsent/{enterpriseId}", "GET"));
        Assert.NotNull(host.Find(UpdateRoute, "PUT"));
        Assert.NotNull(host.Find("/contactv/{id}", "PUT"));
        Assert.NotNull(host.Find(GetByEnterpriseSelectRoute, "GET"));
        Assert.NotNull(host.Find(GetSelectRoute, "GET"));
    }

    [Fact]
    public async Task AdminSession_ReachesTheChildrenUseCaseAndAnswersWithTheBodyOfTheResponse()
    {
        var service = new FakeContactChildrenService
        {
            ForSelectResult = new List<ContactDTO> { new() { Id = 1, EnterpriseId = 5, FullName = "Contacto habilitado", Visibility = "ENABLED" } }
        };
        await using var host = ContactEndpointTestHost.Start(service, new FakeContactSelectService());

        var response = await host.SendAsync(
            GetByEnterpriseSelectRoute,
            "GET",
            ContactEndpointTestHost.SessionToken(AdminAuthorization.AdminRole),
            enterpriseIdValue: "5");

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.Equal(1, service.GetAsyncChildrenByEnterForSelectCalls);
        Assert.Equal(5, HandlerTestSupport.ReadJsonBody(response.Body)[0].GetProperty("enterpriseId").GetInt32());
    }

    [Fact]
    public async Task AdminSession_ReachesTheSelectUseCaseOfTheOtherFake()
    {
        var selectService = new FakeContactSelectService
        {
            SelectResult = new List<ContactDTO> { new() { Id = 1, FullName = "Contacto habilitado" } }
        };
        await using var host = ContactEndpointTestHost.Start(new FakeContactChildrenService(), selectService);

        var response = await host.SendAsync(
            GetSelectRoute,
            "GET",
            ContactEndpointTestHost.SessionToken(AdminAuthorization.AdminRole));

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.Equal(1, selectService.GetAsyncInfoForSelectsCalls);
        Assert.Equal("Contacto habilitado", HandlerTestSupport.ReadJsonBody(response.Body)[0].GetProperty("fullName").GetString());
    }

    [Fact]
    public async Task RequestWithJsonBody_ReachesTheUseCaseBoundToTheRoute()
    {
        var service = new FakeContactChildrenService();
        await using var host = ContactEndpointTestHost.Start(service, new FakeContactSelectService());

        var response = await host.SendAsync(
            InsertRoute,
            "POST",
            ContactEndpointTestHost.SessionToken("user"),
            jsonBody: """{"enterpriseId":5,"fullName":"Persona de referencia"}""");

        Assert.Equal(StatusCodes.Status201Created, response.StatusCode);
        Assert.Equal(string.Empty, response.Body);
        Assert.False(response.HasHeader("Location"));

        var added = Assert.IsType<ContactDTO>(service.LastAddedDto);
        Assert.Equal(5, added.EnterpriseId);
        Assert.Equal("Persona de referencia", added.FullName);
    }

    [Fact]
    public async Task NonAdminSession_IsForbiddenOnAnAdminRouteAndTheUseCaseIsNotReached()
    {
        var selectService = new FakeContactSelectService();
        await using var host = ContactEndpointTestHost.Start(new FakeContactChildrenService(), selectService);

        var response = await host.SendAsync(
            GetSelectRoute,
            "GET",
            ContactEndpointTestHost.SessionToken("user"));

        Assert.Equal(StatusCodes.Status403Forbidden, response.StatusCode);
        Assert.Equal(0, selectService.GetAsyncInfoForSelectsCalls);
    }

    [Fact]
    public async Task SessionWithoutRoleClaim_IsForbiddenOnAnAdminRoute()
    {
        var service = new FakeContactChildrenService();
        await using var host = ContactEndpointTestHost.Start(service, new FakeContactSelectService());

        var response = await host.SendAsync(
            UpdateRoute,
            "PUT",
            ContactEndpointTestHost.SessionToken(role: null),
            jsonBody: """{"fullName":"Persona de referencia"}""",
            idValue: "7");

        Assert.Equal(StatusCodes.Status403Forbidden, response.StatusCode);
        Assert.Equal(0, service.TotalCalls);
    }

    [Fact]
    public async Task RequestWithoutSession_IsUnauthorizedAndTheUseCaseIsNotReached()
    {
        var service = new FakeContactChildrenService();
        await using var host = ContactEndpointTestHost.Start(service, new FakeContactSelectService());

        var response = await host.SendAsync(UpdateRoute, "PUT", bearerToken: null, idValue: "7");

        Assert.Equal(StatusCodes.Status401Unauthorized, response.StatusCode);
        Assert.Equal(0, service.TotalCalls);
    }

    [Fact]
    public async Task AnsweredResponse_ExposesStatusBodyAndHeaders()
    {
        var service = new FakeContactChildrenService
        {
            ChildResult = new ContactDTO { Id = 7, EnterpriseId = 5, FullName = "María Fernández Soto" }
        };
        await using var host = ContactEndpointTestHost.Start(service, new FakeContactSelectService());

        var response = await host.SendAsync(
            "/contact/{id}",
            "GET",
            ContactEndpointTestHost.SessionToken("user"),
            idValue: "7");

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.Equal("María Fernández Soto", HandlerTestSupport.ReadJsonBody(response.Body).GetProperty("fullName").GetString());
        Assert.NotNull(response.Headers);
    }
}