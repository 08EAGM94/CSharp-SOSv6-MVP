using System.Text.Json;
using HexArch.Application.DTOs;
using Microsoft.AspNetCore.Http;
using SosMVP.Handlers;
using test.Fakes;
using test.Support;
using HexArchApplicationException = HexArch.Domain.Exceptions.ApplicationException;

namespace test;

/// <summary>
/// Subject: <c>TypeHandlers.GetTypesForSelectsAsync</c> with a fake <c>ISelectService&lt;TypeDTO&gt;</c>.
/// Covers the three cases the plan asks for: happy path, boundary case and error case
/// (RF-7.1 to RF-7.3, RF-8.4, RF-8.6, CE-6, CE-6c).
/// </summary>
public class TypeHandlersGetTypesForSelectsTests
{
    private static TypeDTO StoredType(int id, string type, string visibility)
    {
        return new TypeDTO
        {
            Id = id,
            Type = type,
            Visibility = visibility
        };
    }

    [Fact]
    public async Task EnabledTypes_AreAnsweredWithOkAndTheCollectionReturnedByTheSelectService()
    {
        // Happy path: the listing is a pass-through, so the response body is exactly the collection
        // that GetAsyncInfoForSelects returned (RF-7.1).
        var selectService = new FakeTypeSelectService
        {
            SelectResult =
            [
                StoredType(1, "Herramientas especiales", "ENABLED"),
                StoredType(2, "Equipos de medición", "ENABLED")
            ]
        };

        var result = await TypeHandlers.GetTypesForSelectsAsync(selectService);

        var response = await TestHttp.ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.Equal(1, selectService.GetAsyncInfoForSelectsCalls);

        var items = HandlerTestSupport.ReadJsonBody(response.Body).EnumerateArray().ToList();
        Assert.Equal(2, items.Count);
        Assert.Equal(1, items[0].GetProperty("id").GetInt32());
        Assert.Equal("Herramientas especiales", items[0].GetProperty("type").GetString());
        Assert.Equal("ENABLED", items[0].GetProperty("visibility").GetString());
        Assert.Equal(2, items[1].GetProperty("id").GetInt32());
        Assert.Equal("Equipos de medición", items[1].GetProperty("type").GetString());
        Assert.Equal("ENABLED", items[1].GetProperty("visibility").GetString());
    }

    [Fact]
    public async Task Listing_AsksTheSelectServiceOnceAndLeavesTheCommonServicePortUntouched()
    {
        // Happy path, port check: this endpoint belongs to the select port of the use case, not to
        // the common one used by GetTypesAsync. The FakeTypeCommonService is only a witness here:
        // it is never handed to the handler, so every one of its counters stays at zero.
        var selectService = new FakeTypeSelectService
        {
            SelectResult = [StoredType(1, "Herramientas especiales", "ENABLED")]
        };
        var commonService = new FakeTypeCommonService();

        await TypeHandlers.GetTypesForSelectsAsync(selectService);

        Assert.Equal(1, selectService.GetAsyncInfoForSelectsCalls);

        Assert.Equal(0, commonService.GetAsyncAllInfoCalls);
        Assert.Equal(0, commonService.GetAsyncInfoCalls);
        Assert.Equal(0, commonService.AddAsyncInfoCalls);
        Assert.Equal(0, commonService.UpdateAsyncInfoCalls);
        Assert.Equal(0, commonService.UpdateAsyncVisibilityCalls);
        Assert.Null(commonService.LastRequestedDto);
        Assert.Null(commonService.LastUpdatedDto);
    }

    [Fact]
    public async Task EnabledCollection_ReachesTheResponseIntactWithoutFilteringNorAddingRecords()
    {
        // Boundary case (RF-7.1, CE-6c): the collection handed by the use case holds ENABLED
        // records only, and the handler returns it whole — same records, same order, nothing
        // removed and nothing added. This is the contrast with GetTypesAsync, where DISABLED
        // records are part of the response: here they are not, because the filter by ENABLED
        // happens downstream in TypeRepository.GetAsyncInfoForSelects and not in the handler.
        var selectService = new FakeTypeSelectService
        {
            SelectResult =
            [
                StoredType(1, "Herramientas especiales", "ENABLED"),
                StoredType(5, "Repuestos originales", "ENABLED"),
                StoredType(9, "Equipos de medición", "ENABLED")
            ]
        };

        var result = await TypeHandlers.GetTypesForSelectsAsync(selectService);

        var response = await TestHttp.ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.Equal(1, selectService.GetAsyncInfoForSelectsCalls);

        var items = HandlerTestSupport.ReadJsonBody(response.Body).EnumerateArray().ToList();
        Assert.Equal(3, items.Count);
        Assert.Equal([1, 5, 9], items.Select(item => item.GetProperty("id").GetInt32()).ToArray());
        Assert.All(items, item => Assert.Equal("ENABLED", item.GetProperty("visibility").GetString()));
    }

    [Fact]
    public async Task TheVisibilityFilterLivesInTheSelectRepositoryAndNotInTheHandler()
    {
        // Boundary case (RF-7.2, CE-6c): this test does not assert the response contract, it
        // asserts where that contract comes from. RF-7.2 is guaranteed by
        // TypeRepository.GetAsyncInfoForSelects (hexArch/repository, intocable by the
        // constitution), which already filtered Visibilidad == "ENABLED" in the database. Because
        // of that, the handler must not filter by visibility again: whatever the select port
        // returns reaches the body untouched, and no DISABLED record is removed here.
        var selectService = new FakeTypeSelectService
        {
            SelectResult =
            [
                StoredType(1, "Herramientas especiales", "ENABLED"),
                StoredType(2, "Equipos de medición", "DISABLED")
            ]
        };

        var result = await TypeHandlers.GetTypesForSelectsAsync(selectService);

        var response = await TestHttp.ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.Equal(1, selectService.GetAsyncInfoForSelectsCalls);

        var items = HandlerTestSupport.ReadJsonBody(response.Body).EnumerateArray().ToList();
        Assert.Equal(2, items.Count);
        Assert.Equal(2, items[1].GetProperty("id").GetInt32());
        Assert.Equal("DISABLED", items[1].GetProperty("visibility").GetString());
    }

    [Fact]
    public async Task StoredTypesThatAreAllDisabled_ProduceAnEmptyCollectionInTheResponse()
    {
        // Boundary case (RF-7.2, CE-6c): when the store only holds DISABLED records, the select
        // repository returns nothing, so the response is 200 with an empty collection: the
        // disabled records are absent from the answer without the handler discarding them.
        var selectService = new FakeTypeSelectService { SelectResult = [] };

        var result = await TypeHandlers.GetTypesForSelectsAsync(selectService);

        var response = await TestHttp.ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.Equal("[]", response.Body);
        Assert.Equal(JsonValueKind.Array, HandlerTestSupport.ReadJsonBody(response.Body).ValueKind);
        Assert.Equal(1, selectService.GetAsyncInfoForSelectsCalls);
    }

    [Fact]
    public async Task NoRegisteredTypes_AreAnsweredWithOkAndAnEmptyCollection()
    {
        // Boundary case (RF-7.3, CE-6): with no registered type the response is 200 with an empty
        // collection, never 404.
        var selectService = new FakeTypeSelectService { SelectResult = [] };

        var result = await TypeHandlers.GetTypesForSelectsAsync(selectService);

        var response = await TestHttp.ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.Equal("[]", response.Body);
    }

    [Fact]
    public async Task BusinessFailureOfTheUseCase_IsTranslatedToBadRequest()
    {
        // Error case: the select use case reports a business failure and the handler lets it reach
        // the centralized translation, which answers 400 without any modification (RF-8.3, RF-8.4).
        var selectService = new FakeTypeSelectService
        {
            ExceptionToThrow = new HexArchApplicationException("No fue posible consultar los tipos habilitados.\n")
        };

        await HandlerTestSupport.AssertTranslationAsync<HexArchApplicationException>(
            () => TypeHandlers.GetTypesForSelectsAsync(selectService),
            StatusCodes.Status400BadRequest);

        Assert.Equal(1, selectService.GetAsyncInfoForSelectsCalls);
    }

    [Fact]
    public async Task GenericUseCaseFailure_IsTranslatedToBadRequest()
    {
        // Error case: the listing has no business failure of its own, so any other business cause
        // reported by the use case is translated to 400 (RF-8.4).
        var selectService = new FakeTypeSelectService
        {
            ExceptionToThrow = new InvalidOperationException("No fue posible consultar los tipos habilitados.")
        };

        await HandlerTestSupport.AssertTranslationAsync<InvalidOperationException>(
            () => TypeHandlers.GetTypesForSelectsAsync(selectService),
            StatusCodes.Status400BadRequest);

        Assert.Equal(1, selectService.GetAsyncInfoForSelectsCalls);
    }

    [Fact]
    public async Task BusinessFailure_DeliversTheMessageInSpanish()
    {
        // The message produced by the global translation is written verbatim (RF-8.6, CE-15).
        var selectService = new FakeTypeSelectService
        {
            ExceptionToThrow = new HexArchApplicationException("No fue posible consultar los tipos habilitados.\n")
        };

        var exception = await Assert.ThrowsAnyAsync<HexArchApplicationException>(
            () => TypeHandlers.GetTypesForSelectsAsync(selectService));
        var response = await TestHttp.TranslateAsync(exception);

        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Equal("No fue posible consultar los tipos habilitados.\n", response.Body);
    }
}