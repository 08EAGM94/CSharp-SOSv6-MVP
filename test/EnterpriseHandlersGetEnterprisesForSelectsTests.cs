using System.Text.Json;
using HexArch.Application.Abstractions.PrimaryPorts;
using HexArch.Application.DTOs;
using Microsoft.AspNetCore.Http;
using SosMVP.Handlers;
using test.Fakes;
using test.Support;
using HexArchApplicationException = HexArch.Domain.Exceptions.ApplicationException;

namespace test;

/// <summary>
/// Subject: <c>EnterpriseHandlers.GetEnterprisesForSelectsAsync</c> with a fake
/// <c>ISelectService&lt;EnterpriseDTO&gt;</c>. Covers the cases the plan asks for: happy path,
/// boundary cases and error cases
/// (RF-7.1 to RF-7.4, RF-8.3, RF-8.4, RF-8.6, CE-6, CE-6c, CE-16).
/// </summary>
/// <remarks>
/// <para>The handler is a pass-through over the select port: it calls
/// <c>GetAsyncInfoForSelects()</c> once and answers whatever the use case returned. The reduction to
/// the enabled companies and the projection to <c>Id</c>, <c>CommercialName</c> and <c>TradeName</c>
/// are decided downstream in <c>EnterpriseRepository.GetAsyncInfoForSelects</c>
/// (hexArch/repository, untouchable by the constitution p5), and the plan states explicitly that the
/// handler must not repeat that decision: filtering again here would move repository logic into the
/// api layer.</para>
/// <para>Because of that, RF-7.2 and CE-6c cannot be observed as a filter in this suite. They are
/// observed from the outside: the handler must not discard a DISABLED record it receives, must not add
/// any, and must not clean, hide or fill the properties the use case did not project. When the store
/// holds no enabled company the select repository answers an empty collection and the response is
/// <c>200</c> with <c>[]</c>.</para>
/// <para>The endpoint belongs to <c>ISelectService&lt;EnterpriseDTO&gt;</c> and not to
/// <c>ICommonService&lt;EnterpriseDTO&gt;</c>: the separate port is what guarantees that the ENABLED
/// filter stays in the repository. The suite pins the declared signature of the handler by reflection
/// and, at the same time, that the select port is asked exactly once.</para>
/// </remarks>
public class EnterpriseHandlersGetEnterprisesForSelectsTests
{
    /// <summary>
    /// Mirrors the projection of <c>EnterpriseRepository.GetAsyncInfoForSelects</c>: only Id,
    /// CommercialName and TradeName travel, so every other property of the dto arrives null (RF-7.4).
    /// The visibility is a parameter and not a constant because the query does not project it, and the
    /// suite needs both shapes: absent (null) and reported.
    /// </summary>
    private static EnterpriseDTO SelectableEnterprise(
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
    /// reduced listing: it lets the suite prove that the handler never projects the response by itself.
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
    /// The reduced listing reaches the use case through the select port only, exactly once: no common
    /// port member is invoked, so the handler has no way of asking for the full catalogue and filtering
    /// it itself (RF-7.1, RF-7.2).
    /// </summary>
    private static void AssertOnlyGetAsyncInfoForSelectsWasCalled(FakeEnterpriseSelectService selectService)
    {
        Assert.Equal(1, selectService.GetAsyncInfoForSelectsCalls);
    }

    /// <summary>
    /// Every property the reduced listing does not project has to reach the consumer as an explicit
    /// null: the handler neither fills it nor hides it from the json (RF-7.4).
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
    public async Task EnabledEnterprises_AreAnsweredWithOkAndTheCollectionReturnedByTheSelectService()
    {
        // Happy path RF-7.1: the response is 200 and the body is exactly the collection that
        // GetAsyncInfoForSelects returned, with the three projected properties of each record.
        var selectService = new FakeEnterpriseSelectService
        {
            SelectResult =
            [
                SelectableEnterprise(1, "Talleres del Norte", "Tallernor"),
                SelectableEnterprise(2, "Servicios del Sur", "Surtec")
            ]
        };

        var result = await EnterpriseHandlers.GetEnterprisesForSelectsAsync(selectService);

        var response = await TestHttp.ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.NotEqual(StatusCodes.Status404NotFound, response.StatusCode);
        AssertOnlyGetAsyncInfoForSelectsWasCalled(selectService);

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
    public async Task Listing_AsksTheSelectServiceOnceAndDeclaresTheSelectPortAndNotTheCommonOne()
    {
        // Happy path, port check: this endpoint reads the catalogue through ISelectService<EnterpriseDTO>,
        // the port whose repository member applies WHERE Visibilidad = "ENABLED", and never through
        // ICommonService<EnterpriseDTO>, whose GetAsyncAllInfo would return the disabled companies too
        // (RF-7.2). The signature is read by reflection because a fake based suite can only prove which
        // port it managed to invoke, not which one the handler declares.
        var selectService = new FakeEnterpriseSelectService
        {
            SelectResult = [SelectableEnterprise(1, "Talleres del Norte", "Tallernor")]
        };
        // The common port is only a witness here: it is never handed to the handler, so every one of
        // its counters stays at zero.
        var commonService = new FakeEnterpriseCommonService();

        var method = typeof(EnterpriseHandlers)
            .GetMethod(nameof(EnterpriseHandlers.GetEnterprisesForSelectsAsync));
        Assert.NotNull(method);

        var parameters = method.GetParameters();
        var selectPort = Assert.Single(parameters);
        Assert.Equal("selectService", selectPort.Name);
        Assert.Equal(typeof(ISelectService<EnterpriseDTO>), selectPort.ParameterType);
        Assert.DoesNotContain(
            parameters,
            parameter => parameter.ParameterType.IsGenericType
                && parameter.ParameterType.GetGenericTypeDefinition() == typeof(ICommonService<>));

        await EnterpriseHandlers.GetEnterprisesForSelectsAsync(selectService);

        AssertOnlyGetAsyncInfoForSelectsWasCalled(selectService);

        Assert.Equal(0, commonService.GetAsyncAllInfoCalls);
        Assert.Equal(0, commonService.GetAsyncInfoCalls);
        Assert.Equal(0, commonService.AddAsyncInfoCalls);
        Assert.Equal(0, commonService.UpdateAsyncInfoCalls);
        Assert.Equal(0, commonService.UpdateAsyncVisibilityCalls);
        Assert.Null(commonService.LastRequestedDto);
        Assert.Null(commonService.LastUpdatedDto);
        Assert.Null(commonService.LastVisibilityDto);
    }

    [Fact]
    public async Task EnabledCollection_ReachesTheResponseIntactWithoutFilteringNorAdding()
    {
        // Boundary case RF-7.1: this is the test that proves the handler neither filters nor modifies.
        // The collection is answered whole: same records, same order, nothing removed and nothing
        // added, and the source collection handed by the use case is not mutated either.
        var stored = new List<EnterpriseDTO>
        {
            SelectableEnterprise(1, "Talleres del Norte", "Tallernor"),
            SelectableEnterprise(5, "Repuestos originales", "Repuestos"),
            SelectableEnterprise(9, "Equipos de medición", "Equipos")
        };
        var selectService = new FakeEnterpriseSelectService { SelectResult = stored };

        var result = await EnterpriseHandlers.GetEnterprisesForSelectsAsync(selectService);

        var response = await TestHttp.ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.NotEqual(StatusCodes.Status404NotFound, response.StatusCode);
        AssertOnlyGetAsyncInfoForSelectsWasCalled(selectService);

        var items = HandlerTestSupport.ReadJsonBody(response.Body).EnumerateArray().ToList();
        Assert.Equal(stored.Count, items.Count);
        Assert.Equal([1, 5, 9], items.Select(item => item.GetProperty("id").GetInt32()).ToArray());
        Assert.Equal("Talleres del Norte", items[0].GetProperty("commercialName").GetString());
        Assert.Equal("Equipos de medición", items[2].GetProperty("commercialName").GetString());

        // The handler did not rewrite nor empty the collection it received from the use case.
        Assert.Equal(3, stored.Count);
        Assert.Null(stored[0].Visibility);
        Assert.Null(stored[2].Visibility);
    }

    [Fact]
    public async Task TheVisibilityFilterLivesInTheSelectRepositoryAndNotInTheHandler()
    {
        // Boundary case RF-7.2 / CE-6c: this test does not assert the response contract, it asserts
        // where that contract comes from. RF-7.2 is guaranteed by
        // EnterpriseRepository.GetAsyncInfoForSelects (hexArch/repository, untouchable by the
        // constitution p5), which already filtered Visibilidad == "ENABLED" in the database. Because of
        // that, the handler must not filter by visibility again: whatever the select port returns
        // reaches the body untouched and no DISABLED record is removed here. This is the difference
        // against GetEnterprisesAsync, where the disabled companies are part of the response on
        // purpose.
        var selectService = new FakeEnterpriseSelectService
        {
            SelectResult =
            [
                SelectableEnterprise(1, "Talleres del Norte", "Tallernor", "ENABLED"),
                SelectableEnterprise(2, "Talleres Obsoletos", "Obsoleto", "DISABLED")
            ]
        };

        var result = await EnterpriseHandlers.GetEnterprisesForSelectsAsync(selectService);

        var response = await TestHttp.ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.NotEqual(StatusCodes.Status404NotFound, response.StatusCode);
        AssertOnlyGetAsyncInfoForSelectsWasCalled(selectService);

        var items = HandlerTestSupport.ReadJsonBody(response.Body).EnumerateArray().ToList();
        Assert.Equal(2, items.Count);
        Assert.Equal([1, 2], items.Select(item => item.GetProperty("id").GetInt32()).ToArray());
        Assert.Equal("DISABLED", items[1].GetProperty("visibility").GetString());
        Assert.Equal("Talleres Obsoletos", items[1].GetProperty("commercialName").GetString());
    }

    [Fact]
    public async Task AllDisabledStore_ProducesAnEmptyCollection()
    {
        // Boundary case RF-7.2 / CE-6c from the other side: when the store only holds DISABLED
        // companies, the select repository answers nothing, so the response is 200 with an empty
        // collection. The disabled companies are absent from the answer without the handler discarding
        // them, which is why the handler cannot be the piece that decides it.
        var selectService = new FakeEnterpriseSelectService { SelectResult = [] };

        var result = await EnterpriseHandlers.GetEnterprisesForSelectsAsync(selectService);

        var response = await TestHttp.ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.NotEqual(StatusCodes.Status404NotFound, response.StatusCode);
        Assert.Equal("[]", response.Body);
        Assert.Equal(JsonValueKind.Array, HandlerTestSupport.ReadJsonBody(response.Body).ValueKind);
        AssertOnlyGetAsyncInfoForSelectsWasCalled(selectService);
    }

    [Fact]
    public async Task NoRegisteredEnterprises_AreAnsweredWithOkAndAnEmptyCollection()
    {
        // Boundary case CE-6 / RF-7.3: with no registered enterprise the response is 200 with an empty
        // collection, never null and never 404.
        var selectService = new FakeEnterpriseSelectService { SelectResult = [] };

        var result = await EnterpriseHandlers.GetEnterprisesForSelectsAsync(selectService);

        var response = await TestHttp.ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.NotEqual(StatusCodes.Status404NotFound, response.StatusCode);
        Assert.Equal("[]", response.Body);
        Assert.Equal(JsonValueKind.Array, HandlerTestSupport.ReadJsonBody(response.Body).ValueKind);
        AssertOnlyGetAsyncInfoForSelectsWasCalled(selectService);
    }

    [Fact]
    public async Task CollectionWithoutVisibility_KeepsTheVisibilityKeyInTheResponseAndItArrivesNull()
    {
        // Risk 3 / RF-7.4: GetAsyncInfoForSelects does not project Visibility, so the dtos reach the
        // handler with Visibility = null. The handler neither fills that hole nor removes the property
        // from the json: the consumer reads "visibility": null and must not assume it is present with a
        // value. This is the observable half of the requirement, the other half (the projection itself)
        // living in the untouchable repository.
        var first = SelectableEnterprise(1, "Talleres del Norte", "Tallernor");
        var second = SelectableEnterprise(2, "Servicios del Sur", "Surtec");
        var selectService = new FakeEnterpriseSelectService { SelectResult = [first, second] };

        var result = await EnterpriseHandlers.GetEnterprisesForSelectsAsync(selectService);

        var response = await TestHttp.ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        AssertOnlyGetAsyncInfoForSelectsWasCalled(selectService);

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

        // The handler did not fill the missing visibility in the dto instances either.
        Assert.Null(first.Visibility);
        Assert.Null(second.Visibility);
    }

    [Theory]
    [InlineData("ENABLED")]
    [InlineData("DISABLED")]
    public async Task VisibilityReportedByTheSelectService_ReachesTheResponseVerbatimWithoutBeingRemovedOrRewritten(
        string visibility)
    {
        // Risk 3 / RF-7.4 from the other side: when the select port does report a visibility, the
        // handler neither strips it from the json nor rewrites its value. Cleaning the response here
        // (for instance projecting every record into Id/CommercialName/TradeName) would silently drop a
        // property that belongs to the query, so this test pins the pass-through. Reporting DISABLED
        // still answers 200: the record is in the answer because the use case sent it, not because the
        // handler accepted it.
        var selectService = new FakeEnterpriseSelectService
        {
            SelectResult = [CompleteEnterprise(11, visibility)]
        };

        var result = await EnterpriseHandlers.GetEnterprisesForSelectsAsync(selectService);

        var response = await TestHttp.ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        AssertOnlyGetAsyncInfoForSelectsWasCalled(selectService);

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
    public async Task BusinessFailureOfTheUseCase_IsTranslatedToBadRequest()
    {
        // Error case RF-8.3 / RF-8.4: the select use case reports a business failure of the application
        // and the handler lets it reach the centralized translation, which answers 400 without any
        // modification. The listing asks the use case exactly once, so there is no retry behind the
        // 400.
        var selectService = new FakeEnterpriseSelectService
        {
            ExceptionToThrow = new HexArchApplicationException("No fue posible consultar las empresas habilitadas.\n")
        };

        await HandlerTestSupport.AssertTranslationAsync<HexArchApplicationException>(
            () => EnterpriseHandlers.GetEnterprisesForSelectsAsync(selectService),
            StatusCodes.Status400BadRequest);

        AssertOnlyGetAsyncInfoForSelectsWasCalled(selectService);
    }

    [Fact]
    public async Task GenericUseCaseFailure_IsTranslatedToBadRequest()
    {
        // Error case RF-8.4: the reduced listing has no business failure of its own, so any other
        // business cause reported by the use case is translated to 400 by the safe fallback of the
        // centralized handler, never to a 500.
        var selectService = new FakeEnterpriseSelectService
        {
            ExceptionToThrow = new InvalidOperationException("No fue posible consultar las empresas habilitadas.")
        };

        await HandlerTestSupport.AssertTranslationAsync<InvalidOperationException>(
            () => EnterpriseHandlers.GetEnterprisesForSelectsAsync(selectService),
            StatusCodes.Status400BadRequest);

        AssertOnlyGetAsyncInfoForSelectsWasCalled(selectService);
    }

    [Fact]
    public async Task BusinessFailureOfTheUseCase_DeliversTheMessageInSpanish()
    {
        // Error case RF-8.6 / CE-16: ApiExceptionHandler writes exception.Message verbatim, so the
        // message the consumer reads on the 400 path has to arrive written in Spanish.
        var selectService = new FakeEnterpriseSelectService
        {
            ExceptionToThrow = new HexArchApplicationException("No fue posible consultar las empresas habilitadas.\n")
        };

        var exception = await Assert.ThrowsAnyAsync<HexArchApplicationException>(
            () => EnterpriseHandlers.GetEnterprisesForSelectsAsync(selectService));
        var response = await TestHttp.TranslateAsync(exception);

        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Equal("No fue posible consultar las empresas habilitadas.\n", response.Body);
    }

    [Fact]
    public async Task GenericUseCaseFailure_DeliversTheMessageInSpanish()
    {
        // Error case RF-8.6 / CE-16 on the safe fallback path.
        var selectService = new FakeEnterpriseSelectService
        {
            ExceptionToThrow = new InvalidOperationException("Se produjo un error inesperado al consultar las empresas habilitadas.")
        };

        var exception = await Assert.ThrowsAnyAsync<InvalidOperationException>(
            () => EnterpriseHandlers.GetEnterprisesForSelectsAsync(selectService));
        var response = await TestHttp.TranslateAsync(exception);

        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Equal("Se produjo un error inesperado al consultar las empresas habilitadas.", response.Body);
    }
}