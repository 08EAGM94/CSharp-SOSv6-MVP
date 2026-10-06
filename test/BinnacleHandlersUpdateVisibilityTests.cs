using HexArch.Application.DTOs;
using HexArch.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using SosMVP.Handlers;
using test.Fakes;
using test.Support;
using HexArchApplicationException = HexArch.Domain.Exceptions.ApplicationException;

namespace test;

/// <summary>
/// Subject: <c>BinnacleHandlers.UpdateBinnacleVisibilityAsync</c> with a fake <c>IBinnacleService</c>.
/// Covers RF-7.1, RF-7.2, RF-7.6, RF-12.1, RF-12.2, RF-12.3, RF-12.6, CE-1, CE-2, CE-4 and CE-13.
/// </summary>
public class BinnacleHandlersUpdateVisibilityTests
{
    private const string InvalidVisibilityMessage = "La visibilidad debe ser ENABLED o DISABLED.\n";

    private static BinnacleDTO DtoWithManyProperties(string? visibility)
    {
        return new BinnacleDTO
        {
            Id = 99,
            UserId = 77,
            ContactId = 5,
            Service = "Mantenimiento preventivo de voltaje",
            Status = "en proceso",
            Visibility = visibility
        };
    }

    private static void AssertOnlyUpdateVisibilityWasCalled(FakeBinnacleService service)
    {
        Assert.Equal(1, service.UpdateVisibilityCalls);
        Assert.Equal(0, service.AddCalls);
        Assert.Equal(0, service.GetCalls);
        Assert.Equal(0, service.GetAllCalls);
        Assert.Equal(0, service.UpdateCalls);
        Assert.Equal(0, service.FollowupPartialCalls);
        Assert.Equal(0, service.ResetActivitiesCalls);
        Assert.Equal(0, service.CancelBinnacleCalls);
        Assert.Equal(0, service.FinishBinnacleCalls);
    }

    [Fact]
    public async Task ValidVisibilityChange_IsAnsweredWithNoContentAndAnEmptyBody()
    {
        var service = new FakeBinnacleService();

        var result = await BinnacleHandlers.UpdateBinnacleVisibilityAsync(service, 7, DtoWithManyProperties("DISABLED"));

        await HandlerTestSupport.AssertNoContentAsync(result);
        AssertOnlyUpdateVisibilityWasCalled(service);
    }

    [Fact]
    public async Task OnlyIdAndVisibility_ReachTheUseCase()
    {
        var service = new FakeBinnacleService();

        await BinnacleHandlers.UpdateBinnacleVisibilityAsync(service, 7, DtoWithManyProperties("DISABLED"));

        var received = Assert.IsType<BinnacleDTO>(service.LastUpdatedVisibilityDto);
        Assert.Equal(7, received.Id);
        Assert.Equal("DISABLED", received.Visibility);
        Assert.Null(received.UserId);
        Assert.Null(received.ContactId);
        Assert.Null(received.Service);
        Assert.Null(received.Status);
    }

    [Fact]
    public async Task RouteId_ReplacesTheIdSentInTheBody()
    {
        var service = new FakeBinnacleService();

        await BinnacleHandlers.UpdateBinnacleVisibilityAsync(service, 7, DtoWithManyProperties("ENABLED"));

        Assert.Equal(7, Assert.IsType<BinnacleDTO>(service.LastUpdatedVisibilityDto).Id);
    }

    [Fact]
    public async Task UnknownVisibility_IsTranslatedToBadRequestAndNothingIsWritten()
    {
        var service = new FakeBinnacleService
        {
            ExceptionToThrow = new HexArchApplicationException(InvalidVisibilityMessage)
        };

        await HandlerTestSupport.AssertTranslationAsync<HexArchApplicationException>(
            () => BinnacleHandlers.UpdateBinnacleVisibilityAsync(service, 7, DtoWithManyProperties("HIDDEN")),
            StatusCodes.Status400BadRequest);

        AssertOnlyUpdateVisibilityWasCalled(service);
    }

    [Fact]
    public async Task UnknownVisibility_DeliversTheMessageInSpanish()
    {
        var service = new FakeBinnacleService
        {
            ExceptionToThrow = new HexArchApplicationException(InvalidVisibilityMessage)
        };

        var exception = await Assert.ThrowsAnyAsync<HexArchApplicationException>(
            () => BinnacleHandlers.UpdateBinnacleVisibilityAsync(service, 7, DtoWithManyProperties("HIDDEN")));
        var response = await TestHttp.TranslateAsync(exception);

        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Equal(InvalidVisibilityMessage, response.Body);
    }

    [Fact]
    public async Task UnknownBinnacle_IsTranslatedToNotFoundAndNothingIsWritten()
    {
        var service = new FakeBinnacleService
        {
            ExceptionToThrow = new KeyNotFoundException("No se pudo cambiar la visibilidad de la bitácora porque no existe una bitácora con ese identificador.")
        };

        await HandlerTestSupport.AssertTranslationAsync<KeyNotFoundException>(
            () => BinnacleHandlers.UpdateBinnacleVisibilityAsync(service, 404, DtoWithManyProperties("DISABLED")),
            StatusCodes.Status404NotFound);

        AssertOnlyUpdateVisibilityWasCalled(service);
    }

    [Fact]
    public async Task RecordNotFound_DeliversTheMessageInSpanish()
    {
        var service = new FakeBinnacleService
        {
            ExceptionToThrow = new KeyNotFoundException("No se pudo cambiar la visibilidad de la bitácora porque no existe una bitácora con ese identificador.")
        };

        var exception = await Assert.ThrowsAnyAsync<KeyNotFoundException>(
            () => BinnacleHandlers.UpdateBinnacleVisibilityAsync(service, 404, DtoWithManyProperties("DISABLED")));
        var response = await TestHttp.TranslateAsync(exception);

        Assert.Equal(StatusCodes.Status404NotFound, response.StatusCode);
        Assert.Equal("No se pudo cambiar la visibilidad de la bitácora porque no existe una bitácora con ese identificador.", response.Body);
    }

    [Fact]
    public async Task RouteIdBelowOne_IsTranslatedToBadRequestAndNothingIsWritten()
    {
        var service = new FakeBinnacleService
        {
            ExceptionToThrow = new EntityException("El identificador debe ser un número mayor que cero.")
        };

        await HandlerTestSupport.AssertTranslationAsync<EntityException>(
            () => BinnacleHandlers.UpdateBinnacleVisibilityAsync(service, 0, DtoWithManyProperties("DISABLED")),
            StatusCodes.Status400BadRequest);

        AssertOnlyUpdateVisibilityWasCalled(service);
    }

    [Fact]
    public async Task RouteIdBelowOne_DeliversTheMessageInSpanish()
    {
        var service = new FakeBinnacleService
        {
            ExceptionToThrow = new EntityException("El identificador debe ser un número mayor que cero.")
        };

        var exception = await Assert.ThrowsAnyAsync<EntityException>(
            () => BinnacleHandlers.UpdateBinnacleVisibilityAsync(service, -1, DtoWithManyProperties("DISABLED")));
        var response = await TestHttp.TranslateAsync(exception);

        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Equal("El identificador debe ser un número mayor que cero.", response.Body);
    }
}