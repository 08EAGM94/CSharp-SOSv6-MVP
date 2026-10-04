using HexArch.Application.DTOs;
using HexArch.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using SosMVP.Handlers;
using test.Fakes;
using test.Support;
using HexArchApplicationException = HexArch.Domain.Exceptions.ApplicationException;

namespace test;

/// <summary>
/// Subject: <c>EnterpriseHandlers.GetEnterpriseAsync</c> with a fake <c>ICommonService&lt;EnterpriseDTO&gt;</c>.
/// Covers the cases the plan asks for: happy path, boundary cases and error cases
/// (RF-3.1 to RF-3.5, RF-8.1, RF-8.2, RF-8.3, RF-8.4, RF-8.6, CE-1, CE-3, CE-10, CE-16).
/// </summary>
/// <remarks>
/// RF-3.4 / CE-14 (a non numeric route identifier answered with 400) are not verified here: minimal api
/// rejects the value while binding the route parameter into the <c>int id</c> of the handler, so the
/// rejection happens before the handler body runs and is out of the reach of a fake based suite. They are
/// covered by the metadata suite, exactly as spec 002 does for <c>TypeEndpointsMetadataTests</c>.
/// </remarks>
public class EnterpriseHandlersGetEnterpriseTests
{
    private static EnterpriseDTO StoredEnterprise(int id = 7, string? visibility = "ENABLED")
    {
        return new EnterpriseDTO
        {
            Id = id,
            CommercialName = "Talleres del Norte",
            TradeName = "Tallernor",
            StreetNumber = "123",
            BetweenStreets = "Calle Norte y Calle Sur",
            ContactingWith = "Sr. Ramírez",
            Phones = "5551234567",
            Schedule = "Lunes a viernes de 8 a 18",
            Atention = "Atención en taller",
            Neighborhood = "Colonia Centro",
            Location = "Ciudad de Prueba",
            Email = "contacto@tallernor.example",
            Visibility = visibility
        };
    }

    /// <summary>
    /// The single lookup has to reach the use case through <c>GetAsyncInfo</c> only, and must not touch
    /// any other member of the primary port (RF-3.1, RF-3.2).
    /// </summary>
    private static void AssertOnlyGetAsyncInfoWasCalled(FakeEnterpriseCommonService commonService)
    {
        Assert.Equal(1, commonService.GetAsyncInfoCalls);
        Assert.Equal(0, commonService.AddAsyncInfoCalls);
        Assert.Equal(0, commonService.GetAsyncAllInfoCalls);
        Assert.Equal(0, commonService.UpdateAsyncInfoCalls);
        Assert.Equal(0, commonService.UpdateAsyncVisibilityCalls);
    }

    [Fact]
    public async Task ExistingEnterprise_AnswersOkWithTheCompleteDtoReturnedByTheUseCase()
    {
        // Happy path: RF-3.2 answers 200 with the complete EnterpriseDTO, every property of the dto the
        // use case returned has to reach the consumer, and the lookup is delegated exactly once.
        var commonService = new FakeEnterpriseCommonService { InfoResult = StoredEnterprise() };

        var result = await EnterpriseHandlers.GetEnterpriseAsync(commonService, 7);

        var response = await TestHttp.ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);

        var body = HandlerTestSupport.ReadJsonBody(response.Body);
        Assert.Equal(7, body.GetProperty("id").GetInt32());
        Assert.Equal("Talleres del Norte", body.GetProperty("commercialName").GetString());
        Assert.Equal("Tallernor", body.GetProperty("tradeName").GetString());
        Assert.Equal("123", body.GetProperty("streetNumber").GetString());
        Assert.Equal("Calle Norte y Calle Sur", body.GetProperty("betweenStreets").GetString());
        Assert.Equal("Sr. Ramírez", body.GetProperty("contactingWith").GetString());
        Assert.Equal("5551234567", body.GetProperty("phones").GetString());
        Assert.Equal("Lunes a viernes de 8 a 18", body.GetProperty("schedule").GetString());
        Assert.Equal("Atención en taller", body.GetProperty("atention").GetString());
        Assert.Equal("Colonia Centro", body.GetProperty("neighborhood").GetString());
        Assert.Equal("Ciudad de Prueba", body.GetProperty("location").GetString());
        Assert.Equal("contacto@tallernor.example", body.GetProperty("email").GetString());
        Assert.Equal("ENABLED", body.GetProperty("visibility").GetString());

        AssertOnlyGetAsyncInfoWasCalled(commonService);
    }

    [Fact]
    public async Task RouteId_ReachesTheUseCaseAsTheRequestedId()
    {
        // RF-3.1: the route parameter is the only source of the identifier handed to the use case.
        var commonService = new FakeEnterpriseCommonService { InfoResult = StoredEnterprise() };

        await EnterpriseHandlers.GetEnterpriseAsync(commonService, 7);

        var requested = Assert.IsType<EnterpriseDTO>(commonService.LastRequestedDto);
        Assert.Equal(7, requested.Id);
    }

    [Fact]
    public async Task OnlyTheRouteIdReachesTheUseCase()
    {
        // The route carries no body, so the request dto is built from the identifier alone and cannot
        // carry any other property (RF-3.1, CE-1).
        var commonService = new FakeEnterpriseCommonService { InfoResult = StoredEnterprise() };

        await EnterpriseHandlers.GetEnterpriseAsync(commonService, 7);

        var requested = Assert.IsType<EnterpriseDTO>(commonService.LastRequestedDto);
        Assert.Equal(7, requested.Id);
        Assert.Null(requested.CommercialName);
        Assert.Null(requested.TradeName);
        Assert.Null(requested.StreetNumber);
        Assert.Null(requested.BetweenStreets);
        Assert.Null(requested.ContactingWith);
        Assert.Null(requested.Phones);
        Assert.Null(requested.Schedule);
        Assert.Null(requested.Atention);
        Assert.Null(requested.Neighborhood);
        Assert.Null(requested.Location);
        Assert.Null(requested.Email);
        Assert.Null(requested.Visibility);
    }

    [Fact]
    public async Task ResultDtoId_DoesNotChangeTheIdPropagatedToTheUseCase()
    {
        // Boundary case CE-1: whatever Id the returned dto carries, the identifier that reached the use
        // case is exactly the route one, and the handler does not rewrite the Id of the answered body.
        var commonService = new FakeEnterpriseCommonService { InfoResult = StoredEnterprise(id: 999) };

        var result = await EnterpriseHandlers.GetEnterpriseAsync(commonService, 7);

        var response = await TestHttp.ExecuteAsync(result);
        var requested = Assert.IsType<EnterpriseDTO>(commonService.LastRequestedDto);

        Assert.Equal(7, requested.Id);
        Assert.Equal(999, HandlerTestSupport.ReadJsonBody(response.Body).GetProperty("id").GetInt32());
        AssertOnlyGetAsyncInfoWasCalled(commonService);
    }

    [Fact]
    public async Task DisabledEnterprise_AnswersOkWithTheCompleteDtoAndIsNotFiltered()
    {
        // Boundary case CE-10 / RF-3.5: an existing enterprise with Visibility = DISABLED is answered
        // with 200 and the complete dto, never with 404, because the handler imposes no visibility
        // filter at any point (decision A).
        var commonService = new FakeEnterpriseCommonService
        {
            InfoResult = StoredEnterprise(visibility: "DISABLED")
        };

        var result = await EnterpriseHandlers.GetEnterpriseAsync(commonService, 7);

        var response = await TestHttp.ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.NotEqual(StatusCodes.Status404NotFound, response.StatusCode);

        var body = HandlerTestSupport.ReadJsonBody(response.Body);
        Assert.Equal(7, body.GetProperty("id").GetInt32());
        Assert.Equal("Talleres del Norte", body.GetProperty("commercialName").GetString());
        Assert.Equal("Tallernor", body.GetProperty("tradeName").GetString());
        Assert.Equal("DISABLED", body.GetProperty("visibility").GetString());

        // The handler did not turn the requested identifier into a visibility based lookup.
        Assert.Null(Assert.IsType<EnterpriseDTO>(commonService.LastRequestedDto).Visibility);
        AssertOnlyGetAsyncInfoWasCalled(commonService);
    }

    [Fact]
    public async Task UnknownEnterprise_IsTranslatedToNotFound()
    {
        // Error case CE-3 / RF-3.3 / RF-8.1: the repository reports an unknown identifier and the global
        // translation answers 404, the only way this endpoint answers 404.
        var commonService = new FakeEnterpriseCommonService
        {
            ExceptionToThrow = new KeyNotFoundException("No se encontró una empresa con el identificador solicitado.")
        };

        await HandlerTestSupport.AssertTranslationAsync<KeyNotFoundException>(
            () => EnterpriseHandlers.GetEnterpriseAsync(commonService, 404),
            StatusCodes.Status404NotFound);

        AssertOnlyGetAsyncInfoWasCalled(commonService);
    }

    [Fact]
    public async Task EntityRuleViolation_IsTranslatedToBadRequest()
    {
        // Error case RF-8.2: a domain rule violation reaches the handler as EntityException and the
        // global translation answers 400.
        var commonService = new FakeEnterpriseCommonService
        {
            ExceptionToThrow = new EntityException("El identificador debe ser un número mayor que cero.")
        };

        await HandlerTestSupport.AssertTranslationAsync<EntityException>(
            () => EnterpriseHandlers.GetEnterpriseAsync(commonService, 0),
            StatusCodes.Status400BadRequest);

        AssertOnlyGetAsyncInfoWasCalled(commonService);
    }

    [Fact]
    public async Task BusinessFailure_IsTranslatedToBadRequest()
    {
        // Error case RF-8.3 / RF-8.4: a business failure of the application (for instance an identifier
        // below one) arrives as HexArch ApplicationException and the global translation answers 400.
        var commonService = new FakeEnterpriseCommonService
        {
            ExceptionToThrow = new HexArchApplicationException("El identificador debe ser un número mayor que cero.")
        };

        await HandlerTestSupport.AssertTranslationAsync<HexArchApplicationException>(
            () => EnterpriseHandlers.GetEnterpriseAsync(commonService, 0),
            StatusCodes.Status400BadRequest);

        AssertOnlyGetAsyncInfoWasCalled(commonService);
    }

    [Fact]
    public async Task AnyOtherBusinessFailure_IsTranslatedToBadRequest()
    {
        // Error case RF-8.4: any other business cause is translated to 400 by the safe fallback of the
        // global handler, so the endpoint never leaks a 500 for it.
        var commonService = new FakeEnterpriseCommonService
        {
            ExceptionToThrow = new Exception("No se pudo consultar la información de la empresa.")
        };

        await HandlerTestSupport.AssertTranslationAsync<Exception>(
            () => EnterpriseHandlers.GetEnterpriseAsync(commonService, 7),
            StatusCodes.Status400BadRequest);

        AssertOnlyGetAsyncInfoWasCalled(commonService);
    }

    [Fact]
    public async Task RecordNotFound_DeliversTheMessageInSpanish()
    {
        // Error case CE-16 / RF-8.6 on the 404 path: ApiExceptionHandler writes exception.Message
        // verbatim, so the message the consumer reads has to arrive written in Spanish.
        var commonService = new FakeEnterpriseCommonService
        {
            ExceptionToThrow = new KeyNotFoundException("No se encontró una empresa con el identificador solicitado.")
        };

        var exception = await Assert.ThrowsAnyAsync<KeyNotFoundException>(
            () => EnterpriseHandlers.GetEnterpriseAsync(commonService, 404));
        var response = await TestHttp.TranslateAsync(exception);

        Assert.Equal(StatusCodes.Status404NotFound, response.StatusCode);
        Assert.Equal("No se encontró una empresa con el identificador solicitado.", response.Body);
    }

    [Fact]
    public async Task BusinessFailure_DeliversMessageInSpanish()
    {
        // Error case CE-16 / RF-8.6 on the 400 path.
        var commonService = new FakeEnterpriseCommonService
        {
            ExceptionToThrow = new HexArchApplicationException("El identificador debe ser un número mayor que cero.")
        };

        var exception = await Assert.ThrowsAnyAsync<HexArchApplicationException>(
            () => EnterpriseHandlers.GetEnterpriseAsync(commonService, 0));
        var response = await TestHttp.TranslateAsync(exception);

        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Equal("El identificador debe ser un número mayor que cero.", response.Body);
    }
}