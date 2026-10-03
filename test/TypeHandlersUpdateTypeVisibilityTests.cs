using HexArch.Application.DTOs;
using Microsoft.AspNetCore.Http;
using SosMVP.Handlers;
using test.Fakes;
using test.Support;
using HexArchApplicationException = HexArch.Domain.Exceptions.ApplicationException;

namespace test;

/// <summary>
/// Subject: <c>TypeHandlers.UpdateTypeVisibilityAsync</c> with a fake <c>ICommonService&lt;TypeDTO&gt;</c>.
/// Covers the three cases the plan asks for: happy path, boundary case and error case
/// (RF-6.1 to RF-6.6, RF-8.1, RF-8.3, CE-1, CE-2, CE-12).
/// </summary>
public class TypeHandlersUpdateTypeVisibilityTests
{
    private static TypeDTO BodyWithAnotherId(string? visibility = "DISABLED", string? type = "Herramientas especiales")
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
        // Happy path: the use case UpdateAsyncVisibility is void, so the response is 204 with no
        // body at all (RF-6.5).
        var commonService = new FakeTypeCommonService();

        var result = await TypeHandlers.UpdateTypeVisibilityAsync(commonService, 7, BodyWithAnotherId());

        await HandlerTestSupport.AssertNoContentAsync(result);
    }

    [Fact]
    public async Task ExistingType_IsDelegatedOnceToTheUseCaseWithTheRouteIdAndTheBodyVisibility()
    {
        // Happy path: a single delegation to UpdateAsyncVisibility carrying the route id and the
        // Visibility of the body (RF-6.1, RF-6.2).
        var commonService = new FakeTypeCommonService();

        await TypeHandlers.UpdateTypeVisibilityAsync(commonService, 7, BodyWithAnotherId());

        Assert.Equal(1, commonService.UpdateAsyncVisibilityCalls);
        Assert.Equal(0, commonService.UpdateAsyncInfoCalls);
        Assert.Equal(0, commonService.AddAsyncInfoCalls);
        Assert.Equal(0, commonService.GetAsyncInfoCalls);
        Assert.Equal(0, commonService.GetAsyncAllInfoCalls);

        var received = Assert.IsType<TypeDTO>(commonService.LastVisibilityDto);
        Assert.Equal(7, received.Id);
        Assert.Equal("DISABLED", received.Visibility);
    }

    [Fact]
    public async Task EnabledVisibilityOfTheBody_IsTheValueHandedToTheUseCase()
    {
        // Happy path: enabling is the same call as disabling; the handler only copies the value
        // received and never rewrites it (RF-6.2).
        var commonService = new FakeTypeCommonService();

        var result = await TypeHandlers.UpdateTypeVisibilityAsync(commonService, 7, BodyWithAnotherId(visibility: "ENABLED"));

        await HandlerTestSupport.AssertNoContentAsync(result);
        Assert.Equal(1, commonService.UpdateAsyncVisibilityCalls);
        Assert.Equal("ENABLED", Assert.IsType<TypeDTO>(commonService.LastVisibilityDto).Visibility);
    }

    [Fact]
    public async Task TypeSentInTheBody_DoesNotReachTheUseCase()
    {
        // Boundary case (RF-6.2, CE-2): the handler projects the request into a new dto holding
        // only Id and Visibility, so the Type of the body is ignored and this endpoint never
        // updates the name of the record.
        var commonService = new FakeTypeCommonService();
        var body = BodyWithAnotherId();

        await TypeHandlers.UpdateTypeVisibilityAsync(commonService, 7, body);

        var received = Assert.IsType<TypeDTO>(commonService.LastVisibilityDto);
        Assert.NotSame(body, received);
        Assert.Equal(7, received.Id);
        Assert.Equal("DISABLED", received.Visibility);
        Assert.Null(received.Type);
        Assert.Equal(0, commonService.UpdateAsyncInfoCalls);
        Assert.Null(commonService.LastUpdatedDto);
    }

    [Fact]
    public async Task TypeOutsideTheAllowedLengthInTheBody_IsStillIgnored()
    {
        // Boundary case (RF-6.2, CE-2): a Type that would violate TypeEntity rules cannot reach the
        // mapper through this endpoint, because it is not projected at all.
        var commonService = new FakeTypeCommonService();

        var result = await TypeHandlers.UpdateTypeVisibilityAsync(commonService, 7, BodyWithAnotherId(type: "ABC"));

        await HandlerTestSupport.AssertNoContentAsync(result);
        Assert.Null(Assert.IsType<TypeDTO>(commonService.LastVisibilityDto).Type);
    }

    [Fact]
    public async Task RouteId_OverridesTheIdOfTheBody()
    {
        // Boundary case (CE-1, RF-6.1): the identifier that reaches the use case is exactly the
        // route one, whatever Id the body carried.
        var commonService = new FakeTypeCommonService();

        await TypeHandlers.UpdateTypeVisibilityAsync(commonService, 7, BodyWithAnotherId());

        Assert.Equal(7, Assert.IsType<TypeDTO>(commonService.LastVisibilityDto).Id);
    }

    [Fact]
    public async Task UnknownType_IsTranslatedToNotFound()
    {
        // Error case: the repository reports an unknown identifier and the global translation
        // answers 404 without any modification (RF-6.4, RF-8.1, CE-3).
        var commonService = new FakeTypeCommonService
        {
            ExceptionToThrow = new KeyNotFoundException("No se encontró un tipo con el identificador solicitado.")
        };

        await HandlerTestSupport.AssertTranslationAsync<KeyNotFoundException>(
            () => TypeHandlers.UpdateTypeVisibilityAsync(commonService, 404, BodyWithAnotherId()),
            StatusCodes.Status404NotFound);

        Assert.Equal(1, commonService.UpdateAsyncVisibilityCalls);
    }

    [Fact]
    public async Task VisibilityOutsideTheAllowedValues_IsTranslatedToBadRequest()
    {
        // Error case: the use case rejects a Visibility other than ENABLED or DISABLED with
        // ApplicationException; the handler lets it reach the global translation, which answers
        // 400 without any modification (RF-6.3, RF-8.3, CE-12).
        var commonService = new FakeTypeCommonService
        {
            ExceptionToThrow = new HexArchApplicationException("La visibilidad debe ser ENABLED o DISABLED.\n")
        };

        await HandlerTestSupport.AssertTranslationAsync<HexArchApplicationException>(
            () => TypeHandlers.UpdateTypeVisibilityAsync(commonService, 7, BodyWithAnotherId(visibility: "INVALID")),
            StatusCodes.Status400BadRequest);

        Assert.Equal(1, commonService.UpdateAsyncVisibilityCalls);
    }

    [Fact]
    public async Task NonPositiveRouteId_IsTranslatedToBadRequest()
    {
        // Error case: the use case rejects an Id lower than one with ApplicationException and the
        // global translation answers 400 (RF-6.6, RF-8.3).
        var commonService = new FakeTypeCommonService
        {
            ExceptionToThrow = new HexArchApplicationException("El identificador debe ser un número mayor que cero.\n")
        };

        await HandlerTestSupport.AssertTranslationAsync<HexArchApplicationException>(
            () => TypeHandlers.UpdateTypeVisibilityAsync(commonService, 0, BodyWithAnotherId()),
            StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task BusinessFailure_DeliversTheMessageInSpanish()
    {
        var commonService = new FakeTypeCommonService
        {
            ExceptionToThrow = new HexArchApplicationException("La visibilidad debe ser ENABLED o DISABLED.\n")
        };

        var exception = await Assert.ThrowsAnyAsync<HexArchApplicationException>(
            () => TypeHandlers.UpdateTypeVisibilityAsync(commonService, 7, BodyWithAnotherId(visibility: "INVALID")));
        var response = await TestHttp.TranslateAsync(exception);

        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Equal("La visibilidad debe ser ENABLED o DISABLED.\n", response.Body);
    }
}