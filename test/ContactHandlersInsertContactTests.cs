using HexArch.Application.DTOs;
using HexArch.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SosMVP.Handlers;
using SosMVP.Security;
using test.Fakes;
using test.Support;
using HexArchApplicationException = HexArch.Domain.Exceptions.ApplicationException;

namespace test;

/// <summary>
/// Subject: <c>ContactHandlers.InsertContactAsync</c> with a fake
/// <c>IEnterpriseChildrenService&lt;ContactDTO&gt;</c>, plus the route level answers that happen before
/// the handler runs. Covers RF-2.1 to RF-2.6, RF-8.2, RF-8.3, RF-8.5, RF-8.6, CE-11, CE-16 and the
/// authentication of the route (RF-1.1, RF-1.2).
/// </summary>
public class ContactHandlersInsertContactTests
{
    private const string Route = "/contact/";
    private const string Method = "POST";

    private const string ValidBody =
        """
        {
          "enterpriseId": 5,
          "fullName": "María Fernández Soto"
        }
        """;

    private static ContactDTO ValidDto(string? visibility = null)
    {
        return new ContactDTO
        {
            Id = null,
            EnterpriseId = 5,
            FullName = "María Fernández Soto",
            Visibility = visibility
        };
    }

    private static DbUpdateException ReferentialIntegrityViolation()
    {
        var foreignKeyViolation = new InvalidOperationException(
            "The INSERT statement conflicted with the FOREIGN KEY constraint \"FK_contacto_empresa\".");

        return new DbUpdateException(
            "No se pudo registrar el contacto porque la empresa indicada no existe.",
            foreignKeyViolation);
    }

    private static void AssertOnlyAddAsyncChildWasCalled(FakeContactChildrenService service)
    {
        Assert.Equal(1, service.AddAsyncChildCalls);
        Assert.Equal(0, service.GetAsyncChildCalls);
        Assert.Equal(0, service.GetAsyncChildrenByEnterpriseCalls);
        Assert.Equal(0, service.GetAsyncChildrenByEnterForSelectCalls);
        Assert.Equal(0, service.UpdateAsyncChildCalls);
        Assert.Equal(0, service.UpdateAsyncVisibilityCalls);
    }

    [Fact]
    public async Task ValidContact_IsAnsweredWithCreatedWithoutBodyAndWithoutLocation()
    {
        var service = new FakeContactChildrenService();

        var result = await ContactHandlers.InsertContactAsync(service, ValidDto());

        await HandlerTestSupport.AssertCreatedWithoutLocationAsync(result);
    }

    [Fact]
    public async Task ValidContact_IsDelegatedExactlyOnceToAddAsyncChild()
    {
        var service = new FakeContactChildrenService();
        var dto = ValidDto();

        await ContactHandlers.InsertContactAsync(service, dto);

        AssertOnlyAddAsyncChildWasCalled(service);
        Assert.Same(dto, service.LastAddedDto);
    }

    [Fact]
    public async Task ValidContact_ReachesTheUseCaseWithEveryPropertyUnchanged()
    {
        var service = new FakeContactChildrenService();

        await ContactHandlers.InsertContactAsync(service, ValidDto());

        var received = Assert.IsType<ContactDTO>(service.LastAddedDto);
        Assert.Null(received.Id);
        Assert.Equal(5, received.EnterpriseId);
        Assert.Equal("María Fernández Soto", received.FullName);
    }

    [Fact]
    public async Task VisibilitySentInTheBody_IsNotValidatedNorRewrittenByTheHandler()
    {
        var service = new FakeContactChildrenService();

        var result = await ContactHandlers.InsertContactAsync(service, ValidDto(visibility: "DISABLED"));

        await HandlerTestSupport.AssertCreatedWithoutLocationAsync(result);
        AssertOnlyAddAsyncChildWasCalled(service);
        Assert.Equal("DISABLED", Assert.IsType<ContactDTO>(service.LastAddedDto).Visibility);
    }

    [Fact]
    public async Task ContactOutsideTheAllowedRules_IsTranslatedToBadRequestAndNothingIsWritten()
    {
        var service = new FakeContactChildrenService
        {
            ExceptionToThrow = new EntityException("El nombre completo debe tener entre 10 y 150 caracteres.")
        };
        var dto = ValidDto();
        dto.FullName = "Corto";

        await HandlerTestSupport.AssertTranslationAsync<EntityException>(
            () => ContactHandlers.InsertContactAsync(service, dto),
            StatusCodes.Status400BadRequest);

        AssertOnlyAddAsyncChildWasCalled(service);
    }

    [Fact]
    public async Task EnterpriseWithoutAnIdentifier_IsTranslatedToBadRequest()
    {
        var service = new FakeContactChildrenService
        {
            ExceptionToThrow = new EntityException("El identificador de la empresa debe ser un número mayor que cero.")
        };
        var dto = ValidDto();
        dto.EnterpriseId = 0;

        await HandlerTestSupport.AssertTranslationAsync<EntityException>(
            () => ContactHandlers.InsertContactAsync(service, dto),
            StatusCodes.Status400BadRequest);

        AssertOnlyAddAsyncChildWasCalled(service);
    }

    [Fact]
    public async Task BusinessFailure_IsTranslatedToBadRequest()
    {
        var service = new FakeContactChildrenService
        {
            ExceptionToThrow = new HexArchApplicationException("El contacto debe pertenecer a una empresa existente.")
        };

        await HandlerTestSupport.AssertTranslationAsync<HexArchApplicationException>(
            () => ContactHandlers.InsertContactAsync(service, ValidDto()),
            StatusCodes.Status400BadRequest);

        AssertOnlyAddAsyncChildWasCalled(service);
    }

    [Fact]
    public async Task UnknownEnterprise_IsTranslatedToInternalServerErrorAndIsNotRetried()
    {
        var service = new FakeContactChildrenService { ExceptionToThrow = ReferentialIntegrityViolation() };

        await HandlerTestSupport.AssertTranslationAsync<DbUpdateException>(
            () => ContactHandlers.InsertContactAsync(service, ValidDto()),
            StatusCodes.Status500InternalServerError);

        AssertOnlyAddAsyncChildWasCalled(service);
    }

    [Fact]
    public async Task EntityRuleViolation_DeliversTheMessageInSpanish()
    {
        var service = new FakeContactChildrenService
        {
            ExceptionToThrow = new EntityException("El nombre completo debe tener entre 10 y 150 caracteres.")
        };

        var exception = await Assert.ThrowsAnyAsync<EntityException>(
            () => ContactHandlers.InsertContactAsync(service, ValidDto()));
        var response = await TestHttp.TranslateAsync(exception);

        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Equal("El nombre completo debe tener entre 10 y 150 caracteres.", response.Body);
    }

    [Fact]
    public async Task RequestWithABodyThatIsNotAContact_IsAnsweredWithBadRequestAndTheUseCaseIsNotReached()
    {
        var service = new FakeContactChildrenService();
        await using var host = ContactEndpointTestHost.Start(service, new FakeContactSelectService());

        var response = await host.SendAsync(
            Route,
            Method,
            ContactEndpointTestHost.SessionToken("user"),
            jsonBody: "{ \"enterpriseId\": ");

        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Equal(0, service.TotalCalls);
    }

    [Fact]
    public async Task ValidRequestThroughTheRoute_IsAnsweredWithCreatedAndReachesTheUseCase()
    {
        var service = new FakeContactChildrenService();
        await using var host = ContactEndpointTestHost.Start(service, new FakeContactSelectService());

        var response = await host.SendAsync(
            Route,
            Method,
            ContactEndpointTestHost.SessionToken("user"),
            jsonBody: ValidBody);

        Assert.Equal(StatusCodes.Status201Created, response.StatusCode);
        Assert.Equal(string.Empty, response.Body);
        Assert.False(response.HasHeader("Location"));

        AssertOnlyAddAsyncChildWasCalled(service);
        Assert.Equal(5, Assert.IsType<ContactDTO>(service.LastAddedDto).EnterpriseId);
    }

    [Fact]
    public async Task RequestWithoutSession_IsAnsweredWithUnauthorizedAndTheUseCaseIsNotReached()
    {
        var service = new FakeContactChildrenService();
        await using var host = ContactEndpointTestHost.Start(service, new FakeContactSelectService());

        var response = await host.SendAsync(Route, Method, bearerToken: null, jsonBody: ValidBody);

        Assert.Equal(StatusCodes.Status401Unauthorized, response.StatusCode);
        Assert.NotEqual(StatusCodes.Status403Forbidden, response.StatusCode);
        Assert.Equal(0, service.TotalCalls);
    }
}