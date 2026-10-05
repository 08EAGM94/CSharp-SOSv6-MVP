using System.Text.Json;
using HexArch.Application.DTOs;
using HexArch.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using SosMVP.Handlers;
using SosMVP.Security;
using test.Fakes;
using test.Support;
using HexArchApplicationException = HexArch.Domain.Exceptions.ApplicationException;

namespace test;

/// <summary>
/// Subject: <c>ContactHandlers.GetContactsByEnterpriseForSelectAsync</c> with a fake
/// <c>IEnterpriseChildrenService&lt;ContactDTO&gt;</c>, plus the route level answers that happen before
/// the handler runs. Covers RF-7.1 to RF-7.6, RF-8.3, RF-8.4, RF-1.6 and CE-6, CE-6c, CE-13, CE-17.
/// </summary>
public class ContactHandlersGetContactsByEnterpriseForSelectTests
{
    private const string Route = "/contactsentsct/{enterpriseId}";
    private const string Method = "GET";

    private static ContactDTO ContactForSelect(int id, string visibility = "ENABLED")
    {
        return new ContactDTO
        {
            Id = id,
            EnterpriseId = 5,
            FullName = $"Persona de referencia {id}",
            Visibility = visibility
        };
    }

    private static void AssertOnlyGetAsyncChildrenByEnterForSelectWasCalled(FakeContactChildrenService service)
    {
        Assert.Equal(1, service.GetAsyncChildrenByEnterForSelectCalls);
        Assert.Equal(0, service.GetAsyncChildrenByEnterpriseCalls);
        Assert.Equal(0, service.GetAsyncChildCalls);
        Assert.Equal(0, service.AddAsyncChildCalls);
        Assert.Equal(0, service.UpdateAsyncChildCalls);
        Assert.Equal(0, service.UpdateAsyncVisibilityCalls);
    }

    [Fact]
    public async Task EnabledCollection_IsAnsweredOkWithTheContactOfEverySelectEntry()
    {
        var service = new FakeContactChildrenService
        {
            ForSelectResult = new List<ContactDTO> { ContactForSelect(1), ContactForSelect(5) }
        };

        var result = await ContactHandlers.GetContactsByEnterpriseForSelectAsync(service, 5);

        var response = await TestHttp.ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);

        var items = HandlerTestSupport.ReadJsonBody(response.Body).EnumerateArray().ToList();
        Assert.Equal(2, items.Count);

        Assert.Equal(1, items[0].GetProperty("id").GetInt32());
        Assert.Equal(5, items[0].GetProperty("enterpriseId").GetInt32());
        Assert.Equal("Persona de referencia 1", items[0].GetProperty("fullName").GetString());
        Assert.Equal("ENABLED", items[0].GetProperty("visibility").GetString());

        Assert.Equal(5, items[1].GetProperty("id").GetInt32());
        Assert.Equal("ENABLED", items[1].GetProperty("visibility").GetString());

        AssertOnlyGetAsyncChildrenByEnterForSelectWasCalled(service);
    }

    [Fact]
    public async Task OnlyTheRouteEnterpriseIdReachesTheUseCase()
    {
        var service = new FakeContactChildrenService
        {
            ForSelectResult = new List<ContactDTO> { ContactForSelect(1) }
        };

        await ContactHandlers.GetContactsByEnterpriseForSelectAsync(service, 5);

        var requested = Assert.IsType<ContactDTO>(service.LastForSelectDto);
        Assert.Equal(5, requested.EnterpriseId);
        Assert.Null(requested.Id);
        Assert.Null(requested.FullName);
        Assert.Null(requested.Visibility);
        Assert.Null(requested.Enterprise);

        AssertOnlyGetAsyncChildrenByEnterForSelectWasCalled(service);
    }

    [Fact]
    public async Task EveryEntryOfTheSelectListing_CarriesIdEnterpriseIdFullNameAndEnabledVisibility()
    {
        var service = new FakeContactChildrenService
        {
            ForSelectResult = new List<ContactDTO> { ContactForSelect(1) }
        };

        var result = await ContactHandlers.GetContactsByEnterpriseForSelectAsync(service, 5);

        var response = await TestHttp.ExecuteAsync(result);

        var item = HandlerTestSupport.ReadJsonBody(response.Body)[0];
        Assert.Equal(JsonValueKind.Number, item.GetProperty("id").ValueKind);
        Assert.Equal(JsonValueKind.Number, item.GetProperty("enterpriseId").ValueKind);
        Assert.Equal(JsonValueKind.String, item.GetProperty("fullName").ValueKind);
        Assert.Equal(JsonValueKind.String, item.GetProperty("visibility").ValueKind);
        Assert.Equal("ENABLED", item.GetProperty("visibility").GetString());
    }

    [Fact]
    public async Task EnabledCollection_ReachesTheResponseIntactWithoutFilteringNorAddingRecords()
    {
        var service = new FakeContactChildrenService
        {
            ForSelectResult = new List<ContactDTO> { ContactForSelect(1), ContactForSelect(5), ContactForSelect(9) }
        };

        var result = await ContactHandlers.GetContactsByEnterpriseForSelectAsync(service, 5);

        var response = await TestHttp.ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);

        var items = HandlerTestSupport.ReadJsonBody(response.Body).EnumerateArray().ToList();
        Assert.Equal(3, items.Count);
        Assert.Equal([1, 5, 9], items.Select(item => item.GetProperty("id").GetInt32()).ToArray());
        Assert.All(items, item => Assert.Equal("ENABLED", item.GetProperty("visibility").GetString()));

        AssertOnlyGetAsyncChildrenByEnterForSelectWasCalled(service);
    }

    [Fact]
    public async Task TheVisibilityFilterLivesInTheRepositoryAndNotInTheHandler()
    {
        // CE-6c: the ENABLED filter belongs to the untouchable query of the repository, so the handler
        // does not decide anything: whatever collection the use case reports is the response.
        var service = new FakeContactChildrenService
        {
            ForSelectResult = new List<ContactDTO> { ContactForSelect(1), ContactForSelect(2, visibility: "DISABLED") }
        };

        var result = await ContactHandlers.GetContactsByEnterpriseForSelectAsync(service, 5);

        var response = await TestHttp.ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);

        var items = HandlerTestSupport.ReadJsonBody(response.Body).EnumerateArray().ToList();
        Assert.Equal(2, items.Count);
        Assert.Equal(2, items[1].GetProperty("id").GetInt32());
        Assert.Equal("DISABLED", items[1].GetProperty("visibility").GetString());
        Assert.Null(Assert.IsType<ContactDTO>(service.LastForSelectDto).Visibility);
    }

    [Fact]
    public async Task EnterpriseWithoutEnabledContacts_IsAnsweredWithOkAndAnEmptyCollection()
    {
        // CE-6: an enterprise with no enabled contact, and an enterpriseId that matches no registered
        // enterprise, both make the query answer zero records and neither is an error to handle.
        var service = new FakeContactChildrenService { ForSelectResult = [] };

        var result = await ContactHandlers.GetContactsByEnterpriseForSelectAsync(service, 5);

        var response = await TestHttp.ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.Equal("[]", response.Body);
        Assert.Equal(JsonValueKind.Array, HandlerTestSupport.ReadJsonBody(response.Body).ValueKind);
        AssertOnlyGetAsyncChildrenByEnterForSelectWasCalled(service);
    }

    [Fact]
    public async Task BusinessFailureOfTheUseCase_IsTranslatedToBadRequest()
    {
        var service = new FakeContactChildrenService
        {
            ExceptionToThrow = new HexArchApplicationException("No fue posible consultar los contactos habilitados.")
        };

        await HandlerTestSupport.AssertTranslationAsync<HexArchApplicationException>(
            () => ContactHandlers.GetContactsByEnterpriseForSelectAsync(service, 5),
            StatusCodes.Status400BadRequest);

        AssertOnlyGetAsyncChildrenByEnterForSelectWasCalled(service);
    }

    [Fact]
    public async Task EnterpriseOutsideTheAllowedRules_IsTranslatedToBadRequest()
    {
        var service = new FakeContactChildrenService
        {
            ExceptionToThrow = new EntityException("El identificador de la empresa debe ser un número mayor que cero.")
        };

        await HandlerTestSupport.AssertTranslationAsync<EntityException>(
            () => ContactHandlers.GetContactsByEnterpriseForSelectAsync(service, 0),
            StatusCodes.Status400BadRequest);

        AssertOnlyGetAsyncChildrenByEnterForSelectWasCalled(service);
    }

    [Fact]
    public async Task BusinessFailure_DeliversTheMessageInSpanish()
    {
        var service = new FakeContactChildrenService
        {
            ExceptionToThrow = new HexArchApplicationException("No fue posible consultar los contactos habilitados.")
        };

        var exception = await Assert.ThrowsAnyAsync<HexArchApplicationException>(
            () => ContactHandlers.GetContactsByEnterpriseForSelectAsync(service, 5));
        var response = await TestHttp.TranslateAsync(exception);

        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Equal("No fue posible consultar los contactos habilitados.", response.Body);
    }

    [Fact]
    public async Task SessionThatIsNotAdmin_OpensTheRouteAndAnswersOk()
    {
        var service = new FakeContactChildrenService
        {
            ForSelectResult = new List<ContactDTO> { ContactForSelect(1) }
        };
        await using var host = ContactEndpointTestHost.Start(service, new FakeContactSelectService());

        var response = await host.SendAsync(
            Route,
            Method,
            ContactEndpointTestHost.SessionToken("user"),
            enterpriseIdValue: "5");

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.NotEqual(StatusCodes.Status403Forbidden, response.StatusCode);
        Assert.NotEqual(StatusCodes.Status401Unauthorized, response.StatusCode);

        var items = HandlerTestSupport.ReadJsonBody(response.Body).EnumerateArray().ToList();
        Assert.Single(items);
        Assert.Equal(1, items[0].GetProperty("id").GetInt32());
        Assert.Equal("ENABLED", items[0].GetProperty("visibility").GetString());
        Assert.Equal(5, Assert.IsType<ContactDTO>(service.LastForSelectDto).EnterpriseId);

        AssertOnlyGetAsyncChildrenByEnterForSelectWasCalled(service);
    }

    [Fact]
    public async Task NonNumericEnterpriseIdInTheRoute_IsAnsweredWithBadRequestAndTheUseCaseIsNotReached()
    {
        var service = new FakeContactChildrenService
        {
            ForSelectResult = new List<ContactDTO> { ContactForSelect(1) }
        };
        await using var host = ContactEndpointTestHost.Start(service, new FakeContactSelectService());

        var response = await host.SendAsync(
            Route,
            Method,
            ContactEndpointTestHost.SessionToken("user"),
            enterpriseIdValue: "cinco");

        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Equal(0, service.TotalCalls);
    }

    [Fact]
    public async Task RequestWithoutSession_IsAnsweredWithUnauthorizedAndTheUseCaseIsNotReached()
    {
        var service = new FakeContactChildrenService
        {
            ForSelectResult = new List<ContactDTO> { ContactForSelect(1) }
        };
        await using var host = ContactEndpointTestHost.Start(service, new FakeContactSelectService());

        var response = await host.SendAsync(Route, Method, bearerToken: null, enterpriseIdValue: "5");

        Assert.Equal(StatusCodes.Status401Unauthorized, response.StatusCode);
        Assert.NotEqual(StatusCodes.Status403Forbidden, response.StatusCode);
        Assert.Equal(0, service.TotalCalls);
    }
}