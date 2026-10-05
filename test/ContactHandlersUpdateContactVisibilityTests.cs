using HexArch.Application.DTOs;
using Microsoft.AspNetCore.Http;
using SosMVP.Handlers;
using SosMVP.Security;
using test.Fakes;
using test.Support;
using HexArchApplicationException = HexArch.Domain.Exceptions.ApplicationException;

namespace test;

/// <summary>
/// Subject: <c>ContactHandlers.UpdateContactVisibilityAsync</c> with a fake
/// <c>IEnterpriseChildrenService&lt;ContactDTO&gt;</c>, plus the route level answers that happen before
/// the handler runs. Covers RF-6.1 to RF-6.6, RF-8.1, RF-8.3 and CE-1, CE-2, CE-5, CE-12, CE-13.
/// </summary>
public class ContactHandlersUpdateContactVisibilityTests
{
    private const string Route = "/contactv/{id}";
    private const string Method = "PUT";

    private const string ValidBody =
        """
        {
          "id": 999,
          "visibility": "DISABLED"
        }
        """;

    private static void AssertOnlyUpdateAsyncVisibilityWasCalled(FakeContactChildrenService service)
    {
        Assert.Equal(1, service.UpdateAsyncVisibilityCalls);
        Assert.Equal(0, service.AddAsyncChildCalls);
        Assert.Equal(0, service.GetAsyncChildCalls);
        Assert.Equal(0, service.GetAsyncChildrenByEnterpriseCalls);
        Assert.Equal(0, service.GetAsyncChildrenByEnterForSelectCalls);
        Assert.Equal(0, service.UpdateAsyncChildCalls);
    }

    [Fact]
    public async Task ValidVisibility_IsAnsweredWithNoContentAndWithoutBody()
    {
        var service = new FakeContactChildrenService();

        var result = await ContactHandlers.UpdateContactVisibilityAsync(service, 7, new ContactDTO { Id = 7, Visibility = "DISABLED" });

        await HandlerTestSupport.AssertNoContentAsync(result);
    }

    [Fact]
    public async Task ValidVisibility_IsDelegatedExactlyOnceToUpdateAsyncVisibility()
    {
        var service = new FakeContactChildrenService();

        await ContactHandlers.UpdateContactVisibilityAsync(service, 7, new ContactDTO { Id = 7, Visibility = "DISABLED" });

        AssertOnlyUpdateAsyncVisibilityWasCalled(service);
    }

    [Fact]
    public async Task RouteId_PrevailsOverTheIdOfTheBody()
    {
        var service = new FakeContactChildrenService();

        await ContactHandlers.UpdateContactVisibilityAsync(service, 7, new ContactDTO { Id = 999, Visibility = "ENABLED" });

        Assert.Equal(7, Assert.IsType<ContactDTO>(service.LastVisibilityDto).Id);
        AssertOnlyUpdateAsyncVisibilityWasCalled(service);
    }

    [Fact]
    public async Task OnlyTheRouteIdAndTheVisibilityReachTheUseCase()
    {
        // CE-2: every other property of the body is dropped before the use case sees it.
        var service = new FakeContactChildrenService();
        var dto = new ContactDTO
        {
            Id = 999,
            EnterpriseId = 5,
            FullName = "Nombre que debe ignorarse",
            Visibility = "DISABLED",
            Enterprise = new EnterpriseDTO { Id = 5, CommercialName = "Constructora Andina" }
        };

        await ContactHandlers.UpdateContactVisibilityAsync(service, 7, dto);

        var received = Assert.IsType<ContactDTO>(service.LastVisibilityDto);
        Assert.Equal(7, received.Id);
        Assert.Equal("DISABLED", received.Visibility);
        Assert.Null(received.EnterpriseId);
        Assert.Null(received.FullName);
        Assert.Null(received.Enterprise);

        AssertOnlyUpdateAsyncVisibilityWasCalled(service);
    }

    [Fact]
    public async Task TheBodyIsNotMutatedByTheHandler()
    {
        var service = new FakeContactChildrenService();
        var dto = new ContactDTO { Id = 999, EnterpriseId = 5, FullName = "Nombre que debe ignorarse", Visibility = "DISABLED" };

        await ContactHandlers.UpdateContactVisibilityAsync(service, 7, dto);

        Assert.Equal(999, dto.Id);
        Assert.Equal(5, dto.EnterpriseId);
        Assert.Equal("Nombre que debe ignorarse", dto.FullName);
    }

    [Fact]
    public async Task VisibilityOutsideTheAllowedValues_IsTranslatedToBadRequestAndNothingIsModified()
    {
        // CE-12: the use case validates the visibility, the handler forwards it untouched.
        var service = new FakeContactChildrenService
        {
            ExceptionToThrow = new HexArchApplicationException("La visibilidad debe ser ENABLED o DISABLED.\n")
        };

        await HandlerTestSupport.AssertTranslationAsync<HexArchApplicationException>(
            () => ContactHandlers.UpdateContactVisibilityAsync(service, 7, new ContactDTO { Id = 7, Visibility = "INVALIDA" }),
            StatusCodes.Status400BadRequest);

        AssertOnlyUpdateAsyncVisibilityWasCalled(service);
    }

    [Fact]
    public async Task VisibilityOutsideTheAllowedValues_ReachesTheUseCaseWithoutBeingRewritten()
    {
        var service = new FakeContactChildrenService();

        await ContactHandlers.UpdateContactVisibilityAsync(service, 7, new ContactDTO { Id = 7, Visibility = "INVALIDA" });

        Assert.Equal("INVALIDA", Assert.IsType<ContactDTO>(service.LastVisibilityDto).Visibility);
        AssertOnlyUpdateAsyncVisibilityWasCalled(service);
    }

    [Fact]
    public async Task InvalidVisibility_DeliversTheMessageInSpanish()
    {
        var service = new FakeContactChildrenService
        {
            ExceptionToThrow = new HexArchApplicationException("La visibilidad debe ser ENABLED o DISABLED.\n")
        };

        var exception = await Assert.ThrowsAnyAsync<HexArchApplicationException>(
            () => ContactHandlers.UpdateContactVisibilityAsync(service, 7, new ContactDTO { Id = 7, Visibility = "INVALIDA" }));
        var response = await TestHttp.TranslateAsync(exception);

        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Equal("La visibilidad debe ser ENABLED o DISABLED.\n", response.Body);
    }

    [Fact]
    public async Task UnknownContact_IsTranslatedToNotFoundAndNothingIsModified()
    {
        var service = new FakeContactChildrenService
        {
            ExceptionToThrow = new KeyNotFoundException(
                "No se pudo cambiar la visibilidad del contacto porque no existe un contacto con ese identificador.")
        };

        await HandlerTestSupport.AssertTranslationAsync<KeyNotFoundException>(
            () => ContactHandlers.UpdateContactVisibilityAsync(service, 404, new ContactDTO { Id = 404, Visibility = "DISABLED" }),
            StatusCodes.Status404NotFound);

        AssertOnlyUpdateAsyncVisibilityWasCalled(service);
    }

    [Fact]
    public async Task RecordNotFound_DeliversTheMessageInSpanish()
    {
        var service = new FakeContactChildrenService
        {
            ExceptionToThrow = new KeyNotFoundException(
                "No se pudo cambiar la visibilidad del contacto porque no existe un contacto con ese identificador.")
        };

        var exception = await Assert.ThrowsAnyAsync<KeyNotFoundException>(
            () => ContactHandlers.UpdateContactVisibilityAsync(service, 404, new ContactDTO { Id = 404, Visibility = "DISABLED" }));
        var response = await TestHttp.TranslateAsync(exception);

        Assert.Equal(StatusCodes.Status404NotFound, response.StatusCode);
        Assert.Equal(
            "No se pudo cambiar la visibilidad del contacto porque no existe un contacto con ese identificador.",
            response.Body);
    }

    [Fact]
    public async Task ValidRequestThroughTheRoute_IsAnsweredWithNoContentAndOnlyIdAndVisibilityTravel()
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

        AssertOnlyUpdateAsyncVisibilityWasCalled(service);

        var received = Assert.IsType<ContactDTO>(service.LastVisibilityDto);
        Assert.Equal(7, received.Id);
        Assert.Equal("DISABLED", received.Visibility);
        Assert.Null(received.EnterpriseId);
        Assert.Null(received.FullName);
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