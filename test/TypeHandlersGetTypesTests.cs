using System.Text.Json;
using HexArch.Application.DTOs;
using Microsoft.AspNetCore.Http;
using SosMVP.Handlers;
using test.Fakes;
using test.Support;

namespace test;

/// <summary>
/// Subject: <c>TypeHandlers.GetTypesAsync</c> with a fake <c>ICommonService&lt;TypeDTO&gt;</c>.
/// Covers the three cases the plan asks for: happy path, boundary case and error case
/// (RF-4.1 to RF-4.3, RF-8.4, CE-6, CE-6b).
/// </summary>
public class TypeHandlersGetTypesTests
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
    public async Task RegisteredTypes_AreAnsweredWithOkAndTheCollectionReturnedByTheUseCase()
    {
        // Happy path: every record returned by the use case reaches the response body untouched
        // (RF-4.1).
        var commonService = new FakeTypeCommonService
        {
            AllResult =
            [
                StoredType(1, "Herramientas especiales", "ENABLED"),
                StoredType(2, "Equipos de medición", "ENABLED")
            ]
        };

        var result = await TypeHandlers.GetTypesAsync(commonService);

        var response = await TestHttp.ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.Equal(1, commonService.GetAsyncAllInfoCalls);

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
    public async Task DisabledTypes_AreIncludedInTheResponseAndAreNotFiltered()
    {
        // Boundary case (RF-4.2, CE-6b): the listing includes the records with
        // Visibility = DISABLED, and the handler returns the collection as it came from the use
        // case, without filtering nor reordering it. This is the test that separates this spec
        // from the User one: disabled records ARE part of the response.
        var commonService = new FakeTypeCommonService
        {
            AllResult =
            [
                StoredType(1, "Herramientas especiales", "ENABLED"),
                StoredType(2, "Equipos de medición", "DISABLED"),
                StoredType(3, "Repuestos originales", "DISABLED")
            ]
        };

        var result = await TypeHandlers.GetTypesAsync(commonService);

        var response = await TestHttp.ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.Equal(1, commonService.GetAsyncAllInfoCalls);

        var items = HandlerTestSupport.ReadJsonBody(response.Body).EnumerateArray().ToList();
        Assert.Equal(3, items.Count);

        var disabled = items.Where(item => item.GetProperty("visibility").GetString() == "DISABLED").ToList();
        Assert.Equal(2, disabled.Count);
        Assert.Equal(2, disabled[0].GetProperty("id").GetInt32());
        Assert.Equal("Equipos de medición", disabled[0].GetProperty("type").GetString());
        Assert.Equal(3, disabled[1].GetProperty("id").GetInt32());
        Assert.Equal("Repuestos originales", disabled[1].GetProperty("type").GetString());
    }

    [Fact]
    public async Task OnlyDisabledTypes_AreStillAnsweredWithOkAndTheWholeCollection()
    {
        // Boundary case (RF-4.2): a collection made only of DISABLED records is returned as is,
        // never reduced to an empty collection.
        var commonService = new FakeTypeCommonService
        {
            AllResult =
            [
                StoredType(4, "Herramientas obsoletas", "DISABLED")
            ]
        };

        var result = await TypeHandlers.GetTypesAsync(commonService);

        var response = await TestHttp.ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);

        var items = HandlerTestSupport.ReadJsonBody(response.Body).EnumerateArray().ToList();
        Assert.Single(items);
        Assert.Equal("DISABLED", items[0].GetProperty("visibility").GetString());
    }

    [Fact]
    public async Task NoRegisteredTypes_AreAnsweredWithOkAndAnEmptyCollection()
    {
        // Boundary case (RF-4.3, CE-6): with no registered type the response is 200 with an empty
        // collection, never 404.
        var commonService = new FakeTypeCommonService { AllResult = [] };

        var result = await TypeHandlers.GetTypesAsync(commonService);

        var response = await TestHttp.ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.Equal("[]", response.Body);
        Assert.Equal(JsonValueKind.Array, HandlerTestSupport.ReadJsonBody(response.Body).ValueKind);
        Assert.Equal(1, commonService.GetAsyncAllInfoCalls);
    }

    [Fact]
    public async Task Listing_AsksTheUseCaseOnceAndLeavesTheOtherPortsUntouched()
    {
        // The listing takes no dto nor parameter, so it reaches the use case through the read-all
        // port only: no write operation is invoked and nothing is filtered by the handler.
        var commonService = new FakeTypeCommonService
        {
            AllResult = [StoredType(1, "Herramientas especiales", "DISABLED")]
        };

        await TypeHandlers.GetTypesAsync(commonService);

        Assert.Equal(1, commonService.GetAsyncAllInfoCalls);
        Assert.Equal(0, commonService.AddAsyncInfoCalls);
        Assert.Equal(0, commonService.GetAsyncInfoCalls);
        Assert.Equal(0, commonService.UpdateAsyncInfoCalls);
        Assert.Equal(0, commonService.UpdateAsyncVisibilityCalls);
        Assert.Null(commonService.LastRequestedDto);
    }

    [Fact]
    public async Task GenericUseCaseFailure_IsTranslatedToBadRequest()
    {
        // Error case: the listing has no business failure of its own, so any other business cause
        // reported by the use case is translated to 400 (RF-8.4).
        var commonService = new FakeTypeCommonService
        {
            ExceptionToThrow = new InvalidOperationException("No fue posible consultar el catálogo de tipos.")
        };

        await HandlerTestSupport.AssertTranslationAsync<InvalidOperationException>(
            () => TypeHandlers.GetTypesAsync(commonService),
            StatusCodes.Status400BadRequest);

        Assert.Equal(1, commonService.GetAsyncAllInfoCalls);
    }

    [Fact]
    public async Task GenericUseCaseFailure_DeliversTheMessageInSpanish()
    {
        // The message produced by the global translation is written verbatim (RF-8.6, CE-15).
        var commonService = new FakeTypeCommonService
        {
            ExceptionToThrow = new InvalidOperationException("No fue posible consultar el catálogo de tipos.")
        };

        var exception = await Assert.ThrowsAnyAsync<InvalidOperationException>(
            () => TypeHandlers.GetTypesAsync(commonService));
        var response = await TestHttp.TranslateAsync(exception);

        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Equal("No fue posible consultar el catálogo de tipos.", response.Body);
    }
}