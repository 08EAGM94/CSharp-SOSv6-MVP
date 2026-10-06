using System.Security.Claims;
using HexArch.Application.DTOs;
using HexArch.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using SosMVP.Handlers;
using SosMVP.Security;
using test.Fakes;
using test.Support;

namespace test;

/// <summary>
/// Subject: <c>BinnacleHandlers.FinishBinnacleAsync</c> with a fake <c>IBinnacleService</c>.
/// Covers RF-1.7, RF-11.1, RF-11.6, RF-12.1, RF-12.2, RF-12.4, RF-12.6, CE-1, CE-4, CE-15 and CE-27.
/// </summary>
public class BinnacleHandlersFinishBinnacleTests
{
    private static ClaimsPrincipal Session(int? userId, string? role = "user")
    {
        return HandlerTestSupport.CreateSession(userId, role);
    }

    private static ClaimsPrincipal SessionWithUnreadableUserId()
    {
        return new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(JwtTokenFactory.IdClaimType, "not-a-number") },
            authenticationType: "TestSession"));
    }

    private static BinnacleDTO ValidDto()
    {
        return new BinnacleDTO
        {
            Id = 99,
            UserId = 77,
            CustomerSignature = "Firma digitalizada del cliente responsable"
        };
    }

    private static void AssertOnlyFinishWasCalled(FakeBinnacleService service)
    {
        Assert.Equal(1, service.FinishBinnacleCalls);
        Assert.Equal(0, service.AddCalls);
        Assert.Equal(0, service.GetCalls);
        Assert.Equal(0, service.GetAllCalls);
        Assert.Equal(0, service.UpdateCalls);
        Assert.Equal(0, service.UpdateVisibilityCalls);
        Assert.Equal(0, service.FollowupPartialCalls);
        Assert.Equal(0, service.ResetActivitiesCalls);
        Assert.Equal(0, service.CancelBinnacleCalls);
    }

    [Fact]
    public async Task ValidFinish_IsAnsweredWithNoContentAndAnEmptyBody()
    {
        var service = new FakeBinnacleService();

        var result = await BinnacleHandlers.FinishBinnacleAsync(service, Session(42), 7, ValidDto());

        await HandlerTestSupport.AssertNoContentAsync(result);
        AssertOnlyFinishWasCalled(service);
    }

    [Fact]
    public async Task RouteIdAndSessionUser_ReplaceTheValuesSentInTheBody()
    {
        var service = new FakeBinnacleService();

        await BinnacleHandlers.FinishBinnacleAsync(service, Session(42), 7, ValidDto());

        var received = Assert.IsType<BinnacleDTO>(service.LastFinishedDto);
        Assert.Equal(7, received.Id);
        Assert.Equal(42, received.UserId);
    }

    [Fact]
    public async Task CustomerSignature_ReachesTheUseCaseUnchanged()
    {
        var service = new FakeBinnacleService();

        await BinnacleHandlers.FinishBinnacleAsync(service, Session(42), 7, ValidDto());

        Assert.Equal(
            "Firma digitalizada del cliente responsable",
            Assert.IsType<BinnacleDTO>(service.LastFinishedDto).CustomerSignature);
    }

    [Fact]
    public async Task SessionWithoutUserIdClaim_IsAnsweredWithUnauthorizedAndTheUseCaseIsNotReached()
    {
        var service = new FakeBinnacleService();

        var result = await BinnacleHandlers.FinishBinnacleAsync(service, Session(null), 7, ValidDto());

        var response = await TestHttp.ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status401Unauthorized, response.StatusCode);
        Assert.Equal("La sesión no contiene un identificador de usuario válido.", response.Body);
        Assert.Equal(0, service.TotalCalls);
    }

    [Fact]
    public async Task SessionWithANonNumericUserIdClaim_IsAnsweredWithUnauthorizedAndTheUseCaseIsNotReached()
    {
        var service = new FakeBinnacleService();

        var result = await BinnacleHandlers.FinishBinnacleAsync(service, SessionWithUnreadableUserId(), 7, ValidDto());

        var response = await TestHttp.ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status401Unauthorized, response.StatusCode);
        Assert.Equal("La sesión no contiene un identificador de usuario válido.", response.Body);
        Assert.Equal(0, service.TotalCalls);
    }

    [Fact]
    public async Task UnknownBinnacle_IsTranslatedToNotFoundAndNothingIsWritten()
    {
        var service = new FakeBinnacleService
        {
            ExceptionToThrow = new KeyNotFoundException("No se pudo finalizar la bitácora porque no existe una bitácora con ese identificador.")
        };

        await HandlerTestSupport.AssertTranslationAsync<KeyNotFoundException>(
            () => BinnacleHandlers.FinishBinnacleAsync(service, Session(42), 404, ValidDto()),
            StatusCodes.Status404NotFound);

        AssertOnlyFinishWasCalled(service);
    }

    [Fact]
    public async Task RecordNotFound_DeliversTheMessageInSpanish()
    {
        var service = new FakeBinnacleService
        {
            ExceptionToThrow = new KeyNotFoundException("No se pudo finalizar la bitácora porque no existe una bitácora con ese identificador.")
        };

        var exception = await Assert.ThrowsAnyAsync<KeyNotFoundException>(
            () => BinnacleHandlers.FinishBinnacleAsync(service, Session(42), 404, ValidDto()));
        var response = await TestHttp.TranslateAsync(exception);

        Assert.Equal(StatusCodes.Status404NotFound, response.StatusCode);
        Assert.Equal("No se pudo finalizar la bitácora porque no existe una bitácora con ese identificador.", response.Body);
    }

    [Fact]
    public async Task SignatureLongerThanTwoHundredFiftyFiveCharacters_IsTranslatedToBadRequestAndNothingIsWritten()
    {
        var service = new FakeBinnacleService
        {
            ExceptionToThrow = new EntityException("La firma del cliente debe tener un máximo de 255 caracteres.")
        };

        await HandlerTestSupport.AssertTranslationAsync<EntityException>(
            () => BinnacleHandlers.FinishBinnacleAsync(service, Session(42), 7, ValidDto()),
            StatusCodes.Status400BadRequest);

        AssertOnlyFinishWasCalled(service);
    }

    [Fact]
    public async Task SignatureLongerThanTwoHundredFiftyFiveCharacters_DeliversTheMessageInSpanish()
    {
        var service = new FakeBinnacleService
        {
            ExceptionToThrow = new EntityException("La firma del cliente debe tener un máximo de 255 caracteres.")
        };

        var exception = await Assert.ThrowsAnyAsync<EntityException>(
            () => BinnacleHandlers.FinishBinnacleAsync(service, Session(42), 7, ValidDto()));
        var response = await TestHttp.TranslateAsync(exception);

        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Equal("La firma del cliente debe tener un máximo de 255 caracteres.", response.Body);
    }

    [Fact]
    public async Task FinishOnABinnacleInForbiddenStatus_IsTranslatedToBadRequestAndNothingIsWritten()
    {
        var service = new FakeBinnacleService
        {
            ExceptionToThrow = new Exception("El acceso a esta bitácora está prohibido.")
        };

        await HandlerTestSupport.AssertTranslationAsync<Exception>(
            () => BinnacleHandlers.FinishBinnacleAsync(service, Session(42), 7, ValidDto()),
            StatusCodes.Status400BadRequest);

        AssertOnlyFinishWasCalled(service);
    }

    [Fact]
    public async Task FinishOnABinnacleInForbiddenStatus_DeliversTheMessageInSpanish()
    {
        var service = new FakeBinnacleService
        {
            ExceptionToThrow = new Exception("El acceso a esta bitácora está prohibido.")
        };

        var exception = await Assert.ThrowsAnyAsync<Exception>(
            () => BinnacleHandlers.FinishBinnacleAsync(service, Session(42), 7, ValidDto()));
        var response = await TestHttp.TranslateAsync(exception);

        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Equal("El acceso a esta bitácora está prohibido.", response.Body);
    }
}