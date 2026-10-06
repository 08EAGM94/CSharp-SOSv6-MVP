using HexArch.Application.DTOs;
using HexArch.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using SosMVP.Handlers;
using test.Fakes;
using test.Support;
using HexArchApplicationException = HexArch.Domain.Exceptions.ApplicationException;

namespace test;

/// <summary>
/// Subject: <c>BinnacleHandlers.UpdateBinnacleAsync</c> with a fake <c>IBinnacleService</c>.
/// Covers RF-6.1, RF-6.6, RF-12.1, RF-12.2, RF-12.3, RF-12.6, CE-1, CE-4, CE-12 and CE-25.
/// </summary>
public class BinnacleHandlersUpdateBinnacleTests
{
    private const string InvalidStatusMessage = "La visibilidad debe ser en proceso o falta confirmar o cancelado o finalizado.\n";

    private static BinnacleDTO ValidDto()
    {
        return new BinnacleDTO
        {
            Id = 99,
            UserId = 77,
            ContactId = 5,
            Status = "en proceso",
            Visibility = "ENABLED"
        };
    }

    private static void AssertOnlyUpdateWasCalled(FakeBinnacleService service)
    {
        Assert.Equal(1, service.UpdateCalls);
        Assert.Equal(0, service.AddCalls);
        Assert.Equal(0, service.GetCalls);
        Assert.Equal(0, service.GetAllCalls);
        Assert.Equal(0, service.UpdateVisibilityCalls);
        Assert.Equal(0, service.FollowupPartialCalls);
        Assert.Equal(0, service.ResetActivitiesCalls);
        Assert.Equal(0, service.CancelBinnacleCalls);
        Assert.Equal(0, service.FinishBinnacleCalls);
    }

    [Fact]
    public async Task ValidUpdate_IsAnsweredWithNoContentAndAnEmptyBody()
    {
        var service = new FakeBinnacleService();

        var result = await BinnacleHandlers.UpdateBinnacleAsync(service, 7, ValidDto());

        await HandlerTestSupport.AssertNoContentAsync(result);
        AssertOnlyUpdateWasCalled(service);
    }

    [Fact]
    public async Task RouteId_ReplacesTheIdSentInTheBody()
    {
        var service = new FakeBinnacleService();

        await BinnacleHandlers.UpdateBinnacleAsync(service, 7, ValidDto());

        Assert.Equal(7, Assert.IsType<BinnacleDTO>(service.LastUpdatedDto).Id);
    }

    [Fact]
    public async Task EveryOtherProperty_ReachesTheUseCaseUnchanged()
    {
        var service = new FakeBinnacleService();

        await BinnacleHandlers.UpdateBinnacleAsync(service, 7, ValidDto());

        var received = Assert.IsType<BinnacleDTO>(service.LastUpdatedDto);
        Assert.Equal(77, received.UserId);
        Assert.Equal(5, received.ContactId);
        Assert.Equal("en proceso", received.Status);
        Assert.Equal("ENABLED", received.Visibility);
    }

    [Fact]
    public async Task NullStatus_IsTranslatedToBadRequestAndNothingIsWritten()
    {
        var service = new FakeBinnacleService
        {
            ExceptionToThrow = new HexArchApplicationException(InvalidStatusMessage)
        };

        await HandlerTestSupport.AssertTranslationAsync<HexArchApplicationException>(
            () => BinnacleHandlers.UpdateBinnacleAsync(service, 7, ValidDto()),
            StatusCodes.Status400BadRequest);

        AssertOnlyUpdateWasCalled(service);
    }

    [Fact]
    public async Task UnknownStatus_IsTranslatedToBadRequestAndNothingIsWritten()
    {
        var service = new FakeBinnacleService
        {
            ExceptionToThrow = new HexArchApplicationException(InvalidStatusMessage)
        };

        await HandlerTestSupport.AssertTranslationAsync<HexArchApplicationException>(
            () => BinnacleHandlers.UpdateBinnacleAsync(service, 7, ValidDto()),
            StatusCodes.Status400BadRequest);

        AssertOnlyUpdateWasCalled(service);
    }

    [Fact]
    public async Task UnknownStatus_DeliversTheMessageInSpanish()
    {
        var service = new FakeBinnacleService
        {
            ExceptionToThrow = new HexArchApplicationException(InvalidStatusMessage)
        };

        var exception = await Assert.ThrowsAnyAsync<HexArchApplicationException>(
            () => BinnacleHandlers.UpdateBinnacleAsync(service, 7, ValidDto()));
        var response = await TestHttp.TranslateAsync(exception);

        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Equal(InvalidStatusMessage, response.Body);
    }

    [Fact]
    public async Task UnknownBinnacle_IsTranslatedToNotFoundAndNothingIsWritten()
    {
        var service = new FakeBinnacleService
        {
            ExceptionToThrow = new KeyNotFoundException("No se pudo actualizar la bitácora porque no existe una bitácora con ese identificador.")
        };

        await HandlerTestSupport.AssertTranslationAsync<KeyNotFoundException>(
            () => BinnacleHandlers.UpdateBinnacleAsync(service, 404, ValidDto()),
            StatusCodes.Status404NotFound);

        AssertOnlyUpdateWasCalled(service);
    }

    [Fact]
    public async Task RecordNotFound_DeliversTheMessageInSpanish()
    {
        var service = new FakeBinnacleService
        {
            ExceptionToThrow = new KeyNotFoundException("No se pudo actualizar la bitácora porque no existe una bitácora con ese identificador.")
        };

        var exception = await Assert.ThrowsAnyAsync<KeyNotFoundException>(
            () => BinnacleHandlers.UpdateBinnacleAsync(service, 404, ValidDto()));
        var response = await TestHttp.TranslateAsync(exception);

        Assert.Equal(StatusCodes.Status404NotFound, response.StatusCode);
        Assert.Equal("No se pudo actualizar la bitácora porque no existe una bitácora con ese identificador.", response.Body);
    }

    [Fact]
    public async Task RouteIdBelowOne_IsTranslatedToBadRequestAndNothingIsWritten()
    {
        var service = new FakeBinnacleService
        {
            ExceptionToThrow = new EntityException("El identificador debe ser un número mayor que cero.")
        };

        await HandlerTestSupport.AssertTranslationAsync<EntityException>(
            () => BinnacleHandlers.UpdateBinnacleAsync(service, 0, ValidDto()),
            StatusCodes.Status400BadRequest);

        AssertOnlyUpdateWasCalled(service);
    }

    [Fact]
    public async Task RouteIdBelowOne_DeliversTheMessageInSpanish()
    {
        var service = new FakeBinnacleService
        {
            ExceptionToThrow = new EntityException("El identificador debe ser un número mayor que cero.")
        };

        var exception = await Assert.ThrowsAnyAsync<EntityException>(
            () => BinnacleHandlers.UpdateBinnacleAsync(service, -1, ValidDto()));
        var response = await TestHttp.TranslateAsync(exception);

        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Equal("El identificador debe ser un número mayor que cero.", response.Body);
    }
}