using System.Security.Claims;
using HexArch.Application.DTOs;
using HexArch.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using SosMVP.Handlers;
using SosMVP.Security;
using test.Fakes;
using test.Support;
using HexArchApplicationException = HexArch.Domain.Exceptions.ApplicationException;

namespace test;

/// <summary>
/// Subject: <c>BinnacleHandlers.FollowupPartialAsync</c> with a fake <c>IBinnacleService</c>.
/// Covers RF-1.7, RF-8.1, RF-8.6, RF-12.1, RF-12.2, RF-12.4, RF-12.6, CE-4, CE-15 and CE-23.
/// </summary>
public class BinnacleHandlersFollowupPartialTests
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
            ActivitiesDone = "Actividades realizadas en campo",
            Hints = "Observaciones del operador en sitio",
            StartingDate = new DateOnly(2026, 10, 1)
        };
    }

    private static void AssertOnlyFollowupPartialWasCalled(FakeBinnacleService service)
    {
        Assert.Equal(1, service.FollowupPartialCalls);
        Assert.Equal(0, service.AddCalls);
        Assert.Equal(0, service.GetCalls);
        Assert.Equal(0, service.GetAllCalls);
        Assert.Equal(0, service.UpdateCalls);
        Assert.Equal(0, service.UpdateVisibilityCalls);
        Assert.Equal(0, service.ResetActivitiesCalls);
        Assert.Equal(0, service.CancelBinnacleCalls);
        Assert.Equal(0, service.FinishBinnacleCalls);
    }

    [Fact]
    public async Task ValidFollowup_IsAnsweredWithNoContentAndAnEmptyBody()
    {
        var service = new FakeBinnacleService();

        var result = await BinnacleHandlers.FollowupPartialAsync(service, Session(42), 7, ValidDto());

        await HandlerTestSupport.AssertNoContentAsync(result);
        AssertOnlyFollowupPartialWasCalled(service);
    }

    [Fact]
    public async Task RouteIdAndSessionUser_ReplaceTheValuesSentInTheBody()
    {
        var service = new FakeBinnacleService();

        await BinnacleHandlers.FollowupPartialAsync(service, Session(42), 7, ValidDto());

        var received = Assert.IsType<BinnacleDTO>(service.LastFollowupPartialDto);
        Assert.Equal(7, received.Id);
        Assert.Equal(42, received.UserId);
    }

    [Fact]
    public async Task EveryOtherBodyProperty_ReachesTheUseCaseUnchanged()
    {
        var service = new FakeBinnacleService();

        await BinnacleHandlers.FollowupPartialAsync(service, Session(42), 7, ValidDto());

        var received = Assert.IsType<BinnacleDTO>(service.LastFollowupPartialDto);
        Assert.Equal("Actividades realizadas en campo", received.ActivitiesDone);
        Assert.Equal("Observaciones del operador en sitio", received.Hints);
        Assert.Equal(new DateOnly(2026, 10, 1), received.StartingDate);
    }

    [Fact]
    public async Task SessionWithoutUserIdClaim_IsAnsweredWithUnauthorizedAndTheUseCaseIsNotReached()
    {
        var service = new FakeBinnacleService();

        var result = await BinnacleHandlers.FollowupPartialAsync(service, Session(null), 7, ValidDto());

        var response = await TestHttp.ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status401Unauthorized, response.StatusCode);
        Assert.Equal("La sesión no contiene un identificador de usuario válido.", response.Body);
        Assert.Equal(0, service.TotalCalls);
    }

    [Fact]
    public async Task SessionWithANonNumericUserIdClaim_IsAnsweredWithUnauthorizedAndTheUseCaseIsNotReached()
    {
        var service = new FakeBinnacleService();

        var result = await BinnacleHandlers.FollowupPartialAsync(service, SessionWithUnreadableUserId(), 7, ValidDto());

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
            ExceptionToThrow = new KeyNotFoundException("No se pudo registrar el seguimiento de la bitácora porque no existe una bitácora con ese identificador.")
        };

        await HandlerTestSupport.AssertTranslationAsync<KeyNotFoundException>(
            () => BinnacleHandlers.FollowupPartialAsync(service, Session(42), 404, ValidDto()),
            StatusCodes.Status404NotFound);

        AssertOnlyFollowupPartialWasCalled(service);
    }

    [Fact]
    public async Task RecordNotFound_DeliversTheMessageInSpanish()
    {
        var service = new FakeBinnacleService
        {
            ExceptionToThrow = new KeyNotFoundException("No se pudo registrar el seguimiento de la bitácora porque no existe una bitácora con ese identificador.")
        };

        var exception = await Assert.ThrowsAnyAsync<KeyNotFoundException>(
            () => BinnacleHandlers.FollowupPartialAsync(service, Session(42), 404, ValidDto()));
        var response = await TestHttp.TranslateAsync(exception);

        Assert.Equal(StatusCodes.Status404NotFound, response.StatusCode);
        Assert.Equal("No se pudo registrar el seguimiento de la bitácora porque no existe una bitácora con ese identificador.", response.Body);
    }

    [Fact]
    public async Task EntityRuleViolation_IsTranslatedToBadRequestAndNothingIsWritten()
    {
        var service = new FakeBinnacleService
        {
            ExceptionToThrow = new EntityException("Las actividades realizadas deben tener al menos 15 caracteres.")
        };

        await HandlerTestSupport.AssertTranslationAsync<EntityException>(
            () => BinnacleHandlers.FollowupPartialAsync(service, Session(42), 7, ValidDto()),
            StatusCodes.Status400BadRequest);

        AssertOnlyFollowupPartialWasCalled(service);
    }

    [Fact]
    public async Task EntityRuleViolation_DeliversTheMessageInSpanish()
    {
        var service = new FakeBinnacleService
        {
            ExceptionToThrow = new EntityException("Las actividades realizadas deben tener al menos 15 caracteres.")
        };

        var exception = await Assert.ThrowsAnyAsync<EntityException>(
            () => BinnacleHandlers.FollowupPartialAsync(service, Session(42), 7, ValidDto()));
        var response = await TestHttp.TranslateAsync(exception);

        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Equal("Las actividades realizadas deben tener al menos 15 caracteres.", response.Body);
    }

    [Fact]
    public async Task FollowupOnABinnacleThatIsNotInProgress_IsTranslatedToBadRequestAndNothingIsWritten()
    {
        var service = new FakeBinnacleService
        {
            ExceptionToThrow = new Exception("El acceso a esta bitácora está prohibido.")
        };

        await HandlerTestSupport.AssertTranslationAsync<Exception>(
            () => BinnacleHandlers.FollowupPartialAsync(service, Session(42), 7, ValidDto()),
            StatusCodes.Status400BadRequest);

        AssertOnlyFollowupPartialWasCalled(service);
    }

    [Fact]
    public async Task FollowupOnABinnacleThatIsNotInProgress_DeliversTheMessageInSpanish()
    {
        var service = new FakeBinnacleService
        {
            ExceptionToThrow = new Exception("El acceso a esta bitácora está prohibido.")
        };

        var exception = await Assert.ThrowsAnyAsync<Exception>(
            () => BinnacleHandlers.FollowupPartialAsync(service, Session(42), 7, ValidDto()));
        var response = await TestHttp.TranslateAsync(exception);

        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Equal("El acceso a esta bitácora está prohibido.", response.Body);
    }
}
