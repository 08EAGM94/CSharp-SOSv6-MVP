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
/// Subject: <c>BinnacleHandlers.ResetActivitiesAsync</c> with a fake <c>IBinnacleService</c>.
/// Covers RF-1.7, RF-9.1, RF-9.6, RF-12.1, RF-12.2, RF-12.4, RF-12.6, CE-3, CE-4 and CE-14.
/// </summary>
public class BinnacleHandlersResetActivitiesTests
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

    private static void AssertOnlyResetWasCalled(FakeBinnacleService service)
    {
        Assert.Equal(1, service.ResetActivitiesCalls);
        Assert.Equal(0, service.AddCalls);
        Assert.Equal(0, service.GetCalls);
        Assert.Equal(0, service.GetAllCalls);
        Assert.Equal(0, service.UpdateCalls);
        Assert.Equal(0, service.UpdateVisibilityCalls);
        Assert.Equal(0, service.FollowupPartialCalls);
        Assert.Equal(0, service.CancelBinnacleCalls);
        Assert.Equal(0, service.FinishBinnacleCalls);
    }

    [Fact]
    public async Task ValidReset_IsAnsweredWithNoContentAndAnEmptyBody()
    {
        var service = new FakeBinnacleService();

        var result = await BinnacleHandlers.ResetActivitiesAsync(service, Session(42), 7);

        await HandlerTestSupport.AssertNoContentAsync(result);
        AssertOnlyResetWasCalled(service);
    }

    [Fact]
    public async Task GeneratedDto_CarriesOnlyTheRouteIdAndTheSessionUser()
    {
        var service = new FakeBinnacleService();

        await BinnacleHandlers.ResetActivitiesAsync(service, Session(42), 7);

        var received = Assert.IsType<BinnacleDTO>(service.LastResetActivitiesDto);
        Assert.Equal(7, received.Id);
        Assert.Equal(42, received.UserId);
        Assert.Null(received.ContactId);
        Assert.Null(received.Service);
        Assert.Null(received.ActivitiesDone);
        Assert.Null(received.Hints);
        Assert.Null(received.Status);
        Assert.Null(received.Visibility);
        Assert.Null(received.CancelDesc);
        Assert.Null(received.CustomerSignature);
    }

    [Fact]
    public async Task SessionWithoutUserIdClaim_IsAnsweredWithUnauthorizedAndTheUseCaseIsNotReached()
    {
        var service = new FakeBinnacleService();

        var result = await BinnacleHandlers.ResetActivitiesAsync(service, Session(null), 7);

        var response = await TestHttp.ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status401Unauthorized, response.StatusCode);
        Assert.Equal("La sesión no contiene un identificador de usuario válido.", response.Body);
        Assert.Equal(0, service.TotalCalls);
    }

    [Fact]
    public async Task SessionWithANonNumericUserIdClaim_IsAnsweredWithUnauthorizedAndTheUseCaseIsNotReached()
    {
        var service = new FakeBinnacleService();

        var result = await BinnacleHandlers.ResetActivitiesAsync(service, SessionWithUnreadableUserId(), 7);

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
            ExceptionToThrow = new KeyNotFoundException("No se pudieron reiniciar las actividades de la bitácora porque no existe una bitácora con ese identificador.")
        };

        await HandlerTestSupport.AssertTranslationAsync<KeyNotFoundException>(
            () => BinnacleHandlers.ResetActivitiesAsync(service, Session(42), 404),
            StatusCodes.Status404NotFound);

        AssertOnlyResetWasCalled(service);
    }

    [Fact]
    public async Task RecordNotFound_DeliversTheMessageInSpanish()
    {
        var service = new FakeBinnacleService
        {
            ExceptionToThrow = new KeyNotFoundException("No se pudieron reiniciar las actividades de la bitácora porque no existe una bitácora con ese identificador.")
        };

        var exception = await Assert.ThrowsAnyAsync<KeyNotFoundException>(
            () => BinnacleHandlers.ResetActivitiesAsync(service, Session(42), 404));
        var response = await TestHttp.TranslateAsync(exception);

        Assert.Equal(StatusCodes.Status404NotFound, response.StatusCode);
        Assert.Equal("No se pudieron reiniciar las actividades de la bitácora porque no existe una bitácora con ese identificador.", response.Body);
    }

    [Fact]
    public async Task IdentifierOutsideTheAllowedRules_IsTranslatedToBadRequestAndNothingIsWritten()
    {
        var service = new FakeBinnacleService
        {
            ExceptionToThrow = new EntityException("El identificador debe ser un número mayor que cero.")
        };

        await HandlerTestSupport.AssertTranslationAsync<EntityException>(
            () => BinnacleHandlers.ResetActivitiesAsync(service, Session(42), 0),
            StatusCodes.Status400BadRequest);

        AssertOnlyResetWasCalled(service);
    }

    [Fact]
    public async Task IdentifierOutsideTheAllowedRules_DeliversTheMessageInSpanish()
    {
        var service = new FakeBinnacleService
        {
            ExceptionToThrow = new EntityException("El identificador debe ser un número mayor que cero.")
        };

        var exception = await Assert.ThrowsAnyAsync<EntityException>(
            () => BinnacleHandlers.ResetActivitiesAsync(service, Session(42), 0));
        var response = await TestHttp.TranslateAsync(exception);

        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Equal("El identificador debe ser un número mayor que cero.", response.Body);
    }

    [Fact]
    public async Task ResetOnACancelledOrFinishedBinnacle_IsTranslatedToBadRequestAndNothingIsWritten()
    {
        var service = new FakeBinnacleService
        {
            ExceptionToThrow = new Exception("El acceso a esta bitácora está prohibido porque ya fue cancelada o finalizada.")
        };

        await HandlerTestSupport.AssertTranslationAsync<Exception>(
            () => BinnacleHandlers.ResetActivitiesAsync(service, Session(42), 7),
            StatusCodes.Status400BadRequest);

        AssertOnlyResetWasCalled(service);
    }

    [Fact]
    public async Task ResetOnACancelledOrFinishedBinnacle_DeliversTheMessageInSpanish()
    {
        var service = new FakeBinnacleService
        {
            ExceptionToThrow = new Exception("El acceso a esta bitácora está prohibido porque ya fue cancelada o finalizada.")
        };

        var exception = await Assert.ThrowsAnyAsync<Exception>(
            () => BinnacleHandlers.ResetActivitiesAsync(service, Session(42), 7));
        var response = await TestHttp.TranslateAsync(exception);

        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Equal("El acceso a esta bitácora está prohibido porque ya fue cancelada o finalizada.", response.Body);
    }
}
