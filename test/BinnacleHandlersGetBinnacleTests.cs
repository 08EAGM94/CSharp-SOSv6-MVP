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
/// Subject: <c>BinnacleHandlers.GetBinnacleAsync</c> with a fake <c>IBinnacleService</c>.
/// Covers RF-1.7, RF-3.1, RF-3.2, RF-3.3, RF-12.1, RF-12.2 and RF-12.6.
/// </summary>
public class BinnacleHandlersGetBinnacleTests
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

    private static BinnacleDTO StoredBinnacle(int id = 7)
    {
        return new BinnacleDTO
        {
            Id = id,
            UserId = 42,
            ContactId = 5,
            Service = "Mantenimiento preventivo de voltaje",
            Amount = 120.5f,
            Status = "en proceso",
            Visibility = "ENABLED"
        };
    }

    private static void AssertOnlyGetWasCalled(FakeBinnacleService service)
    {
        Assert.Equal(1, service.GetCalls);
        Assert.Equal(0, service.AddCalls);
        Assert.Equal(0, service.GetAllCalls);
        Assert.Equal(0, service.UpdateCalls);
        Assert.Equal(0, service.UpdateVisibilityCalls);
        Assert.Equal(0, service.FollowupPartialCalls);
        Assert.Equal(0, service.ResetActivitiesCalls);
        Assert.Equal(0, service.CancelBinnacleCalls);
        Assert.Equal(0, service.FinishBinnacleCalls);
    }

    [Fact]
    public async Task ExistingBinnacle_AnswersOkWithTheDtoReturnedByTheUseCase()
    {
        var service = new FakeBinnacleService { GetResult = StoredBinnacle() };

        var result = await BinnacleHandlers.GetBinnacleAsync(service, Session(42), 7);

        var response = await TestHttp.ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);

        var body = HandlerTestSupport.ReadJsonBody(response.Body);
        Assert.Equal(7, body.GetProperty("id").GetInt32());
        Assert.Equal(42, body.GetProperty("userId").GetInt32());
        Assert.Equal("Mantenimiento preventivo de voltaje", body.GetProperty("service").GetString());
        Assert.Equal("en proceso", body.GetProperty("status").GetString());

        AssertOnlyGetWasCalled(service);
    }

    [Fact]
    public async Task RouteIdAndSessionUser_ReachTheUseCaseAsTheOnlyAssignedValues()
    {
        var service = new FakeBinnacleService { GetResult = StoredBinnacle() };

        await BinnacleHandlers.GetBinnacleAsync(service, Session(42), 7);

        var requested = Assert.IsType<BinnacleDTO>(service.LastGetDto);
        Assert.Equal(7, requested.Id);
        Assert.Equal(42, requested.UserId);
        Assert.Null(requested.ContactId);
        Assert.Null(requested.Service);
        Assert.Null(requested.Status);
        Assert.Null(requested.Visibility);
    }

    [Fact]
    public async Task SessionWithoutUserIdClaim_IsAnsweredWithUnauthorizedAndTheUseCaseIsNotReached()
    {
        var service = new FakeBinnacleService { GetResult = StoredBinnacle() };

        var result = await BinnacleHandlers.GetBinnacleAsync(service, Session(null), 7);

        var response = await TestHttp.ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status401Unauthorized, response.StatusCode);
        Assert.Equal("La sesión no contiene un identificador de usuario válido.", response.Body);
        Assert.Equal(0, service.TotalCalls);
    }

    [Fact]
    public async Task SessionWithANonNumericUserIdClaim_IsAnsweredWithUnauthorizedAndTheUseCaseIsNotReached()
    {
        var service = new FakeBinnacleService { GetResult = StoredBinnacle() };

        var result = await BinnacleHandlers.GetBinnacleAsync(service, SessionWithUnreadableUserId(), 7);

        var response = await TestHttp.ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status401Unauthorized, response.StatusCode);
        Assert.Equal("La sesión no contiene un identificador de usuario válido.", response.Body);
        Assert.Equal(0, service.TotalCalls);
    }

    [Fact]
    public async Task UnknownBinnacle_IsTranslatedToNotFound()
    {
        var service = new FakeBinnacleService
        {
            ExceptionToThrow = new KeyNotFoundException("No se encontró una bitácora con la información solicitada.")
        };

        await HandlerTestSupport.AssertTranslationAsync<KeyNotFoundException>(
            () => BinnacleHandlers.GetBinnacleAsync(service, Session(42), 404),
            StatusCodes.Status404NotFound);

        AssertOnlyGetWasCalled(service);
    }

    [Fact]
    public async Task RecordNotFound_DeliversTheMessageInSpanish()
    {
        var service = new FakeBinnacleService
        {
            ExceptionToThrow = new KeyNotFoundException("No se encontró una bitácora con la información solicitada.")
        };

        var exception = await Assert.ThrowsAnyAsync<KeyNotFoundException>(
            () => BinnacleHandlers.GetBinnacleAsync(service, Session(42), 404));
        var response = await TestHttp.TranslateAsync(exception);

        Assert.Equal(StatusCodes.Status404NotFound, response.StatusCode);
        Assert.Equal("No se encontró una bitácora con la información solicitada.", response.Body);
    }

    [Fact]
    public async Task IdentifierOutsideTheAllowedRules_IsTranslatedToBadRequest()
    {
        var service = new FakeBinnacleService
        {
            ExceptionToThrow = new EntityException("El identificador debe ser un número mayor que cero.")
        };

        await HandlerTestSupport.AssertTranslationAsync<EntityException>(
            () => BinnacleHandlers.GetBinnacleAsync(service, Session(42), 0),
            StatusCodes.Status400BadRequest);

        AssertOnlyGetWasCalled(service);
    }

    [Fact]
    public async Task IdentifierOutsideTheAllowedRules_DeliversTheMessageInSpanish()
    {
        var service = new FakeBinnacleService
        {
            ExceptionToThrow = new EntityException("El identificador debe ser un número mayor que cero.")
        };

        var exception = await Assert.ThrowsAnyAsync<EntityException>(
            () => BinnacleHandlers.GetBinnacleAsync(service, Session(42), -1));
        var response = await TestHttp.TranslateAsync(exception);

        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Equal("El identificador debe ser un número mayor que cero.", response.Body);
    }
}
