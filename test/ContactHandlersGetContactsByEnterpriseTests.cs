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
/// Subject: <c>ContactHandlers.GetContactsByEnterpriseAsync</c> with a fake
/// <c>IEnterpriseChildrenService&lt;ContactDTO&gt;</c>, plus the route level answers that happen before
/// the handler runs. Covers RF-4.1 to RF-4.7, RF-8.3, RF-8.4, RF-4.7 and CE-6, CE-6b, CE-13.
/// </summary>
public class ContactHandlersGetContactsByEnterpriseTests
{
    private const string Route = "/contactsent/{enterpriseId}";
    private const string Method = "GET";

    private static ContactDTO ContactOfEnterprise(int id, string visibility)
    {
        return new ContactDTO
        {
            Id = id,
            EnterpriseId = 5,
            FullName = $"Persona de referencia {id}",
            Visibility = visibility
        };
    }

    private static void AssertOnlyGetAsyncChildrenByEnterpriseWasCalled(FakeContactChildrenService service)
    {
        Assert.Equal(1, service.GetAsyncChildrenByEnterpriseCalls);
        Assert.Equal(0, service.AddAsyncChildCalls);
        Assert.Equal(0, service.GetAsyncChildCalls);
        Assert.Equal(0, service.GetAsyncChildrenByEnterForSelectCalls);
        Assert.Equal(0, service.UpdateAsyncChildCalls);
        Assert.Equal(0, service.UpdateAsyncVisibilityCalls);
    }

    [Fact]
    public async Task StoredCollection_IsAnsweredOkWithEveryContactOfTheEnterprise()
    {
        var service = new FakeContactChildrenService
        {
            ChildrenResult = new List<ContactDTO>
            {
                ContactOfEnterprise(1, "ENABLED"),
                ContactOfEnterprise(2, "ENABLED")
            }
        };

        var result = await ContactHandlers.GetContactsByEnterpriseAsync(service, 5);

        var response = await TestHttp.ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);

        var body = HandlerTestSupport.ReadJsonBody(response.Body);
        Assert.Equal(2, body.GetArrayLength());

        var first = body[0];
        Assert.Equal(1, first.GetProperty("id").GetInt32());
        Assert.Equal(5, first.GetProperty("enterpriseId").GetInt32());
        Assert.Equal("Persona de referencia 1", first.GetProperty("fullName").GetString());
        Assert.Equal("ENABLED", first.GetProperty("visibility").GetString());

        AssertOnlyGetAsyncChildrenByEnterpriseWasCalled(service);
    }

    [Fact]
    public async Task DisabledContactReportedByTheUseCase_IsAnsweredAndNotRemovedFromTheListing()
    {
        // CE-6b: the complete listing exists to govern the contacts, so the handler does not filter by
        // visibility. The DISABLED record is answered exactly as the use case reported it.
        var service = new FakeContactChildrenService
        {
            ChildrenResult = new List<ContactDTO>
            {
                ContactOfEnterprise(1, "ENABLED"),
                ContactOfEnterprise(2, "DISABLED")
            }
        };

        var result = await ContactHandlers.GetContactsByEnterpriseAsync(service, 5);

        var response = await TestHttp.ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);

        var items = HandlerTestSupport.ReadJsonBody(response.Body).EnumerateArray().ToList();
        Assert.Equal(2, items.Count);
        Assert.Equal(2, items[1].GetProperty("id").GetInt32());
        Assert.Equal(5, items[1].GetProperty("enterpriseId").GetInt32());
        Assert.Equal("Persona de referencia 2", items[1].GetProperty("fullName").GetString());
        Assert.Equal("DISABLED", items[1].GetProperty("visibility").GetString());

        AssertOnlyGetAsyncChildrenByEnterpriseWasCalled(service);
    }

    [Fact]
    public async Task OnlyTheRouteEnterpriseIdReachesTheUseCase()
    {
        var service = new FakeContactChildrenService
        {
            ChildrenResult = new List<ContactDTO> { ContactOfEnterprise(1, "ENABLED") }
        };

        await ContactHandlers.GetContactsByEnterpriseAsync(service, 5);

        var requested = Assert.IsType<ContactDTO>(service.LastEnterpriseDto);
        Assert.Equal(5, requested.EnterpriseId);
        Assert.Null(requested.Id);
        Assert.Null(requested.FullName);
        Assert.Null(requested.Visibility);
        Assert.Null(requested.Enterprise);

        AssertOnlyGetAsyncChildrenByEnterpriseWasCalled(service);
    }

    [Fact]
    public async Task EnterpriseWithoutContacts_IsAnsweredWithOkAndAnEmptyCollection()
    {
        // CE-6: an enterprise with no contacts, and an enterpriseId that matches no registered
        // enterprise, both make the query answer zero records and neither is an error to handle.
        var service = new FakeContactChildrenService { ChildrenResult = [] };

        var result = await ContactHandlers.GetContactsByEnterpriseAsync(service, 5);

        var response = await TestHttp.ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.Equal("[]", response.Body);
        Assert.Equal(System.Text.Json.JsonValueKind.Array, HandlerTestSupport.ReadJsonBody(response.Body).ValueKind);
        AssertOnlyGetAsyncChildrenByEnterpriseWasCalled(service);
    }

    [Fact]
    public async Task BusinessFailureOfTheUseCase_IsTranslatedToBadRequest()
    {
        var service = new FakeContactChildrenService
        {
            ExceptionToThrow = new HexArchApplicationException("No fue posible consultar los contactos de la empresa.")
        };

        await HandlerTestSupport.AssertTranslationAsync<HexArchApplicationException>(
            () => ContactHandlers.GetContactsByEnterpriseAsync(service, 5),
            StatusCodes.Status400BadRequest);

        AssertOnlyGetAsyncChildrenByEnterpriseWasCalled(service);
    }

    [Fact]
    public async Task GenericUseCaseFailure_IsTranslatedToBadRequest()
    {
        var service = new FakeContactChildrenService
        {
            ExceptionToThrow = new InvalidOperationException("No fue posible consultar los contactos de la empresa.")
        };

        await HandlerTestSupport.AssertTranslationAsync<InvalidOperationException>(
            () => ContactHandlers.GetContactsByEnterpriseAsync(service, 5),
            StatusCodes.Status400BadRequest);

        AssertOnlyGetAsyncChildrenByEnterpriseWasCalled(service);
    }

    [Fact]
    public async Task EnterpriseOutsideTheAllowedRules_IsTranslatedToBadRequest()
    {
        var service = new FakeContactChildrenService
        {
            ExceptionToThrow = new EntityException("El identificador de la empresa debe ser un número mayor que cero.")
        };

        await HandlerTestSupport.AssertTranslationAsync<EntityException>(
            () => ContactHandlers.GetContactsByEnterpriseAsync(service, 0),
            StatusCodes.Status400BadRequest);

        AssertOnlyGetAsyncChildrenByEnterpriseWasCalled(service);
    }

    [Fact]
    public async Task BusinessFailure_DeliversTheMessageInSpanish()
    {
        var service = new FakeContactChildrenService
        {
            ExceptionToThrow = new HexArchApplicationException("No fue posible consultar los contactos de la empresa.")
        };

        var exception = await Assert.ThrowsAnyAsync<HexArchApplicationException>(
            () => ContactHandlers.GetContactsByEnterpriseAsync(service, 5));
        var response = await TestHttp.TranslateAsync(exception);

        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Equal("No fue posible consultar los contactos de la empresa.", response.Body);
    }

    [Fact]
    public async Task AdminSession_OpensTheRouteAndAnswersOkWithTheCompleteListing()
    {
        var service = new FakeContactChildrenService
        {
            ChildrenResult = new List<ContactDTO>
            {
                ContactOfEnterprise(1, "ENABLED"),
                ContactOfEnterprise(2, "DISABLED")
            }
        };
        await using var host = ContactEndpointTestHost.Start(service, new FakeContactSelectService());

        var response = await host.SendAsync(
            Route,
            Method,
            ContactEndpointTestHost.SessionToken(AdminAuthorization.AdminRole),
            enterpriseIdValue: "5");

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.Equal(2, HandlerTestSupport.ReadJsonBody(response.Body).GetArrayLength());
        AssertOnlyGetAsyncChildrenByEnterpriseWasCalled(service);
    }

    [Fact]
    public async Task SessionThatIsNotAdmin_IsAnsweredWithForbiddenAndTheUseCaseIsNotReached()
    {
        var service = new FakeContactChildrenService
        {
            ChildrenResult = new List<ContactDTO> { ContactOfEnterprise(1, "ENABLED") }
        };
        await using var host = ContactEndpointTestHost.Start(service, new FakeContactSelectService());

        var response = await host.SendAsync(
            Route,
            Method,
            ContactEndpointTestHost.SessionToken("user"),
            enterpriseIdValue: "5");

        Assert.Equal(StatusCodes.Status403Forbidden, response.StatusCode);
        Assert.NotEqual(StatusCodes.Status401Unauthorized, response.StatusCode);
        Assert.Equal(0, service.TotalCalls);
    }

    [Fact]
    public async Task NonNumericEnterpriseIdInTheRoute_IsAnsweredWithBadRequestAndTheUseCaseIsNotReached()
    {
        var service = new FakeContactChildrenService
        {
            ChildrenResult = new List<ContactDTO> { ContactOfEnterprise(1, "ENABLED") }
        };
        await using var host = ContactEndpointTestHost.Start(service, new FakeContactSelectService());

        var response = await host.SendAsync(
            Route,
            Method,
            ContactEndpointTestHost.SessionToken(AdminAuthorization.AdminRole),
            enterpriseIdValue: "cinco");

        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Equal(0, service.TotalCalls);
    }

    [Fact]
    public async Task RequestWithoutSession_IsAnsweredWithUnauthorizedAndTheUseCaseIsNotReached()
    {
        var service = new FakeContactChildrenService
        {
            ChildrenResult = new List<ContactDTO> { ContactOfEnterprise(1, "ENABLED") }
        };
        await using var host = ContactEndpointTestHost.Start(service, new FakeContactSelectService());

        var response = await host.SendAsync(Route, Method, bearerToken: null, enterpriseIdValue: "5");

        Assert.Equal(StatusCodes.Status401Unauthorized, response.StatusCode);
        Assert.NotEqual(StatusCodes.Status403Forbidden, response.StatusCode);
        Assert.Equal(0, service.TotalCalls);
    }
}