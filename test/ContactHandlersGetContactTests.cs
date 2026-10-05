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
/// Subject: <c>ContactHandlers.GetContactAsync</c> with a fake
/// <c>IEnterpriseChildrenService&lt;ContactDTO&gt;</c>, plus the route level answers that happen before
/// the handler runs. Covers RF-3.1 to RF-3.6, RF-8.1, RF-8.2, RF-8.3, CE-3, CE-10, CE-13 and the
/// authentication of the route (RF-1.1, RF-1.2).
/// </summary>
public class ContactHandlersGetContactTests
{
    private const string Route = "/contact/{id}";
    private const string Method = "GET";

    private static ContactDTO StoredContact(int id = 7, string? visibility = "ENABLED")
    {
        return new ContactDTO
        {
            Id = id,
            EnterpriseId = 5,
            FullName = "María Fernández Soto",
            Visibility = visibility,
            Enterprise = new EnterpriseDTO { Id = 5, CommercialName = "Constructora Andina" }
        };
    }

    private static void AssertOnlyGetAsyncChildWasCalled(FakeContactChildrenService service)
    {
        Assert.Equal(1, service.GetAsyncChildCalls);
        Assert.Equal(0, service.AddAsyncChildCalls);
        Assert.Equal(0, service.GetAsyncChildrenByEnterpriseCalls);
        Assert.Equal(0, service.GetAsyncChildrenByEnterForSelectCalls);
        Assert.Equal(0, service.UpdateAsyncChildCalls);
        Assert.Equal(0, service.UpdateAsyncVisibilityCalls);
    }

    [Fact]
    public async Task ExistingContact_AnswersOkWithTheCompleteDtoReturnedByTheUseCase()
    {
        var service = new FakeContactChildrenService { ChildResult = StoredContact() };

        var result = await ContactHandlers.GetContactAsync(service, 7);

        var response = await TestHttp.ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);

        var body = HandlerTestSupport.ReadJsonBody(response.Body);
        Assert.Equal(7, body.GetProperty("id").GetInt32());
        Assert.Equal(5, body.GetProperty("enterpriseId").GetInt32());
        Assert.Equal("María Fernández Soto", body.GetProperty("fullName").GetString());
        Assert.Equal(5, body.GetProperty("enterprise").GetProperty("id").GetInt32());
        Assert.Equal("Constructora Andina", body.GetProperty("enterprise").GetProperty("commercialName").GetString());

        AssertOnlyGetAsyncChildWasCalled(service);
    }

    [Fact]
    public async Task RouteId_ReachesTheUseCaseAsTheRequestedId()
    {
        var service = new FakeContactChildrenService { ChildResult = StoredContact() };

        await ContactHandlers.GetContactAsync(service, 7);

        Assert.Equal(7, Assert.IsType<ContactDTO>(service.LastRequestedDto).Id);
    }

    [Fact]
    public async Task OnlyTheRouteIdReachesTheUseCase()
    {
        var service = new FakeContactChildrenService { ChildResult = StoredContact() };

        await ContactHandlers.GetContactAsync(service, 7);

        var requested = Assert.IsType<ContactDTO>(service.LastRequestedDto);
        Assert.Equal(7, requested.Id);
        Assert.Null(requested.EnterpriseId);
        Assert.Null(requested.FullName);
        Assert.Null(requested.Visibility);
        Assert.Null(requested.Enterprise);
    }

    [Fact]
    public async Task ResultDtoId_DoesNotChangeTheIdPropagatedToTheUseCase()
    {
        var service = new FakeContactChildrenService { ChildResult = StoredContact(id: 999) };

        var result = await ContactHandlers.GetContactAsync(service, 7);

        var response = await TestHttp.ExecuteAsync(result);

        Assert.Equal(7, Assert.IsType<ContactDTO>(service.LastRequestedDto).Id);
        Assert.Equal(999, HandlerTestSupport.ReadJsonBody(response.Body).GetProperty("id").GetInt32());
        AssertOnlyGetAsyncChildWasCalled(service);
    }

    [Fact]
    public async Task DisabledContact_AnswersOkWithTheCompleteDtoAndIsNotFiltered()
    {
        var service = new FakeContactChildrenService
        {
            ChildResult = StoredContact(visibility: "DISABLED")
        };

        var result = await ContactHandlers.GetContactAsync(service, 7);

        var response = await TestHttp.ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.NotEqual(StatusCodes.Status404NotFound, response.StatusCode);

        var body = HandlerTestSupport.ReadJsonBody(response.Body);
        Assert.Equal(7, body.GetProperty("id").GetInt32());
        Assert.Equal("María Fernández Soto", body.GetProperty("fullName").GetString());
        Assert.Equal("DISABLED", body.GetProperty("visibility").GetString());

        Assert.Null(Assert.IsType<ContactDTO>(service.LastRequestedDto).Visibility);
        AssertOnlyGetAsyncChildWasCalled(service);
    }

    [Fact]
    public async Task ProjectionWithoutVisibility_DoesNotTravelWithAValueInTheResponse()
    {
        // RF-3.6: the individual query of the untouchable repository does not project Visibility, so
        // the consumer must not assume it is present with a value. The handler neither fills that hole
        // nor removes the property from the json.
        var projected = StoredContact();
        projected.Visibility = null;
        var service = new FakeContactChildrenService { ChildResult = projected };

        var result = await ContactHandlers.GetContactAsync(service, 7);

        var response = await TestHttp.ExecuteAsync(result);

        var body = HandlerTestSupport.ReadJsonBody(response.Body);
        Assert.True(body.TryGetProperty("visibility", out var visibility));
        Assert.Equal(System.Text.Json.JsonValueKind.Null, visibility.ValueKind);
        Assert.Equal(7, body.GetProperty("id").GetInt32());

        Assert.Null(projected.Visibility);
        AssertOnlyGetAsyncChildWasCalled(service);
    }

    [Fact]
    public async Task UnknownContact_IsTranslatedToNotFound()
    {
        var service = new FakeContactChildrenService
        {
            ExceptionToThrow = new KeyNotFoundException("No se encontró un contacto con la información solicitada.")
        };

        await HandlerTestSupport.AssertTranslationAsync<KeyNotFoundException>(
            () => ContactHandlers.GetContactAsync(service, 404),
            StatusCodes.Status404NotFound);

        AssertOnlyGetAsyncChildWasCalled(service);
    }

    [Fact]
    public async Task RecordNotFound_DeliversTheMessageInSpanish()
    {
        var service = new FakeContactChildrenService
        {
            ExceptionToThrow = new KeyNotFoundException("No se encontró un contacto con la información solicitada.")
        };

        var exception = await Assert.ThrowsAnyAsync<KeyNotFoundException>(
            () => ContactHandlers.GetContactAsync(service, 404));
        var response = await TestHttp.TranslateAsync(exception);

        Assert.Equal(StatusCodes.Status404NotFound, response.StatusCode);
        Assert.Equal("No se encontró un contacto con la información solicitada.", response.Body);
    }

    [Fact]
    public async Task IdentifierOutsideTheAllowedRules_IsTranslatedToBadRequest()
    {
        var service = new FakeContactChildrenService
        {
            ExceptionToThrow = new EntityException("El identificador debe ser un número mayor que cero.")
        };

        await HandlerTestSupport.AssertTranslationAsync<EntityException>(
            () => ContactHandlers.GetContactAsync(service, 0),
            StatusCodes.Status400BadRequest);

        AssertOnlyGetAsyncChildWasCalled(service);
    }

    [Fact]
    public async Task BusinessFailure_IsTranslatedToBadRequest()
    {
        var service = new FakeContactChildrenService
        {
            ExceptionToThrow = new HexArchApplicationException("El contacto no pertenece a la empresa solicitada.")
        };

        await HandlerTestSupport.AssertTranslationAsync<HexArchApplicationException>(
            () => ContactHandlers.GetContactAsync(service, 7),
            StatusCodes.Status400BadRequest);

        AssertOnlyGetAsyncChildWasCalled(service);
    }

    [Fact]
    public async Task NonNumericIdentifierInTheRoute_IsAnsweredWithBadRequestAndTheUseCaseIsNotReached()
    {
        var service = new FakeContactChildrenService { ChildResult = StoredContact() };
        await using var host = ContactEndpointTestHost.Start(service, new FakeContactSelectService());

        var response = await host.SendAsync(
            Route,
            Method,
            ContactEndpointTestHost.SessionToken("user"),
            idValue: "abc");

        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Equal(0, service.TotalCalls);
    }

    [Fact]
    public async Task ExistingIdentifierThroughTheRoute_IsAnsweredWithOkAndReachesTheUseCase()
    {
        var service = new FakeContactChildrenService { ChildResult = StoredContact() };
        await using var host = ContactEndpointTestHost.Start(service, new FakeContactSelectService());

        var response = await host.SendAsync(
            Route,
            Method,
            ContactEndpointTestHost.SessionToken("user"),
            idValue: "7");

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.Equal(7, HandlerTestSupport.ReadJsonBody(response.Body).GetProperty("id").GetInt32());
        AssertOnlyGetAsyncChildWasCalled(service);
    }

    [Fact]
    public async Task RequestWithoutSession_IsAnsweredWithUnauthorizedAndTheUseCaseIsNotReached()
    {
        var service = new FakeContactChildrenService { ChildResult = StoredContact() };
        await using var host = ContactEndpointTestHost.Start(service, new FakeContactSelectService());

        var response = await host.SendAsync(Route, Method, bearerToken: null, idValue: "7");

        Assert.Equal(StatusCodes.Status401Unauthorized, response.StatusCode);
        Assert.NotEqual(StatusCodes.Status403Forbidden, response.StatusCode);
        Assert.Equal(0, service.TotalCalls);
    }
}