using System.Security.Claims;
using HexArch.Application.DTOs;
using Microsoft.AspNetCore.Http;
using SosMVP.Handlers;
using SosMVP.Security;
using test.Fakes;
using test.Support;
using HexArchApplicationException = HexArch.Domain.Exceptions.ApplicationException;

namespace test;

/// <summary>
/// Subject: <c>BinnacleHandlers.FollowupListAsync</c> with a fake <c>IBinnacleService</c>.
/// Covers RF-1.7, RF-4.1, RF-4.2, RF-4.4, RF-12.3 and RF-12.6.
/// </summary>
public class BinnacleHandlersFollowupListTests
{
    private const string FollowupListAction = "FollowupList";
    private const string PaginationMessage = "La página y los elementos por página deben ser números mayores que cero.";

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

    private static PaginationResult StoredPage()
    {
        return new PaginationResult
        {
            Elements = [new BinnacleDTO { Id = 1, UserId = 42, Service = "Mantenimiento preventivo de voltaje" }],
            ActualPage = 1,
            TotalPages = 2,
            TotalRegisters = 9
        };
    }

    private static void AssertOnlyGetAllWasCalled(FakeBinnacleService service)
    {
        Assert.Equal(1, service.GetAllCalls);
        Assert.Equal(0, service.AddCalls);
        Assert.Equal(0, service.GetCalls);
        Assert.Equal(0, service.UpdateCalls);
        Assert.Equal(0, service.UpdateVisibilityCalls);
        Assert.Equal(0, service.FollowupPartialCalls);
        Assert.Equal(0, service.ResetActivitiesCalls);
        Assert.Equal(0, service.CancelBinnacleCalls);
        Assert.Equal(0, service.FinishBinnacleCalls);
    }

    [Fact]
    public async Task ValidRequest_AnswersOkWithThePaginationResultOfTheUseCase()
    {
        var service = new FakeBinnacleService { GetAllResult = StoredPage() };

        var result = await BinnacleHandlers.FollowupListAsync(service, Session(42), 1, 5);

        var response = await TestHttp.ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);

        var body = HandlerTestSupport.ReadJsonBody(response.Body);
        Assert.Equal(1, body.GetProperty("actualPage").GetInt32());
        Assert.Equal(2, body.GetProperty("totalPages").GetInt32());
        Assert.Equal(9, body.GetProperty("totalRegisters").GetInt32());
        Assert.Single(body.GetProperty("elements").EnumerateArray());

        AssertOnlyGetAllWasCalled(service);
    }

    [Fact]
    public async Task SessionUserId_IsTheOnlyValueAssignedToTheDto()
    {
        var service = new FakeBinnacleService { GetAllResult = StoredPage() };

        await BinnacleHandlers.FollowupListAsync(service, Session(42), 1, 5);

        var requested = Assert.IsType<BinnacleDTO>(service.LastGetAllDto);
        Assert.Equal(42, requested.UserId);
        Assert.Null(requested.Id);
        Assert.Null(requested.ContactId);
        Assert.Null(requested.Service);
        Assert.Null(requested.Status);
        Assert.Null(requested.Visibility);
    }

    [Fact]
    public async Task PageElemsKeyAndTheFollowupListAction_AreDelegatedWithAnUnfilteredReport()
    {
        var service = new FakeBinnacleService { GetAllResult = StoredPage() };

        await BinnacleHandlers.FollowupListAsync(service, Session(42), 2, 5);

        Assert.Equal(2, service.LastGetAllPage);
        Assert.Equal(5, service.LastGetAllElemsKey);
        Assert.Equal(FollowupListAction, service.LastGetAllControllerAction);
        Assert.Null(service.LastGetAllBinnFilter);
        AssertOnlyGetAllWasCalled(service);
    }

    [Fact]
    public async Task SessionWithoutUserIdClaim_IsAnsweredWithUnauthorizedAndTheUseCaseIsNotReached()
    {
        var service = new FakeBinnacleService { GetAllResult = StoredPage() };

        var result = await BinnacleHandlers.FollowupListAsync(service, Session(null), 1, 5);

        var response = await TestHttp.ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status401Unauthorized, response.StatusCode);
        Assert.Equal("La sesión no contiene un identificador de usuario válido.", response.Body);
        Assert.Equal(0, service.TotalCalls);
    }

    [Fact]
    public async Task SessionWithANonNumericUserIdClaim_IsAnsweredWithUnauthorizedAndTheUseCaseIsNotReached()
    {
        var service = new FakeBinnacleService { GetAllResult = StoredPage() };

        var result = await BinnacleHandlers.FollowupListAsync(service, SessionWithUnreadableUserId(), 1, 5);

        var response = await TestHttp.ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status401Unauthorized, response.StatusCode);
        Assert.Equal("La sesión no contiene un identificador de usuario válido.", response.Body);
        Assert.Equal(0, service.TotalCalls);
    }

    [Fact]
    public async Task PageBelowOne_IsTranslatedToBadRequestAndNothingIsListed()
    {
        var service = new FakeBinnacleService
        {
            ExceptionToThrow = new HexArchApplicationException(PaginationMessage)
        };

        await HandlerTestSupport.AssertTranslationAsync<HexArchApplicationException>(
            () => BinnacleHandlers.FollowupListAsync(service, Session(42), 0, 5),
            StatusCodes.Status400BadRequest);

        AssertOnlyGetAllWasCalled(service);
    }

    [Fact]
    public async Task ElemsKeyBelowOne_IsTranslatedToBadRequest()
    {
        var service = new FakeBinnacleService
        {
            ExceptionToThrow = new HexArchApplicationException(PaginationMessage)
        };

        await HandlerTestSupport.AssertTranslationAsync<HexArchApplicationException>(
            () => BinnacleHandlers.FollowupListAsync(service, Session(42), 1, 0),
            StatusCodes.Status400BadRequest);

        AssertOnlyGetAllWasCalled(service);
    }

    [Fact]
    public async Task PaginationFailure_DeliversTheMessageInSpanish()
    {
        var service = new FakeBinnacleService
        {
            ExceptionToThrow = new HexArchApplicationException(PaginationMessage)
        };

        var exception = await Assert.ThrowsAnyAsync<HexArchApplicationException>(
            () => BinnacleHandlers.FollowupListAsync(service, Session(42), 0, 5));
        var response = await TestHttp.TranslateAsync(exception);

        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Equal("La página y los elementos por página deben ser números mayores que cero.", response.Body);
    }
}
