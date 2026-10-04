using System.Text.Json;
using HexArch.Application.DTOs;
using Microsoft.AspNetCore.Http;
using SosMVP.Handlers;
using test.Fakes;
using test.Support;
using HexArchApplicationException = HexArch.Domain.Exceptions.ApplicationException;

namespace test;

/// <summary>
/// Subject: <c>EnterpriseHandlers.GetEnterprisesAsync</c> with a fake <c>ICommonService&lt;EnterpriseDTO&gt;</c>.
/// Covers the cases the plan asks for: happy path, boundary cases and error cases
/// (RF-4.1 to RF-4.4, RF-8.4, RF-8.6, CE-6, CE-6b, CE-16).
/// </summary>
/// <remarks>
/// The listing is a pass-through: <c>GetAsyncAllInfo</c> already decides in <c>EnterpriseRepository</c>
/// (hexArch/repository, untouchable by the constitution p5) which records are returned and which
/// columns are projected, so the handler must neither filter nor project anything. RF-4.2 and RF-4.4
/// are therefore verified from the outside: the handler must not discard the DISABLED records it
/// receives, must not add any, and must not clean, hide or fill the properties the use case did not
/// project.
/// </remarks>
public class EnterpriseHandlersGetEnterprisesTests
{
    /// <summary>
    /// Mirrors the projection of <c>EnterpriseRepository.GetAsyncAllInfo</c>: only Id, CommercialName
    /// and TradeName travel, so every other property of the dto arrives null (RF-4.4).
    /// </summary>
    private static EnterpriseDTO ListedEnterprise(
        int id,
        string commercialName,
        string tradeName,
        string? visibility = null)
    {
        return new EnterpriseDTO
        {
            Id = id,
            CommercialName = commercialName,
            TradeName = tradeName,
            Visibility = visibility
        };
    }

    /// <summary>
    /// A full dto, the shape a consumer would get from the single record lookup instead of from the
    /// listing: it lets the suite prove that the handler never projects the response by itself.
    /// </summary>
    private static EnterpriseDTO CompleteEnterprise(int id, string? visibility)
    {
        return new EnterpriseDTO
        {
            Id = id,
            CommercialName = "Talleres del Norte",
            TradeName = "Tallernor",
            StreetNumber = "123",
            BetweenStreets = "Calle Norte y Calle Sur",
            ContactingWith = "Sr. Ramírez",
            Phones = "5551234567",
            Schedule = "Lunes a viernes de 8 a 18",
            Atention = "Atención en taller",
            Neighborhood = "Colonia Centro",
            Location = "Ciudad de Prueba",
            Email = "contacto@tallernor.example",
            Visibility = visibility
        };
    }

    /// <summary>
    /// The listing must reach the use case through <c>GetAsyncAllInfo</c> only, exactly once, and
    /// must not touch any other member of the primary port (RF-4.1).
    /// </summary>
    private static void AssertOnlyGetAsyncAllInfoWasCalled(FakeEnterpriseCommonService commonService)
    {
        Assert.Equal(1, commonService.GetAsyncAllInfoCalls);
        Assert.Equal(0, commonService.AddAsyncInfoCalls);
        Assert.Equal(0, commonService.GetAsyncInfoCalls);
        Assert.Equal(0, commonService.UpdateAsyncInfoCalls);
        Assert.Equal(0, commonService.UpdateAsyncVisibilityCalls);
    }

    /// <summary>
    /// Every property the listing does not project has to reach the consumer as an explicit null: the
    /// handler neither fills it nor hides it from the json, so the consumer cannot assume it is
    /// present with a value (RF-4.4).
    /// </summary>
    private static void AssertNonProjectedPropertiesArriveNull(JsonElement item)
    {
        Assert.Equal(JsonValueKind.Null, item.GetProperty("streetNumber").ValueKind);
        Assert.Equal(JsonValueKind.Null, item.GetProperty("betweenStreets").ValueKind);
        Assert.Equal(JsonValueKind.Null, item.GetProperty("contactingWith").ValueKind);
        Assert.Equal(JsonValueKind.Null, item.GetProperty("phones").ValueKind);
        Assert.Equal(JsonValueKind.Null, item.GetProperty("schedule").ValueKind);
        Assert.Equal(JsonValueKind.Null, item.GetProperty("atention").ValueKind);
        Assert.Equal(JsonValueKind.Null, item.GetProperty("neighborhood").ValueKind);
        Assert.Equal(JsonValueKind.Null, item.GetProperty("location").ValueKind);
        Assert.Equal(JsonValueKind.Null, item.GetProperty("email").ValueKind);
    }

    [Fact]
    public async Task Enterprises_AreAnsweredWithOkAndTheCollectionReturnedByTheUseCase()
    {
        // Happy path RF-4.1: the response is 200 and the body is exactly the collection that
        // GetAsyncAllInfo returned, with the three projected properties of each record.
        var commonService = new FakeEnterpriseCommonService
        {
            AllResult =
            [
                ListedEnterprise(1, "Talleres del Norte", "Tallernor"),
                ListedEnterprise(2, "Servicios del Sur", "Surtec")
            ]
        };

        var result = await EnterpriseHandlers.GetEnterprisesAsync(commonService);

        var response = await TestHttp.ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        AssertOnlyGetAsyncAllInfoWasCalled(commonService);

        var items = HandlerTestSupport.ReadJsonBody(response.Body).EnumerateArray().ToList();
        Assert.Equal(2, items.Count);
        Assert.Equal(1, items[0].GetProperty("id").GetInt32());
        Assert.Equal("Talleres del Norte", items[0].GetProperty("commercialName").GetString());
        Assert.Equal("Tallernor", items[0].GetProperty("tradeName").GetString());
        Assert.Equal(2, items[1].GetProperty("id").GetInt32());
        Assert.Equal("Servicios del Sur", items[1].GetProperty("commercialName").GetString());
        Assert.Equal("Surtec", items[1].GetProperty("tradeName").GetString());
    }

    [Fact]
    public async Task CollectionFromTheUseCase_ReachesTheResponseIntactWithoutFilteringNorAdding()
    {
        // Boundary case RF-4.1 / CE-6b: this is the test that proves the handler neither filters nor
        // modifies. The collection is answered whole: same records, same order, nothing removed and
        // nothing added, and the source collection handed by the use case is not mutated either.
        var stored = new List<EnterpriseDTO>
        {
            ListedEnterprise(1, "Talleres del Norte", "Tallernor", "ENABLED"),
            ListedEnterprise(2, "Servicios del Sur", "Surtec", "DISABLED"),
            ListedEnterprise(3, "Transportes del Este", "Este Express", "DISABLED")
        };
        var commonService = new FakeEnterpriseCommonService { AllResult = stored };

        var result = await EnterpriseHandlers.GetEnterprisesAsync(commonService);

        var response = await TestHttp.ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.NotEqual(StatusCodes.Status404NotFound, response.StatusCode);
        AssertOnlyGetAsyncAllInfoWasCalled(commonService);

        var items = HandlerTestSupport.ReadJsonBody(response.Body).EnumerateArray().ToList();
        Assert.Equal(stored.Count, items.Count);
        Assert.Equal([1, 2, 3], items.Select(item => item.GetProperty("id").GetInt32()).ToArray());

        // The handler did not rewrite nor empty the collection it received from the use case.
        Assert.Equal(3, stored.Count);
        Assert.Equal("DISABLED", stored[1].Visibility);
        Assert.Equal("DISABLED", stored[2].Visibility);
    }

    [Fact]
    public async Task DisabledEnterprises_AreIncludedInTheResponseAndAreNotFiltered()
    {
        // Boundary case CE-6b / RF-4.2: the listing includes the records with Visibility = DISABLED.
        // This is the difference against the reduced listing for selects: here the disabled records
        // are part of the answer because the query does not filter by visibility, and the handler
        // adds no visibility filter of its own on top of it.
        var commonService = new FakeEnterpriseCommonService
        {
            AllResult =
            [
                ListedEnterprise(1, "Talleres del Norte", "Tallernor", "ENABLED"),
                ListedEnterprise(2, "Servicios del Sur", "Surtec", "DISABLED"),
                ListedEnterprise(3, "Transportes del Este", "Este Express", "DISABLED")
            ]
        };

        var result = await EnterpriseHandlers.GetEnterprisesAsync(commonService);

        var response = await TestHttp.ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.NotEqual(StatusCodes.Status404NotFound, response.StatusCode);
        AssertOnlyGetAsyncAllInfoWasCalled(commonService);

        var items = HandlerTestSupport.ReadJsonBody(response.Body).EnumerateArray().ToList();
        Assert.Equal(3, items.Count);

        var disabled = items.Where(item => item.GetProperty("visibility").GetString() == "DISABLED").ToList();
        Assert.Equal(2, disabled.Count);
        Assert.Equal(2, disabled[0].GetProperty("id").GetInt32());
        Assert.Equal("Servicios del Sur", disabled[0].GetProperty("commercialName").GetString());
        Assert.Equal("Surtec", disabled[0].GetProperty("tradeName").GetString());
        Assert.Equal(3, disabled[1].GetProperty("id").GetInt32());
        Assert.Equal("Transportes del Este", disabled[1].GetProperty("commercialName").GetString());
        Assert.Equal("Este Express", disabled[1].GetProperty("tradeName").GetString());
    }

    [Fact]
    public async Task OnlyDisabledEnterprises_AreStillAnsweredWithOkAndTheWholeCollection()
    {
        // Boundary case CE-6b / RF-4.2: a collection made only of DISABLED records is answered as it
        // is, never reduced to an empty collection, because the visibility decision belongs to the
        // query and not to the handler.
        var commonService = new FakeEnterpriseCommonService
        {
            AllResult =
            [
                ListedEnterprise(4, "Talleres Obsoletos", "Obsoleto", "DISABLED"),
                ListedEnterprise(5, "Servicios Retirados", "Retirado", "DISABLED")
            ]
        };

        var result = await EnterpriseHandlers.GetEnterprisesAsync(commonService);

        var response = await TestHttp.ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);

        var items = HandlerTestSupport.ReadJsonBody(response.Body).EnumerateArray().ToList();
        Assert.Equal(2, items.Count);
        Assert.All(items, item => Assert.Equal("DISABLED", item.GetProperty("visibility").GetString()));
        Assert.Equal([4, 5], items.Select(item => item.GetProperty("id").GetInt32()).ToArray());
    }

    [Fact]
    public async Task ListingWithoutVisibility_KeepsTheVisibilityKeyInTheResponseAndItArrivesNull()
    {
        // Risk 3 / RF-4.4: GetAsyncAllInfo does not project Visibility, so the dtos reach the handler
        // with Visibility = null. The handler neither fills that hole nor removes the property from
        // the json: the consumer reads "visibility": null and must not assume it is present with a
        // value. This is the observable half of the requirement, the other half (the projection
        // itself) living in the untouchable repository.
        var first = ListedEnterprise(1, "Talleres del Norte", "Tallernor");
        var second = ListedEnterprise(2, "Servicios del Sur", "Surtec");
        var commonService = new FakeEnterpriseCommonService { AllResult = [first, second] };

        var result = await EnterpriseHandlers.GetEnterprisesAsync(commonService);

        var response = await TestHttp.ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);

        var items = HandlerTestSupport.ReadJsonBody(response.Body).EnumerateArray().ToList();
        Assert.Equal(2, items.Count);

        foreach (var item in items)
        {
            Assert.True(item.TryGetProperty("visibility", out var visibility));
            Assert.Equal(JsonValueKind.Null, visibility.ValueKind);

            // The projected three carry their value and the ten that are not projected arrive null.
            Assert.NotEqual(JsonValueKind.Null, item.GetProperty("id").ValueKind);
            Assert.NotEqual(JsonValueKind.Null, item.GetProperty("commercialName").ValueKind);
            Assert.NotEqual(JsonValueKind.Null, item.GetProperty("tradeName").ValueKind);
            AssertNonProjectedPropertiesArriveNull(item);
        }

        Assert.Equal("Tallernor", items[0].GetProperty("tradeName").GetString());

        // The handler did not fill the missing visibility in the dto instances either.
        Assert.Null(first.Visibility);
        Assert.Null(second.Visibility);
    }

    [Theory]
    [InlineData("ENABLED")]
    [InlineData("DISABLED")]
    public async Task VisibilityReportedByTheUseCase_ReachesTheResponseVerbatimWithoutBeingRemovedOrRewritten(
        string visibility)
    {
        // Risk 3 / RF-4.4 from the other side: when the use case does report a visibility, the handler
        // neither strips it from the json nor rewrites its value. Cleaning the response here (for
        // instance projecting every record into Id/CommercialName/TradeName) would silently drop a
        // property that belongs to the query, so this test pins the pass-through.
        var commonService = new FakeEnterpriseCommonService
        {
            AllResult = [CompleteEnterprise(11, visibility)]
        };

        var result = await EnterpriseHandlers.GetEnterprisesAsync(commonService);

        var response = await TestHttp.ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        AssertOnlyGetAsyncAllInfoWasCalled(commonService);

        var items = HandlerTestSupport.ReadJsonBody(response.Body).EnumerateArray().ToList();
        var item = Assert.Single(items);

        Assert.True(item.TryGetProperty("visibility", out var reportedVisibility));
        Assert.Equal(visibility, reportedVisibility.GetString());

        // A full dto is answered whole too: the handler does not project the response by itself.
        Assert.Equal(11, item.GetProperty("id").GetInt32());
        Assert.Equal("Talleres del Norte", item.GetProperty("commercialName").GetString());
        Assert.Equal("Tallernor", item.GetProperty("tradeName").GetString());
        Assert.Equal("123", item.GetProperty("streetNumber").GetString());
        Assert.Equal("Calle Norte y Calle Sur", item.GetProperty("betweenStreets").GetString());
        Assert.Equal("Sr. Ramírez", item.GetProperty("contactingWith").GetString());
        Assert.Equal("5551234567", item.GetProperty("phones").GetString());
        Assert.Equal("Lunes a viernes de 8 a 18", item.GetProperty("schedule").GetString());
        Assert.Equal("Atención en taller", item.GetProperty("atention").GetString());
        Assert.Equal("Colonia Centro", item.GetProperty("neighborhood").GetString());
        Assert.Equal("Ciudad de Prueba", item.GetProperty("location").GetString());
        Assert.Equal("contacto@tallernor.example", item.GetProperty("email").GetString());
    }

    [Fact]
    public async Task Listing_AsksTheUseCaseOnceAndLeavesTheOtherPortsUntouched()
    {
        // The listing takes no dto nor parameter, so it reaches the use case through the read-all port
        // only: no write operation is invoked and nothing is filtered by the handler.
        var commonService = new FakeEnterpriseCommonService
        {
            AllResult = [ListedEnterprise(1, "Talleres del Norte", "Tallernor", "DISABLED")]
        };

        await EnterpriseHandlers.GetEnterprisesAsync(commonService);

        AssertOnlyGetAsyncAllInfoWasCalled(commonService);
        Assert.Null(commonService.LastRequestedDto);
        Assert.Null(commonService.LastAddedDto);
        Assert.Null(commonService.LastUpdatedDto);
        Assert.Null(commonService.LastVisibilityDto);
    }

    [Fact]
    public async Task NoRegisteredEnterprises_AreAnsweredWithOkAndAnEmptyCollection()
    {
        // Boundary case CE-6 / RF-4.1: with no registered enterprise the response is 200 with an empty
        // collection, never null and never 404.
        var commonService = new FakeEnterpriseCommonService { AllResult = [] };

        var result = await EnterpriseHandlers.GetEnterprisesAsync(commonService);

        var response = await TestHttp.ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.NotEqual(StatusCodes.Status404NotFound, response.StatusCode);
        Assert.Equal("[]", response.Body);
        Assert.Equal(JsonValueKind.Array, HandlerTestSupport.ReadJsonBody(response.Body).ValueKind);
        AssertOnlyGetAsyncAllInfoWasCalled(commonService);
    }

    [Fact]
    public async Task BusinessFailureOfTheUseCase_IsTranslatedToBadRequest()
    {
        // Error case RF-8.4: a business failure of the application reaches the handler as
        // HexArch ApplicationException, which the centralized translation answers with 400.
        var commonService = new FakeEnterpriseCommonService
        {
            ExceptionToThrow = new HexArchApplicationException("No fue posible consultar el catálogo de empresas.")
        };

        await HandlerTestSupport.AssertTranslationAsync<HexArchApplicationException>(
            () => EnterpriseHandlers.GetEnterprisesAsync(commonService),
            StatusCodes.Status400BadRequest);

        AssertOnlyGetAsyncAllInfoWasCalled(commonService);
    }

    [Fact]
    public async Task AnyOtherBusinessFailure_IsTranslatedToBadRequest()
    {
        // Error case RF-8.4: any other business cause is translated to 400 by the safe fallback of the
        // centralized handler, so the listing never leaks a 500 for it.
        var commonService = new FakeEnterpriseCommonService
        {
            ExceptionToThrow = new InvalidOperationException("No fue posible consultar el catálogo de empresas.")
        };

        await HandlerTestSupport.AssertTranslationAsync<InvalidOperationException>(
            () => EnterpriseHandlers.GetEnterprisesAsync(commonService),
            StatusCodes.Status400BadRequest);

        AssertOnlyGetAsyncAllInfoWasCalled(commonService);
    }

    [Fact]
    public async Task BusinessFailureOfTheUseCase_DeliversTheMessageInSpanish()
    {
        // Error case CE-16 / RF-8.6: ApiExceptionHandler writes exception.Message verbatim, so the
        // message the consumer reads on the 400 path has to arrive written in Spanish.
        var commonService = new FakeEnterpriseCommonService
        {
            ExceptionToThrow = new HexArchApplicationException("No fue posible consultar el catálogo de empresas.")
        };

        var exception = await Assert.ThrowsAnyAsync<HexArchApplicationException>(
            () => EnterpriseHandlers.GetEnterprisesAsync(commonService));
        var response = await TestHttp.TranslateAsync(exception);

        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Equal("No fue posible consultar el catálogo de empresas.", response.Body);
    }

    [Fact]
    public async Task AnyOtherBusinessFailure_DeliversTheMessageInSpanish()
    {
        // Error case CE-16 / RF-8.6 on the safe fallback path.
        var commonService = new FakeEnterpriseCommonService
        {
            ExceptionToThrow = new InvalidOperationException("Se produjo un error inesperado al consultar las empresas.")
        };

        var exception = await Assert.ThrowsAnyAsync<InvalidOperationException>(
            () => EnterpriseHandlers.GetEnterprisesAsync(commonService));
        var response = await TestHttp.TranslateAsync(exception);

        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Equal("Se produjo un error inesperado al consultar las empresas.", response.Body);
    }
}