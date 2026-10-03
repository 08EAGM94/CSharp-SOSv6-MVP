using HexArch.Application.DTOs;
using HexArch.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using SosMVP.Handlers;
using test.Fakes;
using test.Support;

namespace test;

/// <summary>
/// Subject: <c>TypeHandlers.UpdateTypeAsync</c> with a fake <c>ICommonService&lt;TypeDTO&gt;</c>.
/// Covers the three cases the plan asks for: happy path, boundary case and error case
/// (RF-5.1 to RF-5.5, RF-8.1, RF-8.2, CE-1, CE-5, CE-11).
/// </summary>
public class TypeHandlersUpdateTypeTests
{
    private static TypeDTO BodyWithAnotherId(string type = "Herramientas especiales", string? visibility = "ENABLED")
    {
        return new TypeDTO
        {
            Id = 99,
            Type = type,
            Visibility = visibility
        };
    }

    [Fact]
    public async Task ExistingType_IsAnsweredWithNoContentAndWithoutBody()
    {
        // Happy path: the use case UpdateAsyncInfo is void, so the response is 204 with no body
        // at all (RF-5.5).
        var commonService = new FakeTypeCommonService();

        var result = await TypeHandlers.UpdateTypeAsync(commonService, 7, BodyWithAnotherId());

        await HandlerTestSupport.AssertNoContentAsync(result);
    }

    [Fact]
    public async Task ExistingType_IsDelegatedOnceToTheUseCaseWithTheTypeOfTheBody()
    {
        // Happy path: a single delegation to UpdateAsyncInfo carrying the Type of the body
        // (RF-5.2).
        var commonService = new FakeTypeCommonService();
        var dto = BodyWithAnotherId();

        await TypeHandlers.UpdateTypeAsync(commonService, 7, dto);

        Assert.Equal(1, commonService.UpdateAsyncInfoCalls);
        Assert.Equal(0, commonService.UpdateAsyncVisibilityCalls);
        Assert.Equal(0, commonService.AddAsyncInfoCalls);
        Assert.Equal(0, commonService.GetAsyncInfoCalls);
        Assert.Equal(0, commonService.GetAsyncAllInfoCalls);

        var received = Assert.IsType<TypeDTO>(commonService.LastUpdatedDto);
        Assert.Equal("Herramientas especiales", received.Type);
    }

    [Fact]
    public async Task RouteId_OverridesTheIdOfTheBody()
    {
        // Boundary case (CE-1, RF-5.1): the identifier that reaches the use case is exactly the
        // route one, whatever Id the body carried.
        var commonService = new FakeTypeCommonService();

        await TypeHandlers.UpdateTypeAsync(commonService, 7, BodyWithAnotherId());

        var received = Assert.IsType<TypeDTO>(commonService.LastUpdatedDto);
        Assert.Equal(7, received.Id);
    }

    [Fact]
    public async Task TheRestOfTheBodyReachesTheUseCaseUnchanged()
    {
        // Boundary case (CE-1): only the Id is replaced. The handler does not project the request
        // into a new dto, so the remaining properties are never lost (RF-5.2).
        var commonService = new FakeTypeCommonService();
        var dto = BodyWithAnotherId(type: "Herramientas(rename)", visibility: "DISABLED");

        await TypeHandlers.UpdateTypeAsync(commonService, 7, dto);

        var received = Assert.IsType<TypeDTO>(commonService.LastUpdatedDto);
        Assert.Equal(7, received.Id);
        Assert.Equal(dto.Type, received.Type);
        Assert.Equal(dto.Visibility, received.Visibility);
    }

    [Fact]
    public async Task VisibilitySentInTheBody_IsNotRewrittenByTheHandler()
    {
        // RF-5.2 only updates the Type of the record; whether the incoming Visibility is honoured
        // belongs to the use case, so the handler neither validates nor rewrites it.
        var commonService = new FakeTypeCommonService();

        var result = await TypeHandlers.UpdateTypeAsync(commonService, 7, BodyWithAnotherId(visibility: "DISABLED"));

        await HandlerTestSupport.AssertNoContentAsync(result);
        Assert.Equal(1, commonService.UpdateAsyncInfoCalls);
        Assert.Equal("DISABLED", Assert.IsType<TypeDTO>(commonService.LastUpdatedDto).Visibility);
    }

    [Fact]
    public async Task UnknownType_IsTranslatedToNotFound()
    {
        // Error case: the repository reports an unknown identifier and the global translation
        // answers 404 without any modification (RF-5.3, RF-8.1, CE-5).
        var commonService = new FakeTypeCommonService
        {
            ExceptionToThrow = new KeyNotFoundException("No se encontró un tipo con el identificador solicitado.")
        };

        await HandlerTestSupport.AssertTranslationAsync<KeyNotFoundException>(
            () => TypeHandlers.UpdateTypeAsync(commonService, 404, BodyWithAnotherId()),
            StatusCodes.Status404NotFound);

        Assert.Equal(1, commonService.UpdateAsyncInfoCalls);
    }

    [Fact]
    public async Task RecordNotFound_DeliversTheMessageInSpanish()
    {
        var commonService = new FakeTypeCommonService
        {
            ExceptionToThrow = new KeyNotFoundException("No se encontró un tipo con el identificador solicitado.")
        };

        var exception = await Assert.ThrowsAnyAsync<KeyNotFoundException>(
            () => TypeHandlers.UpdateTypeAsync(commonService, 404, BodyWithAnotherId()));
        var response = await TestHttp.TranslateAsync(exception);

        Assert.Equal(StatusCodes.Status404NotFound, response.StatusCode);
        Assert.Equal("No se encontró un tipo con el identificador solicitado.", response.Body);
    }

    [Fact]
    public async Task TypeOutsideTheAllowedLength_IsTranslatedToBadRequest()
    {
        // Error case: a Type shorter than five characters violates TypeEntity rules, which the use
        // case reports as EntityException; the handler lets it reach the global translation, which
        // answers 400 without any modification (RF-5.4, RF-8.2, CE-11).
        var commonService = new FakeTypeCommonService
        {
            ExceptionToThrow = new EntityException("El tipo debe tener entre 5 y 50 caracteres.")
        };

        await HandlerTestSupport.AssertTranslationAsync<EntityException>(
            () => TypeHandlers.UpdateTypeAsync(commonService, 7, BodyWithAnotherId(type: "ABC")),
            StatusCodes.Status400BadRequest);

        Assert.Equal(1, commonService.UpdateAsyncInfoCalls);
    }

    [Fact]
    public async Task EntityRuleViolation_DeliversTheMessageInSpanish()
    {
        var commonService = new FakeTypeCommonService
        {
            ExceptionToThrow = new EntityException("El tipo debe tener entre 5 y 50 caracteres.")
        };

        var exception = await Assert.ThrowsAnyAsync<EntityException>(
            () => TypeHandlers.UpdateTypeAsync(commonService, 7, BodyWithAnotherId(type: "ABC")));
        var response = await TestHttp.TranslateAsync(exception);

        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Equal("El tipo debe tener entre 5 y 50 caracteres.", response.Body);
    }
}