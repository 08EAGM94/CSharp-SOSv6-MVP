using HexArch.Application.DTOs;
using HexArch.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using SosMVP.Handlers;
using SosMVP.Security;
using test.Fakes;
using test.Support;

namespace test;

/// <summary>
/// Subject: <c>ContactHandlers.UpdateContactAsync</c> with a fake
/// <c>IEnterpriseChildrenService&lt;ContactDTO&gt;</c>, plus the route level answers that happen before
/// the handler runs. Covers RF-5.1 to RF-5.6, RF-8.1, RF-8.2, RF-8.3, CE-1, CE-5, CE-11, CE-13.
/// </summary>
public class ContactHandlersUpdateContactTests
{
    private const string Route = "/contact/{id}";
    private const string Method = "PUT";

    private const string ValidBody =
        """
        {
          "id": 999,
          "enterpriseId": 5,
          "fullName": "María Fernández Soto"
        }
        """;

    private static ContactDTO Body(int id = 999)
    {
        return new ContactDTO
        {
            Id = id,
            EnterpriseId = 5,
            FullName = "María Fernández Soto"
        };
    }

    private static void AssertOnlyUpdateAsyncChildWasCalled(FakeContactChildrenService service)
    {
        Assert.Equal(1, service.UpdateAsyncChildCalls);
        Assert.Equal(0, service.AddAsyncChildCalls);
        Assert.Equal(0, service.GetAsyncChildCalls);
        Assert.Equal(0, service.GetAsyncChildrenByEnterpriseCalls);
        Assert.Equal(0, service.GetAsyncChildrenByEnterForSelectCalls);
        Assert.Equal(0, service.UpdateAsyncVisibilityCalls);
    }

    [Fact]
    public async Task ValidEditableField_IsAnsweredWithNoContentAndWithoutBody()
    {
        var service = new FakeContactChildrenService();

        var result = await ContactHandlers.UpdateContactAsync(service, 7, Body());

        await HandlerTestSupport.AssertNoContentAsync(result);
    }

    [Fact]
    public async Task ValidEditableField_IsDelegatedExactlyOnceToUpdateAsyncChild()
    {
        var service = new FakeContactChildrenService();

        await ContactHandlers.UpdateContactAsync(service, 7, Body());

        AssertOnlyUpdateAsyncChildWasCalled(service);
    }

    [Fact]
    public async Task RouteId_PrevailsOverTheIdOfTheBody()
    {
        var service = new FakeContactChildrenService();

        await ContactHandlers.UpdateContactAsync(service, 7, Body(id: 999));

        Assert.Equal(7, Assert.IsType<ContactDTO>(service.LastUpdatedDto).Id);
        AssertOnlyUpdateAsyncChildWasCalled(service);
    }

    [Fact]
    public async Task SameBodyIdAsTheRoute_LeavesTheIdTheHandlerSentUnchanged()
    {
        var service = new FakeContactChildrenService();

        await ContactHandlers.UpdateContactAsync(service, 7, Body(id: 7));

        Assert.Equal(7, Assert.IsType<ContactDTO>(service.LastUpdatedDto).Id);
        AssertOnlyUpdateAsyncChildWasCalled(service);
    }

    [Fact]
    public async Task TheRestOfTheBody_ReachesTheUseCaseSoItCanApplyItsOwnRules()
    {
        var service = new FakeContactChildrenService();

        await ContactHandlers.UpdateContactAsync(service, 7, Body());

        var received = Assert.IsType<ContactDTO>(service.LastUpdatedDto);
        Assert.Equal("María Fernández Soto", received.FullName);
        Assert.Equal(5, received.EnterpriseId);

        AssertOnlyUpdateAsyncChildWasCalled(service);
    }

    [Fact]
    public async Task UnknownContact_IsTranslatedToNotFoundAndNothingIsModified()
    {
        var service = new FakeContactChildrenService
        {
            ExceptionToThrow = new KeyNotFoundException(
                "No se pudo actualizar la información del contacto porque no existe un contacto con ese identificador.")
        };

        await HandlerTestSupport.AssertTranslationAsync<KeyNotFoundException>(
            () => ContactHandlers.UpdateContactAsync(service, 404, Body(id: 404)),
            StatusCodes.Status404NotFound);

        AssertOnlyUpdateAsyncChildWasCalled(service);
    }

    [Fact]
    public async Task RecordNotFound_DeliversTheMessageInSpanish()
    {
        var service = new FakeContactChildrenService
        {
            ExceptionToThrow = new KeyNotFoundException(
                "No se pudo actualizar la información del contacto porque no existe un contacto con ese identificador.")
        };

        var exception = await Assert.ThrowsAnyAsync<KeyNotFoundException>(
            () => ContactHandlers.UpdateContactAsync(service, 404, Body(id: 404)));
        var response = await TestHttp.TranslateAsync(exception);

        Assert.Equal(StatusCodes.Status404NotFound, response.StatusCode);
        Assert.Equal(
            "No se pudo actualizar la información del contacto porque no existe un contacto con ese identificador.",
            response.Body);
    }

    [Fact]
    public async Task EditableFieldOutsideTheAllowedRules_IsTranslatedToBadRequestAndNothingIsModified()
    {
        var service = new FakeContactChildrenService
        {
            ExceptionToThrow = new EntityException("El nombre completo debe tener entre 10 y 150 caracteres.")
        };
        var dto = Body();
        dto.FullName = "Corto";

        await HandlerTestSupport.AssertTranslationAsync<EntityException>(
            () => ContactHandlers.UpdateContactAsync(service, 7, dto),
            StatusCodes.Status400BadRequest);

        AssertOnlyUpdateAsyncChildWasCalled(service);
    }

    [Fact]
    public async Task IdentifierOutsideTheAllowedRules_IsTranslatedToBadRequest()
    {
        var service = new FakeContactChildrenService
        {
            ExceptionToThrow = new EntityException("El identificador debe ser un número mayor que cero.")
        };

        await HandlerTestSupport.AssertTranslationAsync<EntityException>(
            () => ContactHandlers.UpdateContactAsync(service, 0, Body(id: 0)),
            StatusCodes.Status400BadRequest);

        AssertOnlyUpdateAsyncChildWasCalled(service);
    }

    [Fact]
    public async Task EntityRuleViolation_DeliversTheMessageInSpanish()
    {
        var service = new FakeContactChildrenService
        {
            ExceptionToThrow = new EntityException("El nombre completo debe tener entre 10 y 150 caracteres.")
        };

        var exception = await Assert.ThrowsAnyAsync<EntityException>(
            () => ContactHandlers.UpdateContactAsync(service, 7, Body()));
        var response = await TestHttp.TranslateAsync(exception);

        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Equal("El nombre completo debe tener entre 10 y 150 caracteres.", response.Body);
    }

    [Fact]
    public async Task ValidRequestThroughTheRoute_IsAnsweredWithNoContentAndTheRouteIdReachesTheUseCase()
    {
        var service = new FakeContactChildrenService();
        await using var host = ContactEndpointTestHost.Start(service, new FakeContactSelectService());

        var response = await host.SendAsync(
            Route,
            Method,
            ContactEndpointTestHost.SessionToken(AdminAuthorization.AdminRole),
            jsonBody: ValidBody,
            idValue: "7");

        Assert.Equal(StatusCodes.Status204NoContent, response.StatusCode);
        Assert.Equal(string.Empty, response.Body);

        AssertOnlyUpdateAsyncChildWasCalled(service);
        Assert.Equal(7, Assert.IsType<ContactDTO>(service.LastUpdatedDto).Id);
    }

    [Fact]
    public async Task SessionThatIsNotAdmin_IsAnsweredWithForbiddenAndTheUseCaseIsNotReached()
    {
        var service = new FakeContactChildrenService();
        await using var host = ContactEndpointTestHost.Start(service, new FakeContactSelectService());

        var response = await host.SendAsync(
            Route,
            Method,
            ContactEndpointTestHost.SessionToken("user"),
            jsonBody: ValidBody,
            idValue: "7");

        Assert.Equal(StatusCodes.Status403Forbidden, response.StatusCode);
        Assert.NotEqual(StatusCodes.Status401Unauthorized, response.StatusCode);
        Assert.Equal(0, service.TotalCalls);
    }

    [Fact]
    public async Task NonNumericIdentifierInTheRoute_IsAnsweredWithBadRequestAndTheUseCaseIsNotReached()
    {
        var service = new FakeContactChildrenService();
        await using var host = ContactEndpointTestHost.Start(service, new FakeContactSelectService());

        var response = await host.SendAsync(
            Route,
            Method,
            ContactEndpointTestHost.SessionToken(AdminAuthorization.AdminRole),
            jsonBody: ValidBody,
            idValue: "abc");

        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Equal(0, service.TotalCalls);
    }

    [Fact]
    public async Task RequestWithoutSession_IsAnsweredWithUnauthorizedAndTheUseCaseIsNotReached()
    {
        var service = new FakeContactChildrenService();
        await using var host = ContactEndpointTestHost.Start(service, new FakeContactSelectService());

        var response = await host.SendAsync(Route, Method, bearerToken: null, jsonBody: ValidBody, idValue: "7");

        Assert.Equal(StatusCodes.Status401Unauthorized, response.StatusCode);
        Assert.NotEqual(StatusCodes.Status403Forbidden, response.StatusCode);
        Assert.Equal(0, service.TotalCalls);
    }
}