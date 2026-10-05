using HexArch.Application.DTOs;
using Microsoft.AspNetCore.Http;
using SosMVP.Handlers;
using SosMVP.Security;
using test.Fakes;
using test.Support;

namespace test;

/// <summary>
/// Subject: <c>ContactHandlers.GetContactsForSelectAsync</c> with a fake
/// <c>ISelectService&lt;ContactDTO&gt;</c>. Covers RF-10.1, RF-10.3, RF-10.4, CE-14, CE-18: the
/// handler takes no input, answers 200 with the reduced collection the select use case returns (empty
/// included), and the route behind it is admin only.
/// </summary>
public class ContactHandlersGetContactsForSelectTests
{
    private const string Route = "/contactsct/";
    private const string Method = "GET";

    [Fact]
    public async Task StoredProjection_IsAnsweredOkWithIdAndFullNameOfEverySelectEntry()
    {
        var service = new FakeContactSelectService
        {
            SelectResult = new List<ContactDTO>
            {
                new() { Id = 1, FullName = "Contacto habilitado" }
            }
        };

        var result = await ContactHandlers.GetContactsForSelectAsync(service);

        var response = await TestHttp.ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);

        var body = HandlerTestSupport.ReadJsonBody(response.Body);
        Assert.Equal(1, body.GetArrayLength());
        Assert.Equal(1, body[0].GetProperty("id").GetInt32());
        Assert.Equal("Contacto habilitado", body[0].GetProperty("fullName").GetString());

        Assert.Equal(1, service.GetAsyncInfoForSelectsCalls);
    }

    [Fact]
    public async Task EveryStoredEntry_IsAnsweredInTheSameOrderAndWithItsOwnIdAndFullName()
    {
        var service = new FakeContactSelectService
        {
            SelectResult = new List<ContactDTO>
            {
                new() { Id = 1, FullName = "Ana Prado Ruiz" },
                new() { Id = 5, FullName = "Luis Ocampo Vela" },
                new() { Id = 9, FullName = "Sara Nilo Peña" }
            }
        };

        var result = await ContactHandlers.GetContactsForSelectAsync(service);

        var response = await TestHttp.ExecuteAsync(result);

        var body = HandlerTestSupport.ReadJsonBody(response.Body);
        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.Equal(3, body.GetArrayLength());
        Assert.Equal([1, 5, 9], body.EnumerateArray().Select(entry => entry.GetProperty("id").GetInt32()));
        Assert.Equal(
            ["Ana Prado Ruiz", "Luis Ocampo Vela", "Sara Nilo Peña"],
            body.EnumerateArray().Select(entry => entry.GetProperty("fullName").GetString()));

        Assert.Equal(1, service.GetAsyncInfoForSelectsCalls);
    }

    [Fact]
    public async Task EmptyProjection_IsAnsweredOkWithAnEmptyCollection()
    {
        var service = new FakeContactSelectService();

        var result = await ContactHandlers.GetContactsForSelectAsync(service);

        var response = await TestHttp.ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.Equal(0, HandlerTestSupport.ReadJsonBody(response.Body).GetArrayLength());
        Assert.Equal(1, service.GetAsyncInfoForSelectsCalls);
    }

    [Fact]
    public async Task ProjectionWithoutEnterpriseIdAndVisibility_ArrivesNullAndTheHandlerDoesNotFillIt()
    {
        // RF-10.3: the reduced projection lives in the untouchable select repository, so the handler
        // neither projects nor removes properties: the consumer reads "enterpriseId": null and
        // "visibility": null and must not assume they travel with a value.
        var projected = new ContactDTO { Id = 1, FullName = "Contacto habilitado" };
        var service = new FakeContactSelectService { SelectResult = new List<ContactDTO> { projected } };

        var result = await ContactHandlers.GetContactsForSelectAsync(service);

        var response = await TestHttp.ExecuteAsync(result);

        var entry = HandlerTestSupport.ReadJsonBody(response.Body)[0];
        Assert.Equal(1, entry.GetProperty("id").GetInt32());
        Assert.Equal("Contacto habilitado", entry.GetProperty("fullName").GetString());
        Assert.True(entry.TryGetProperty("enterpriseId", out var enterpriseId));
        Assert.Equal(System.Text.Json.JsonValueKind.Null, enterpriseId.ValueKind);
        Assert.True(entry.TryGetProperty("visibility", out var visibility));
        Assert.Equal(System.Text.Json.JsonValueKind.Null, visibility.ValueKind);

        Assert.Null(projected.EnterpriseId);
        Assert.Null(projected.Visibility);
    }

    [Fact]
    public async Task FullDtoReportedByTheSelectService_ReachesTheResponseVerbatim()
    {
        var service = new FakeContactSelectService
        {
            SelectResult = new List<ContactDTO>
            {
                new() { Id = 1, EnterpriseId = 5, FullName = "Contacto habilitado", Visibility = "ENABLED" }
            }
        };

        var result = await ContactHandlers.GetContactsForSelectAsync(service);

        var response = await TestHttp.ExecuteAsync(result);

        var entry = HandlerTestSupport.ReadJsonBody(response.Body)[0];
        Assert.Equal(5, entry.GetProperty("enterpriseId").GetInt32());
        Assert.Equal("ENABLED", entry.GetProperty("visibility").GetString());
        Assert.Equal(1, service.GetAsyncInfoForSelectsCalls);
    }

    [Fact]
    public async Task ValidRequestThroughTheRoute_IsAnsweredOkAndOnlyTheSelectUseCaseIsReached()
    {
        // RF-10.1: this route reads through the select port, never through the children port.
        var childrenService = new FakeContactChildrenService();
        var selectService = new FakeContactSelectService
        {
            SelectResult = new List<ContactDTO> { new() { Id = 1, FullName = "Contacto habilitado" } }
        };
        await using var host = ContactEndpointTestHost.Start(childrenService, selectService);

        var response = await host.SendAsync(Route, Method, ContactEndpointTestHost.SessionToken(AdminAuthorization.AdminRole));

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);

        var body = HandlerTestSupport.ReadJsonBody(response.Body);
        Assert.Equal(1, body.GetArrayLength());
        Assert.Equal(1, body[0].GetProperty("id").GetInt32());

        Assert.Equal(1, selectService.GetAsyncInfoForSelectsCalls);
        Assert.Equal(0, childrenService.TotalCalls);
    }

    [Fact]
    public async Task SessionThatIsNotAdmin_IsAnsweredWithForbiddenAndTheUseCaseIsNotReached()
    {
        var selectService = new FakeContactSelectService
        {
            SelectResult = new List<ContactDTO> { new() { Id = 1, FullName = "Contacto habilitado" } }
        };
        await using var host = ContactEndpointTestHost.Start(new FakeContactChildrenService(), selectService);

        var response = await host.SendAsync(Route, Method, ContactEndpointTestHost.SessionToken("user"));

        Assert.Equal(StatusCodes.Status403Forbidden, response.StatusCode);
        Assert.NotEqual(StatusCodes.Status401Unauthorized, response.StatusCode);
        Assert.Equal(0, selectService.GetAsyncInfoForSelectsCalls);
    }

    [Fact]
    public async Task RequestWithoutSession_IsAnsweredWithUnauthorizedAndTheUseCaseIsNotReached()
    {
        var selectService = new FakeContactSelectService
        {
            SelectResult = new List<ContactDTO> { new() { Id = 1, FullName = "Contacto habilitado" } }
        };
        await using var host = ContactEndpointTestHost.Start(new FakeContactChildrenService(), selectService);

        var response = await host.SendAsync(Route, Method, bearerToken: null);

        Assert.Equal(StatusCodes.Status401Unauthorized, response.StatusCode);
        Assert.NotEqual(StatusCodes.Status403Forbidden, response.StatusCode);
        Assert.Equal(0, selectService.GetAsyncInfoForSelectsCalls);
    }
}