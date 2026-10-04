using HexArch.Application.DTOs;
using HexArch.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using SosMVP.Handlers;
using test.Fakes;
using test.Support;
using HexArchApplicationException = HexArch.Domain.Exceptions.ApplicationException;

namespace test;

/// <summary>
/// Subject: <c>EnterpriseHandlers.UpdateEnterpriseAsync</c> with a fake <c>ICommonService&lt;EnterpriseDTO&gt;</c>.
/// Covers the cases the plan asks for: happy path, boundary cases and error cases
/// (RF-5.1 to RF-5.6, RF-8.1, RF-8.2, RF-8.3, RF-8.4, RF-8.6, CE-1, CE-3, CE-5, CE-11, CE-16).
/// </summary>
/// <remarks>
/// <para>RF-5.2 (the update touches every property except <c>Id</c> and <c>Visibility</c>) is implemented by
/// <c>EnterpriseRepository.UpdateAsyncInfo</c> (hexArch/repository, untouchable by the constitution p5). What is
/// observable here is the pass-through: the handler replaces the <c>Id</c> and delegates the received dto itself,
/// so it neither erases <c>Visibility</c> nor any other property of the body, and no column selection is
/// invented in the API layer.</para>
/// <para>RF-5.6 / CE-14 (a non numeric route identifier answered with 400) are not verified here: minimal api
/// rejects the value while binding the route parameter into the <c>int id</c> of the handler, so the rejection
/// happens before the handler body runs and is out of the reach of a fake based suite. They are covered by the
/// metadata suite, exactly as spec 002 does for <c>TypeEndpointsMetadataTests</c>.</para>
/// </remarks>
public class EnterpriseHandlersUpdateEnterpriseTests
{
    private const int RouteId = 7;

    /// <summary>
    /// The body a consumer would send to <c>PUT /enterprise/{id}</c>: every property informed and an
    /// <c>Id</c> of its own, so the suite can tell whether the route or the body decides the identifier.
    /// </summary>
    private static EnterpriseDTO BodyWithAnotherId(int bodyId = 99, string? visibility = "DISABLED")
    {
        return new EnterpriseDTO
        {
            Id = bodyId,
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
    /// Overwrites one property of the body with a value longer than the maximum the entity setters accept,
    /// reproducing what the domain would reject with <c>EntityException</c> (RF-5.4).
    /// </summary>
    private static EnterpriseDTO BodyOutsideAllowedLength(string property, int length)
    {
        var dto = BodyWithAnotherId();
        var value = new string('A', length);

        switch (property)
        {
            case nameof(EnterpriseDTO.CommercialName):
                dto.CommercialName = value;
                break;
            case nameof(EnterpriseDTO.TradeName):
                dto.TradeName = value;
                break;
            case nameof(EnterpriseDTO.StreetNumber):
                dto.StreetNumber = value;
                break;
            case nameof(EnterpriseDTO.BetweenStreets):
                dto.BetweenStreets = value;
                break;
            case nameof(EnterpriseDTO.ContactingWith):
                dto.ContactingWith = value;
                break;
            case nameof(EnterpriseDTO.Phones):
                dto.Phones = value;
                break;
            case nameof(EnterpriseDTO.Schedule):
                dto.Schedule = value;
                break;
            case nameof(EnterpriseDTO.Atention):
                dto.Atention = value;
                break;
            case nameof(EnterpriseDTO.Neighborhood):
                dto.Neighborhood = value;
                break;
            case nameof(EnterpriseDTO.Location):
                dto.Location = value;
                break;
            case nameof(EnterpriseDTO.Email):
                dto.Email = value;
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(property),
                    property,
                    "EnterpriseDTO has no property with that name.");
        }

        return dto;
    }

    /// <summary>
    /// The update has to reach the use case through <c>UpdateAsyncInfo</c> only once, and must not touch any
    /// other member of the primary port (RF-5.2).
    /// </summary>
    private static void AssertOnlyUpdateAsyncInfoWasCalled(FakeEnterpriseCommonService commonService)
    {
        Assert.Equal(1, commonService.UpdateAsyncInfoCalls);
        Assert.Equal(0, commonService.AddAsyncInfoCalls);
        Assert.Equal(0, commonService.GetAsyncInfoCalls);
        Assert.Equal(0, commonService.GetAsyncAllInfoCalls);
        Assert.Equal(0, commonService.UpdateAsyncVisibilityCalls);
        Assert.Null(commonService.LastAddedDto);
        Assert.Null(commonService.LastRequestedDto);
        Assert.Null(commonService.LastVisibilityDto);
    }

    [Fact]
    public async Task ValidUpdate_IsAnsweredWithNoContentAndWithoutBody()
    {
        // Happy path: the use case UpdateAsyncInfo is void, so the response is 204 with no body at all
        // (RF-5.5): the update produces no object of return.
        var commonService = new FakeEnterpriseCommonService();

        var result = await EnterpriseHandlers.UpdateEnterpriseAsync(commonService, RouteId, BodyWithAnotherId());

        await HandlerTestSupport.AssertNoContentAsync(result);
    }

    [Fact]
    public async Task ValidUpdate_IsDelegatedOnceToTheUseCaseAndLeavesTheOtherPortsUntouched()
    {
        // Happy path: exactly one delegation to UpdateAsyncInfo carries the whole body (RF-5.2), and no read,
        // insert or visibility operation is triggered by this endpoint.
        var commonService = new FakeEnterpriseCommonService();
        var dto = BodyWithAnotherId();

        await EnterpriseHandlers.UpdateEnterpriseAsync(commonService, RouteId, dto);

        AssertOnlyUpdateAsyncInfoWasCalled(commonService);
        Assert.NotNull(commonService.LastUpdatedDto);
    }

    [Fact]
    public async Task Update_DelegatesTheReceivedDtoInsteadOfProjectingIt()
    {
        // RF-5.2 from the shape of the delegation: the handler delegates the very dto it received and only
        // overwrites its Id. Projecting the body into a reduced dto here (the way UpdateEnterpriseVisibility
        // reduces the body to Id plus Visibility, RF-6.2) would silently drop the properties the update needs,
        // so this test pins the pass-through.
        var commonService = new FakeEnterpriseCommonService();
        var dto = BodyWithAnotherId();

        await EnterpriseHandlers.UpdateEnterpriseAsync(commonService, RouteId, dto);

        Assert.Same(dto, commonService.LastUpdatedDto);
    }

    [Fact]
    public async Task RouteId_OverridesTheIdOfTheBody()
    {
        // Boundary case (CE-1, RF-5.1): the identifier that reaches the use case is exactly the route one,
        // whatever Id the body carried.
        var commonService = new FakeEnterpriseCommonService();

        await EnterpriseHandlers.UpdateEnterpriseAsync(commonService, RouteId, BodyWithAnotherId());

        var received = Assert.IsType<EnterpriseDTO>(commonService.LastUpdatedDto);
        Assert.Equal(RouteId, received.Id);
        AssertOnlyUpdateAsyncInfoWasCalled(commonService);
    }

    [Theory]
    [InlineData(99)]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task RouteId_OverridesAnyIdSentInTheBody(int bodyId)
    {
        // Boundary case CE-1 from every angle: another positive identifier, zero and a negative one are all
        // discarded, because the route is the only source of the identifier the update is applied to
        // (RF-5.1). A handler that validated the Id of the body, or that let it win, would answer a
        // different code or would update another record.
        var commonService = new FakeEnterpriseCommonService();

        var result = await EnterpriseHandlers.UpdateEnterpriseAsync(
            commonService,
            RouteId,
            BodyWithAnotherId(bodyId: bodyId));

        await HandlerTestSupport.AssertNoContentAsync(result);
        Assert.Equal(RouteId, Assert.IsType<EnterpriseDTO>(commonService.LastUpdatedDto).Id);
        AssertOnlyUpdateAsyncInfoWasCalled(commonService);
    }

    [Fact]
    public async Task BodyWithoutId_IsUpdatedWithTheRouteId()
    {
        // Boundary case CE-1: a body that does not carry an Id at all is the shape the mapper tolerates
        // (Id is nullable on the dto), and the handler still hands the update the route identifier
        // (RF-5.1). Nothing is lost and nothing has to be filled by the consumer.
        var commonService = new FakeEnterpriseCommonService();

        await EnterpriseHandlers.UpdateEnterpriseAsync(commonService, RouteId, BodyWithAnotherId(bodyId: 0));

        var received = Assert.IsType<EnterpriseDTO>(commonService.LastUpdatedDto);
        Assert.Equal(RouteId, received.Id);
        AssertOnlyUpdateAsyncInfoWasCalled(commonService);
    }

    [Fact]
    public async Task TheRestOfTheBodyReachesTheUseCaseUnchanged()
    {
        // Boundary case CE-1 / RF-5.2: only the Id is replaced. Every other property of the request travels
        // to the use case with the value the consumer sent, none of them is emptied nor defaulted by the
        // handler, which is what lets the repository decide which columns are written.
        var commonService = new FakeEnterpriseCommonService();
        var dto = BodyWithAnotherId();

        await EnterpriseHandlers.UpdateEnterpriseAsync(commonService, RouteId, dto);

        var received = Assert.IsType<EnterpriseDTO>(commonService.LastUpdatedDto);
        Assert.Equal(RouteId, received.Id);
        Assert.Equal(dto.CommercialName, received.CommercialName);
        Assert.Equal(dto.TradeName, received.TradeName);
        Assert.Equal(dto.StreetNumber, received.StreetNumber);
        Assert.Equal(dto.BetweenStreets, received.BetweenStreets);
        Assert.Equal(dto.ContactingWith, received.ContactingWith);
        Assert.Equal(dto.Phones, received.Phones);
        Assert.Equal(dto.Schedule, received.Schedule);
        Assert.Equal(dto.Atention, received.Atention);
        Assert.Equal(dto.Neighborhood, received.Neighborhood);
        Assert.Equal(dto.Location, received.Location);
        Assert.Equal(dto.Email, received.Email);
        Assert.Equal(dto.Visibility, received.Visibility);
    }

    [Theory]
    [InlineData("ENABLED")]
    [InlineData("DISABLED")]
    [InlineData("VALOR_NO_ADMITIDO")]
    public async Task VisibilitySentInTheBody_IsNotRemovedNorRewrittenByTheHandler(string visibility)
    {
        // RF-5.2 / risk 5: the update leaves the visibility column alone, so whether the incoming Visibility
        // is honoured belongs to the repository, not to the handler. The handler neither validates the value
        // against ENABLED/DISABLED nor overwrites it, and it does not strip the property from the delegated
        // dto; clearing it here would make the pass-through lose information the query layer still owns.
        var commonService = new FakeEnterpriseCommonService();

        var result = await EnterpriseHandlers.UpdateEnterpriseAsync(
            commonService,
            RouteId,
            BodyWithAnotherId(visibility: visibility));

        await HandlerTestSupport.AssertNoContentAsync(result);
        AssertOnlyUpdateAsyncInfoWasCalled(commonService);
        Assert.Equal(visibility, Assert.IsType<EnterpriseDTO>(commonService.LastUpdatedDto).Visibility);
    }

    [Fact]
    public async Task BodyWithoutVisibility_ReachesTheUseCaseWithoutVisibility()
    {
        // RF-5.2 from the other side: a body that does not inform Visibility delegates a dto whose Visibility
        // stays null. The handler does not fill the hole with a default, which would make this endpoint
        // decide the lifecycle state of a record it is only allowed to describe.
        var commonService = new FakeEnterpriseCommonService();

        await EnterpriseHandlers.UpdateEnterpriseAsync(
            commonService,
            RouteId,
            BodyWithAnotherId(visibility: null));

        Assert.Null(Assert.IsType<EnterpriseDTO>(commonService.LastUpdatedDto).Visibility);
        AssertOnlyUpdateAsyncInfoWasCalled(commonService);
    }

    [Fact]
    public async Task NonExistentEnterprise_IsTranslatedToNotFoundAndIsNotRetried()
    {
        // Error case CE-3 / CE-5 / RF-5.3 / RF-8.1: the repository reports that the identifier matches no
        // record and the centralized translation answers 404, the only way this endpoint answers 404. The
        // failure is not retried, so no second modification of a non existing record is attempted, and the
        // route id was already applied before delegating.
        var commonService = new FakeEnterpriseCommonService
        {
            ExceptionToThrow = new KeyNotFoundException(
                "No se encontró una empresa con el identificador solicitado.")
        };

        await HandlerTestSupport.AssertTranslationAsync<KeyNotFoundException>(
            () => EnterpriseHandlers.UpdateEnterpriseAsync(commonService, 404, BodyWithAnotherId()),
            StatusCodes.Status404NotFound);

        AssertOnlyUpdateAsyncInfoWasCalled(commonService);
        Assert.Equal(404, Assert.IsType<EnterpriseDTO>(commonService.LastUpdatedDto).Id);
    }

    [Fact]
    public async Task RecordNotFound_DeliversTheMessageInSpanish()
    {
        // Error case CE-16 / RF-8.6 on the 404 path: ApiExceptionHandler writes exception.Message verbatim,
        // so the message the consumer reads has to arrive written in Spanish.
        var commonService = new FakeEnterpriseCommonService
        {
            ExceptionToThrow = new KeyNotFoundException(
                "No se encontró una empresa con el identificador solicitado.")
        };

        var exception = await Assert.ThrowsAnyAsync<KeyNotFoundException>(
            () => EnterpriseHandlers.UpdateEnterpriseAsync(commonService, 404, BodyWithAnotherId()));
        var response = await TestHttp.TranslateAsync(exception);

        Assert.Equal(StatusCodes.Status404NotFound, response.StatusCode);
        Assert.Equal(
            "No se encontró una empresa con el identificador solicitado.",
            response.Body);
    }

    [Theory]
    [InlineData("CommercialName", 151)]
    [InlineData("TradeName", 151)]
    [InlineData("StreetNumber", 51)]
    [InlineData("BetweenStreets", 151)]
    [InlineData("ContactingWith", 151)]
    [InlineData("Phones", 151)]
    [InlineData("Schedule", 151)]
    [InlineData("Atention", 151)]
    [InlineData("Neighborhood", 51)]
    [InlineData("Location", 51)]
    [InlineData("Email", 151)]
    public async Task EnterpriseOutsideAllowedLengths_IsTranslatedToBadRequest(string property, int length)
    {
        // Error case CE-11 / RF-5.4: each property of the body has its own maximum length in
        // EnterpriseEntity, and going one character over makes the setters report the violation as
        // EntityException, which RF-8.2 translates to 400 without writing anything. The handler performs no
        // length validation of its own, so the dto reaches the use case exactly as it arrived.
        var commonService = new FakeEnterpriseCommonService
        {
            ExceptionToThrow = new EntityException(
                "El nombre comercial debe tener un máximo de 150 caracteres.")
        };

        await HandlerTestSupport.AssertTranslationAsync<EntityException>(
            () => EnterpriseHandlers.UpdateEnterpriseAsync(
                commonService,
                RouteId,
                BodyOutsideAllowedLength(property, length)),
            StatusCodes.Status400BadRequest);

        AssertOnlyUpdateAsyncInfoWasCalled(commonService);
    }

    [Fact]
    public async Task EntityRuleViolation_DeliversMessageInSpanish()
    {
        // Error case CE-16 / RF-8.6 on the 400 path raised by the entity.
        var commonService = new FakeEnterpriseCommonService
        {
            ExceptionToThrow = new EntityException("La colonia debe tener un máximo de 50 caracteres.")
        };

        var exception = await Assert.ThrowsAnyAsync<EntityException>(
            () => EnterpriseHandlers.UpdateEnterpriseAsync(
                commonService,
                RouteId,
                BodyOutsideAllowedLength(nameof(EnterpriseDTO.Neighborhood), 51)));
        var response = await TestHttp.TranslateAsync(exception);

        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Equal("La colonia debe tener un máximo de 50 caracteres.", response.Body);
    }

    [Fact]
    public async Task BusinessFailureOfTheUseCase_IsTranslatedToBadRequest()
    {
        // Error case RF-8.3 / RF-8.4: a business failure of the application reaches the handler as HexArch
        // ApplicationException, which the centralized translation answers with 400, never with 204.
        var commonService = new FakeEnterpriseCommonService
        {
            ExceptionToThrow = new HexArchApplicationException(
                "No fue posible actualizar la información de la empresa.")
        };

        await HandlerTestSupport.AssertTranslationAsync<HexArchApplicationException>(
            () => EnterpriseHandlers.UpdateEnterpriseAsync(commonService, RouteId, BodyWithAnotherId()),
            StatusCodes.Status400BadRequest);

        AssertOnlyUpdateAsyncInfoWasCalled(commonService);
    }

    [Fact]
    public async Task AnyOtherBusinessFailure_IsTranslatedToBadRequest()
    {
        // Error case RF-8.4: any other business cause is translated to 400 by the safe fallback of the
        // centralized handler, so the update never leaks a 500 for it.
        var commonService = new FakeEnterpriseCommonService
        {
            ExceptionToThrow = new InvalidOperationException(
                "No fue posible actualizar la información de la empresa.")
        };

        await HandlerTestSupport.AssertTranslationAsync<InvalidOperationException>(
            () => EnterpriseHandlers.UpdateEnterpriseAsync(commonService, RouteId, BodyWithAnotherId()),
            StatusCodes.Status400BadRequest);

        AssertOnlyUpdateAsyncInfoWasCalled(commonService);
    }

    [Fact]
    public async Task BusinessFailureOfTheUseCase_DeliversTheMessageInSpanish()
    {
        // Error case CE-16 / RF-8.6 on the 400 path: the message reaches the consumer as written.
        var commonService = new FakeEnterpriseCommonService
        {
            ExceptionToThrow = new HexArchApplicationException(
                "No fue posible actualizar la información de la empresa.")
        };

        var exception = await Assert.ThrowsAnyAsync<HexArchApplicationException>(
            () => EnterpriseHandlers.UpdateEnterpriseAsync(commonService, RouteId, BodyWithAnotherId()));
        var response = await TestHttp.TranslateAsync(exception);

        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Equal("No fue posible actualizar la información de la empresa.", response.Body);
    }

    [Fact]
    public async Task AnyOtherBusinessFailure_DeliversTheMessageInSpanish()
    {
        // Error case CE-16 / RF-8.6 on the safe fallback path.
        var commonService = new FakeEnterpriseCommonService
        {
            ExceptionToThrow = new InvalidOperationException(
                "Se produjo un error inesperado al actualizar la empresa.")
        };

        var exception = await Assert.ThrowsAnyAsync<InvalidOperationException>(
            () => EnterpriseHandlers.UpdateEnterpriseAsync(commonService, RouteId, BodyWithAnotherId()));
        var response = await TestHttp.TranslateAsync(exception);

        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Equal("Se produjo un error inesperado al actualizar la empresa.", response.Body);
    }
}
