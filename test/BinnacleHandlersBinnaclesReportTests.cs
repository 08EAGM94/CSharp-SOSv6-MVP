using HexArch.Application.DTOs;
using Microsoft.AspNetCore.Http;
using SosMVP.Handlers;
using test.Fakes;
using test.Support;
using HexArchApplicationException = HexArch.Domain.Exceptions.ApplicationException;

namespace test;

/// <summary>
/// Subject: <c>BinnacleHandlers.BinnaclesReportAsync</c> with a fake <c>IBinnacleService</c>.
/// Covers RF-5.1, RF-5.3, RF-12.3 and RF-12.6.
/// </summary>
public class BinnacleHandlersBinnaclesReportTests
{
    private const string BinnaclesReportAction = "BinnaclesReport";
    private const string PaginationMessage = "La página y los elementos por página deben ser números mayores que cero.";

    private static PaginationResult StoredPage()
    {
        return new PaginationResult
        {
            Elements = [new BinnacleDTO { Id = 1, Visibility = "ENABLED" }],
            ActualPage = 1,
            TotalPages = 3,
            TotalRegisters = 20
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
        var request = new BinnaclesReportRequest(1, 5, new Dictionary<string, string> { { "visibility", "ENABLED" } });

        var result = await BinnacleHandlers.BinnaclesReportAsync(service, request);

        var response = await TestHttp.ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);

        var body = HandlerTestSupport.ReadJsonBody(response.Body);
        Assert.Equal(1, body.GetProperty("actualPage").GetInt32());
        Assert.Equal(3, body.GetProperty("totalPages").GetInt32());
        Assert.Equal(20, body.GetProperty("totalRegisters").GetInt32());
        Assert.Single(body.GetProperty("elements").EnumerateArray());

        AssertOnlyGetAllWasCalled(service);
    }

    [Fact]
    public async Task WrapperValues_AreDelegatedWithANullDtoAndTheBinnaclesReportAction()
    {
        var service = new FakeBinnacleService { GetAllResult = StoredPage() };
        var filter = new Dictionary<string, string> { { "visibility", "DISABLED" } };
        var request = new BinnaclesReportRequest(2, 10, filter);

        await BinnacleHandlers.BinnaclesReportAsync(service, request);

        Assert.Null(service.LastGetAllDto);
        Assert.Equal(2, service.LastGetAllPage);
        Assert.Equal(10, service.LastGetAllElemsKey);
        Assert.Same(filter, service.LastGetAllBinnFilter);
        Assert.Equal(BinnaclesReportAction, service.LastGetAllControllerAction);
        AssertOnlyGetAllWasCalled(service);
    }

    [Fact]
    public async Task PageBelowOne_IsTranslatedToBadRequestAndNothingIsListed()
    {
        var service = new FakeBinnacleService
        {
            ExceptionToThrow = new HexArchApplicationException(PaginationMessage)
        };
        var request = new BinnaclesReportRequest(0, 5, new Dictionary<string, string>());

        await HandlerTestSupport.AssertTranslationAsync<HexArchApplicationException>(
            () => BinnacleHandlers.BinnaclesReportAsync(service, request),
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
        var request = new BinnaclesReportRequest(1, 0, new Dictionary<string, string>());

        await HandlerTestSupport.AssertTranslationAsync<HexArchApplicationException>(
            () => BinnacleHandlers.BinnaclesReportAsync(service, request),
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
        var request = new BinnaclesReportRequest(0, 5, new Dictionary<string, string>());

        var exception = await Assert.ThrowsAnyAsync<HexArchApplicationException>(
            () => BinnacleHandlers.BinnaclesReportAsync(service, request));
        var response = await TestHttp.TranslateAsync(exception);

        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Equal("La página y los elementos por página deben ser números mayores que cero.", response.Body);
    }
}
