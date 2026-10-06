using System.Security.Claims;
using HexArch.Application.DTOs;
using HexArch.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SosMVP.Handlers;
using SosMVP.Security;
using test.Fakes;
using test.Support;
using HexArchApplicationException = HexArch.Domain.Exceptions.ApplicationException;

namespace test;

/// <summary>
/// Subject: <c>BinnacleHandlers.InsertBinnacleAsync</c> with a fake <c>IBinnacleService</c>.
/// Covers RF-1.7, RF-2.1, RF-2.5, RF-2.6, RF-12.2, RF-12.5 and RF-12.6.
/// </summary>
public class BinnacleHandlersInsertBinnacleTests
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
            UserId = 99,
            ContactId = 5,
            DeviceId = 8,
            Service = "Mantenimiento preventivo de voltaje",
            Amount = 120.5f,
            CustomerSignature = "Firma del cliente"
        };
    }

    private static DbUpdateException ReferentialIntegrityViolation()
    {
        var foreignKeyViolation = new InvalidOperationException(
            "The INSERT statement conflicted with the FOREIGN KEY constraint \"FK_bitacora_contacto\".");

        return new DbUpdateException(
            "No se pudo registrar la bitácora porque el contacto indicado no existe.",
            foreignKeyViolation);
    }

    private static void AssertOnlyAddWasCalled(FakeBinnacleService service)
    {
        Assert.Equal(1, service.AddCalls);
        Assert.Equal(0, service.GetCalls);
        Assert.Equal(0, service.GetAllCalls);
        Assert.Equal(0, service.UpdateCalls);
        Assert.Equal(0, service.UpdateVisibilityCalls);
        Assert.Equal(0, service.FollowupPartialCalls);
        Assert.Equal(0, service.ResetActivitiesCalls);
        Assert.Equal(0, service.CancelBinnacleCalls);
        Assert.Equal(0, service.FinishBinnacleCalls);
    }

    [Fact]
    public async Task ValidBinnacle_IsAnsweredWithCreatedWithoutBodyAndWithoutLocation()
    {
        var service = new FakeBinnacleService();

        var result = await BinnacleHandlers.InsertBinnacleAsync(service, Session(42), ValidDto());

        await HandlerTestSupport.AssertCreatedWithoutLocationAsync(result);
        AssertOnlyAddWasCalled(service);
    }

    [Fact]
    public async Task SessionUserId_ReplacesTheUserIdSentInTheBody()
    {
        var service = new FakeBinnacleService();

        await BinnacleHandlers.InsertBinnacleAsync(service, Session(42), ValidDto());

        Assert.Equal(42, Assert.IsType<BinnacleDTO>(service.LastAddedDto).UserId);
    }

    [Fact]
    public async Task EveryOtherBodyProperty_ReachesTheUseCaseUnchanged()
    {
        var service = new FakeBinnacleService();

        await BinnacleHandlers.InsertBinnacleAsync(service, Session(42), ValidDto());

        var received = Assert.IsType<BinnacleDTO>(service.LastAddedDto);
        Assert.Equal(5, received.ContactId);
        Assert.Equal(8, received.DeviceId);
        Assert.Equal("Mantenimiento preventivo de voltaje", received.Service);
        Assert.Equal(120.5f, received.Amount);
        Assert.Equal("Firma del cliente", received.CustomerSignature);
    }

    [Fact]
    public async Task SessionWithoutUserIdClaim_IsAnsweredWithUnauthorizedAndTheUseCaseIsNotReached()
    {
        var service = new FakeBinnacleService();

        var result = await BinnacleHandlers.InsertBinnacleAsync(service, Session(null), ValidDto());

        var response = await TestHttp.ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status401Unauthorized, response.StatusCode);
        Assert.Equal("La sesión no contiene un identificador de usuario válido.", response.Body);
        Assert.Equal(0, service.TotalCalls);
    }

    [Fact]
    public async Task SessionWithANonNumericUserIdClaim_IsAnsweredWithUnauthorizedAndTheUseCaseIsNotReached()
    {
        var service = new FakeBinnacleService();

        var result = await BinnacleHandlers.InsertBinnacleAsync(service, SessionWithUnreadableUserId(), ValidDto());

        var response = await TestHttp.ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status401Unauthorized, response.StatusCode);
        Assert.Equal("La sesión no contiene un identificador de usuario válido.", response.Body);
        Assert.Equal(0, service.TotalCalls);
    }

    [Fact]
    public async Task BusinessRuleViolation_IsTranslatedToBadRequestAndNothingIsWritten()
    {
        var service = new FakeBinnacleService
        {
            ExceptionToThrow = new EntityException("El servicio debe tener al menos 15 caracteres.")
        };

        await HandlerTestSupport.AssertTranslationAsync<EntityException>(
            () => BinnacleHandlers.InsertBinnacleAsync(service, Session(42), ValidDto()),
            StatusCodes.Status400BadRequest);

        AssertOnlyAddWasCalled(service);
    }

    [Fact]
    public async Task ApplicationRuleViolation_IsTranslatedToBadRequest()
    {
        var service = new FakeBinnacleService
        {
            ExceptionToThrow = new HexArchApplicationException("La página y los elementos por página deben ser números mayores que cero.")
        };

        await HandlerTestSupport.AssertTranslationAsync<HexArchApplicationException>(
            () => BinnacleHandlers.InsertBinnacleAsync(service, Session(42), ValidDto()),
            StatusCodes.Status400BadRequest);

        AssertOnlyAddWasCalled(service);
    }

    [Fact]
    public async Task UnknownContactOrUser_IsTranslatedToInternalServerErrorAndIsNotRetried()
    {
        var service = new FakeBinnacleService { ExceptionToThrow = ReferentialIntegrityViolation() };

        await HandlerTestSupport.AssertTranslationAsync<DbUpdateException>(
            () => BinnacleHandlers.InsertBinnacleAsync(service, Session(42), ValidDto()),
            StatusCodes.Status500InternalServerError);

        AssertOnlyAddWasCalled(service);
    }

    [Fact]
    public async Task EntityRuleViolation_DeliversTheMessageInSpanish()
    {
        var service = new FakeBinnacleService
        {
            ExceptionToThrow = new EntityException("El servicio debe tener al menos 15 caracteres.")
        };

        var exception = await Assert.ThrowsAnyAsync<EntityException>(
            () => BinnacleHandlers.InsertBinnacleAsync(service, Session(42), ValidDto()));
        var response = await TestHttp.TranslateAsync(exception);

        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Equal("El servicio debe tener al menos 15 caracteres.", response.Body);
    }

    [Fact]
    public async Task ReferentialIntegrityViolation_DeliversTheMessageInSpanish()
    {
        var service = new FakeBinnacleService { ExceptionToThrow = ReferentialIntegrityViolation() };

        var exception = await Assert.ThrowsAnyAsync<DbUpdateException>(
            () => BinnacleHandlers.InsertBinnacleAsync(service, Session(42), ValidDto()));
        var response = await TestHttp.TranslateAsync(exception);

        Assert.Equal(StatusCodes.Status500InternalServerError, response.StatusCode);
        Assert.Equal("No se pudo registrar la bitácora porque el contacto indicado no existe.", response.Body);
    }
}
