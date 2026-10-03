using HexArch.Application.DTOs;
using HexArch.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using SosMVP.Handlers;
using test.Fakes;
using test.Support;

namespace test;

/// <summary>
/// Subject: <c>TypeHandlers.GetTypeAsync</c> with a fake <c>ICommonService&lt;TypeDTO&gt;</c>.
/// Covers the three cases the plan asks for: happy path, boundary case and error case
/// (RF-3.1 to RF-3.5, RF-8.1, RF-8.2, CE-1, CE-3, CE-10, CE-13).
/// </summary>
public class TypeHandlersGetTypeTests
{
    private static TypeDTO StoredType(int id = 7, string type = "Herramientas especiales", string? visibility = "ENABLED")
    {
        return new TypeDTO
        {
            Id = id,
            Type = type,
            Visibility = visibility
        };
    }

    [Fact]
    public async Task ExistingType_AnswersOkWithTheCompleteDtoReturnedByTheUseCase()
    {
        // Happy path: an existing identifier answers 200 with the complete TypeDTO (RF-3.2).
        var commonService = new FakeTypeCommonService { InfoResult = StoredType() };

        var result = await TypeHandlers.GetTypeAsync(commonService, 7);

        var response = await TestHttp.ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);

        var body = HandlerTestSupport.ReadJsonBody(response.Body);
        Assert.Equal(7, body.GetProperty("id").GetInt32());
        Assert.Equal("Herramientas especiales", body.GetProperty("type").GetString());
        Assert.Equal("ENABLED", body.GetProperty("visibility").GetString());
        Assert.Equal(1, commonService.GetAsyncInfoCalls);
    }

    [Fact]
    public async Task RouteId_ReachesTheUseCaseAsTheRequestedId()
    {
        // RF-3.1: the route parameter is the only source of the identifier handed to the use case.
        var commonService = new FakeTypeCommonService { InfoResult = StoredType() };

        await TypeHandlers.GetTypeAsync(commonService, 7);

        var requested = Assert.IsType<TypeDTO>(commonService.LastRequestedDto);
        Assert.Equal(7, requested.Id);
    }

    [Fact]
    public async Task OnlyTheRouteIdReachesTheUseCase()
    {
        // The route carries no body, so the request dto cannot carry Type nor Visibility (RF-3.1, CE-1).
        var commonService = new FakeTypeCommonService { InfoResult = StoredType() };

        await TypeHandlers.GetTypeAsync(commonService, 7);

        var requested = Assert.IsType<TypeDTO>(commonService.LastRequestedDto);
        Assert.Equal(7, requested.Id);
        Assert.Null(requested.Type);
        Assert.Null(requested.Visibility);
    }

    [Fact]
    public async Task DisabledType_AnswersOkWithTheCompleteDtoAndIsNotFiltered()
    {
        // Boundary case: an existing type with Visibility = DISABLED is answered with 200, never
        // with 404, and the handler does not filter by visibility at any point
        // (RF-3.5, CE-10, decision A).
        var commonService = new FakeTypeCommonService
        {
            InfoResult = StoredType(visibility: "DISABLED")
        };

        var result = await TypeHandlers.GetTypeAsync(commonService, 7);

        var response = await TestHttp.ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);

        var body = HandlerTestSupport.ReadJsonBody(response.Body);
        Assert.Equal(7, body.GetProperty("id").GetInt32());
        Assert.Equal("Herramientas especiales", body.GetProperty("type").GetString());
        Assert.Equal("DISABLED", body.GetProperty("visibility").GetString());
        Assert.Equal(1, commonService.GetAsyncInfoCalls);
    }

    [Fact]
    public async Task ResultDtoId_DoesNotChangeTheIdPropagatedToTheUseCase()
    {
        // Boundary case (CE-1): whatever Id the returned dto carries, the identifier that reached
        // the use case is exactly the route one.
        var commonService = new FakeTypeCommonService { InfoResult = StoredType(id: 999) };

        var result = await TypeHandlers.GetTypeAsync(commonService, 7);

        var response = await TestHttp.ExecuteAsync(result);
        var requested = Assert.IsType<TypeDTO>(commonService.LastRequestedDto);

        Assert.Equal(7, requested.Id);
        Assert.Equal(999, HandlerTestSupport.ReadJsonBody(response.Body).GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task UnknownType_IsTranslatedToNotFound()
    {
        // Error case: the repository reports an unknown identifier and the global translation
        // answers 404 (RF-3.3, RF-8.1, CE-3).
        var commonService = new FakeTypeCommonService
        {
            ExceptionToThrow = new KeyNotFoundException("No se encontró un tipo con el identificador solicitado.")
        };

        await HandlerTestSupport.AssertTranslationAsync<KeyNotFoundException>(
            () => TypeHandlers.GetTypeAsync(commonService, 404),
            StatusCodes.Status404NotFound);

        Assert.Equal(1, commonService.GetAsyncInfoCalls);
    }

    [Fact]
    public async Task RecordNotFound_DeliversTheMessageInSpanish()
    {
        var commonService = new FakeTypeCommonService
        {
            ExceptionToThrow = new KeyNotFoundException("No se encontró un tipo con el identificador solicitado.")
        };

        var exception = await Assert.ThrowsAnyAsync<KeyNotFoundException>(
            () => TypeHandlers.GetTypeAsync(commonService, 404));
        var response = await TestHttp.TranslateAsync(exception);

        Assert.Equal(StatusCodes.Status404NotFound, response.StatusCode);
        Assert.Equal("No se encontró un tipo con el identificador solicitado.", response.Body);
    }

    [Fact]
    public async Task EntityRuleViolation_IsTranslatedToBadRequest()
    {
        // Error case: a domain rule violation reaches the handler as EntityException and the
        // global translation answers 400 (RF-8.2).
        var commonService = new FakeTypeCommonService
        {
            ExceptionToThrow = new EntityException("El identificador debe ser un número mayor que cero.")
        };

        await HandlerTestSupport.AssertTranslationAsync<EntityException>(
            () => TypeHandlers.GetTypeAsync(commonService, 0),
            StatusCodes.Status400BadRequest);

        Assert.Equal(1, commonService.GetAsyncInfoCalls);
    }
}