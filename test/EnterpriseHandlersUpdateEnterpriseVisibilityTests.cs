using System.Text.Json;
using HexArch.Application.DTOs;
using Microsoft.AspNetCore.Http;
using SosMVP.Handlers;
using test.Fakes;
using test.Support;
using HexArchApplicationException = HexArch.Domain.Exceptions.ApplicationException;

namespace test;

/// <summary>
/// Subject: <c>EnterpriseHandlers.UpdateEnterpriseVisibilityAsync</c> with a fake
/// <c>ICommonService&lt;EnterpriseDTO&gt;</c>. Covers the cases the plan asks for: happy path,
/// boundary cases and error cases
/// (RF-6.1 to RF-6.6, RF-8.1, RF-8.3, RF-8.4, RF-8.6, CE-1, CE-2, CE-3, CE-13, CE-16).
/// </summary>
/// <remarks>
/// <para>This handler is the opposite of <c>UpdateEnterpriseAsync</c>: it does not delegate the dto it
/// received. It builds a new dto holding only <c>Id</c> (the route one) and <c>Visibility</c> (the body
/// one) and delegates that projection, so every other property of the request is discarded before the
/// use case sees it (RF-6.2, CE-2). The suite pins that reduction from both sides: the eleven
/// properties of the body must arrive null at the use case, and the record information must not be
/// written by this endpoint (<c>UpdateAsyncInfo</c> is never called).</para>
/// <para>RF-6.6 / CE-14 (a non numeric route identifier answered with 400) are not verified here: minimal
/// api rejects the value while binding the route parameter into the <c>int id</c> of the handler, so the
/// rejection happens before the handler body runs and is out of the reach of a fake based suite. They are
/// covered by the metadata suite, exactly as spec 002 does for <c>TypeEndpointsMetadataTests</c>. The
/// closest thing this suite can observe is a route identifier that is numeric but not positive, which the
/// use case rejects with <c>ApplicationException</c> and the translation answers with 400.</para>
/// </remarks>
public class EnterpriseHandlersUpdateEnterpriseVisibilityTests
{
    private const int RouteId = 7;

    /// <summary>
    /// The message the use case reports when the Visibility is not one of the admitted values
    /// (<c>CommonService.UpdateAsyncVisibility</c>), reproduced verbatim so the suite also proves the
    /// handler delivers that message to the consumer.
    /// </summary>
    private const string InvalidVisibilityMessage = "La visibilidad debe ser ENABLED o DISABLED.\n";

    /// <summary>
    /// The message the use case reports when the identifier is not greater than zero
    /// (<c>CommonService.UpdateAsyncVisibility</c>).
    /// </summary>
    private const string NonPositiveIdMessage = "El identificador debe ser un número mayor que cero.\n";

    /// <summary>
    /// The body a consumer would really send to <c>PUT /enterprisev/{id}</c>: every property informed and
    /// an <c>Id</c> of its own, so the suite can tell whether the route or the body decides the identifier
    /// and whether the rest of the record reaches the use case.
    /// </summary>
    private static EnterpriseDTO BodyWithEveryPropertyInformed(int? bodyId = 99, string? visibility = "DISABLED")
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
    /// The very same body as a consumer writes it on the wire: the thirteen camelCase keys minimal api
    /// binds. Reading the body from json makes the reduction test independent of how the dto instance was
    /// built, and pinning every key here proves the nulls observed later come from the handler and not
    /// from a key that failed to bind (note <c>atention</c>, not <c>attention</c>).
    /// </summary>
    private const string ConsumerBodyJson = """
        {
          "id": 99,
          "commercialName": "Talleres del Norte",
          "tradeName": "Tallernor",
          "streetNumber": "123",
          "betweenStreets": "Calle Norte y Calle Sur",
          "contactingWith": "Sr. Ramírez",
          "phones": "5551234567",
          "schedule": "Lunes a viernes de 8 a 18",
          "atention": "Atención en taller",
          "neighborhood": "Colonia Centro",
          "location": "Ciudad de Prueba",
          "email": "contacto@tallernor.example",
          "visibility": "DISABLED"
        }
        """;

    private static EnterpriseDTO BodyAsItArrivesFromJson()
    {
        return JsonSerializer.Deserialize<EnterpriseDTO>(
            ConsumerBodyJson,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    }

    /// <summary>
    /// Overwrites one property of the body with a value longer than the maximum the entity setters accept,
    /// reproducing what the domain would reject with <c>EntityException</c> if the property ever reached
    /// the mapper (RF-5.4 on the record update, irrelevant here).
    /// </summary>
    private static EnterpriseDTO BodyOutsideAllowedLength(string property, int length)
    {
        var dto = BodyWithEveryPropertyInformed();
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
    /// The change of visibility has to reach the use case through <c>UpdateAsyncVisibility</c> only, exactly
    /// once, and must not touch any other member of the primary port (RF-6.2). In particular
    /// <c>UpdateAsyncInfo</c> stays at zero: this endpoint never writes the information of the record.
    /// </summary>
    private static void AssertOnlyUpdateAsyncVisibilityWasCalled(FakeEnterpriseCommonService commonService)
    {
        Assert.Equal(1, commonService.UpdateAsyncVisibilityCalls);
        Assert.Equal(0, commonService.AddAsyncInfoCalls);
        Assert.Equal(0, commonService.GetAsyncInfoCalls);
        Assert.Equal(0, commonService.GetAsyncAllInfoCalls);
        Assert.Equal(0, commonService.UpdateAsyncInfoCalls);
        Assert.Null(commonService.LastAddedDto);
        Assert.Null(commonService.LastRequestedDto);
        Assert.Null(commonService.LastUpdatedDto);
    }

    /// <summary>
    /// The eleven properties of the record other than <c>Id</c> and <c>Visibility</c> must arrive null at
    /// the use case: this endpoint only changes the visibility of the enterprise (RF-6.2, CE-2).
    /// </summary>
    private static void AssertEveryPropertyExceptIdAndVisibilityIsNull(EnterpriseDTO dto)
    {
        Assert.Null(dto.CommercialName);
        Assert.Null(dto.TradeName);
        Assert.Null(dto.StreetNumber);
        Assert.Null(dto.BetweenStreets);
        Assert.Null(dto.ContactingWith);
        Assert.Null(dto.Phones);
        Assert.Null(dto.Schedule);
        Assert.Null(dto.Atention);
        Assert.Null(dto.Neighborhood);
        Assert.Null(dto.Location);
        Assert.Null(dto.Email);
    }

    [Fact]
    public async Task ValidVisibilityUpdate_IsAnsweredWithNoContentAndWithoutBody()
    {
        // Happy path: the use case UpdateAsyncVisibility is void, so the response is 204 with no body at
        // all (RF-6.5): changing the visibility produces no object of return.
        var commonService = new FakeEnterpriseCommonService();

        var result = await EnterpriseHandlers.UpdateEnterpriseVisibilityAsync(
            commonService,
            RouteId,
            BodyWithEveryPropertyInformed());

        await HandlerTestSupport.AssertNoContentAsync(result);
    }

    [Fact]
    public async Task ValidVisibilityUpdate_IsDelegatedOnceToTheUseCaseAndLeavesTheOtherPortsUntouched()
    {
        // Happy path: exactly one delegation to UpdateAsyncVisibility (RF-6.5) and no read, insert or
        // record update is triggered by this endpoint.
        var commonService = new FakeEnterpriseCommonService();

        await EnterpriseHandlers.UpdateEnterpriseVisibilityAsync(
            commonService,
            RouteId,
            BodyWithEveryPropertyInformed());

        AssertOnlyUpdateAsyncVisibilityWasCalled(commonService);
        Assert.NotNull(commonService.LastVisibilityDto);
    }

    [Fact]
    public async Task RouteId_OverridesTheIdOfTheBody()
    {
        // Boundary case (CE-1, RF-6.1): the identifier that reaches the use case is exactly the route one,
        // whatever Id the body carried.
        var commonService = new FakeEnterpriseCommonService();

        await EnterpriseHandlers.UpdateEnterpriseVisibilityAsync(
            commonService,
            RouteId,
            BodyWithEveryPropertyInformed());

        Assert.Equal(RouteId, Assert.IsType<EnterpriseDTO>(commonService.LastVisibilityDto).Id);
        AssertOnlyUpdateAsyncVisibilityWasCalled(commonService);
    }

    [Theory]
    [InlineData(99)]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task RouteId_OverridesAnyIdSentInTheBody(int bodyId)
    {
        // Boundary case CE-1 from every angle: another positive identifier, zero and a negative one are
        // all discarded, because the route is the only source of the identifier whose visibility is
        // changed (RF-6.1). A handler that let the body win would change the visibility of another
        // record.
        var commonService = new FakeEnterpriseCommonService();

        var result = await EnterpriseHandlers.UpdateEnterpriseVisibilityAsync(
            commonService,
            RouteId,
            BodyWithEveryPropertyInformed(bodyId: bodyId));

        await HandlerTestSupport.AssertNoContentAsync(result);
        Assert.Equal(RouteId, Assert.IsType<EnterpriseDTO>(commonService.LastVisibilityDto).Id);
        AssertOnlyUpdateAsyncVisibilityWasCalled(commonService);
    }

    [Fact]
    public async Task BodyWithoutId_IsUpdatedWithTheRouteId()
    {
        // Boundary case CE-1: a body that does not carry an Id at all is the shape the consumer may send
        // (Id is nullable on the dto), and the handler still hands the change the route identifier
        // (RF-6.1). Nothing has to be filled by the consumer.
        var commonService = new FakeEnterpriseCommonService();

        await EnterpriseHandlers.UpdateEnterpriseVisibilityAsync(
            commonService,
            RouteId,
            BodyWithEveryPropertyInformed(bodyId: null));

        Assert.Equal(RouteId, Assert.IsType<EnterpriseDTO>(commonService.LastVisibilityDto).Id);
        AssertOnlyUpdateAsyncVisibilityWasCalled(commonService);
    }

    [Fact]
    public async Task OnlyIdAndVisibilityReachTheUseCase_EveryOtherPropertyOfTheBodyIsDiscarded()
    {
        // Boundary case (RF-6.2, CE-2), and the reason this endpoint exists as a separate one: the handler
        // projects the request into a new dto holding only Id and Visibility, so the eleven properties of
        // the record arrive null at the use case and can never be written by this endpoint.
        var commonService = new FakeEnterpriseCommonService();
        var body = BodyWithEveryPropertyInformed();

        await EnterpriseHandlers.UpdateEnterpriseVisibilityAsync(commonService, RouteId, body);

        var received = Assert.IsType<EnterpriseDTO>(commonService.LastVisibilityDto);
        Assert.Equal(RouteId, received.Id);
        Assert.Equal("DISABLED", received.Visibility);
        AssertEveryPropertyExceptIdAndVisibilityIsNull(received);
        AssertOnlyUpdateAsyncVisibilityWasCalled(commonService);
    }

    [Fact]
    public async Task RealisticJsonBody_LeavesOnlyIdAndVisibilityAtTheUseCase()
    {
        // Boundary case (RF-6.2, CE-2) from the wire: the body is bound from the thirteen camelCase keys
        // minimal api understands, so every one of them really reaches the handler, and the dto delegated
        // to the use case keeps only the route id and the reported visibility. The first block asserts the
        // binding worked, so the nulls of the second block are attributable to the projection.
        var body = BodyAsItArrivesFromJson();

        Assert.Equal(99, body.Id);
        Assert.Equal("Talleres del Norte", body.CommercialName);
        Assert.Equal("Tallernor", body.TradeName);
        Assert.Equal("123", body.StreetNumber);
        Assert.Equal("Calle Norte y Calle Sur", body.BetweenStreets);
        Assert.Equal("Sr. Ramírez", body.ContactingWith);
        Assert.Equal("5551234567", body.Phones);
        Assert.Equal("Lunes a viernes de 8 a 18", body.Schedule);
        Assert.Equal("Atención en taller", body.Atention);
        Assert.Equal("Colonia Centro", body.Neighborhood);
        Assert.Equal("Ciudad de Prueba", body.Location);
        Assert.Equal("contacto@tallernor.example", body.Email);
        Assert.Equal("DISABLED", body.Visibility);

        var commonService = new FakeEnterpriseCommonService();

        var result = await EnterpriseHandlers.UpdateEnterpriseVisibilityAsync(commonService, RouteId, body);

        await HandlerTestSupport.AssertNoContentAsync(result);

        var received = Assert.IsType<EnterpriseDTO>(commonService.LastVisibilityDto);
        Assert.Equal(RouteId, received.Id);
        Assert.Equal("DISABLED", received.Visibility);
        AssertEveryPropertyExceptIdAndVisibilityIsNull(received);
        AssertOnlyUpdateAsyncVisibilityWasCalled(commonService);
    }

    [Fact]
    public async Task TheDelegatedDtoIsANewProjectionAndNotTheBodyItself()
    {
        // Boundary case (RF-6.2): unlike UpdateEnterprise, which delegates the received dto as a
        // pass-through, this handler delegates a different instance. Delegating the body itself would let
        // every property of the request travel to the query, so this test pins the copy.
        var commonService = new FakeEnterpriseCommonService();
        var body = BodyWithEveryPropertyInformed();

        await EnterpriseHandlers.UpdateEnterpriseVisibilityAsync(commonService, RouteId, body);

        Assert.NotSame(body, commonService.LastVisibilityDto);
    }

    [Fact]
    public async Task InformationOfTheEnterprise_IsNotModifiedByThisEndpoint()
    {
        // Boundary case (RF-6.2, CE-2) from the record point of view: changing the visibility never writes
        // the information of the enterprise. The body keeps the values the consumer sent, the use case is
        // reached only through UpdateAsyncVisibility and no dto travels through UpdateAsyncInfo, the one
        // that would let the data of the record reach the storage.
        var commonService = new FakeEnterpriseCommonService();
        var body = BodyWithEveryPropertyInformed();

        var result = await EnterpriseHandlers.UpdateEnterpriseVisibilityAsync(commonService, RouteId, body);

        await HandlerTestSupport.AssertNoContentAsync(result);

        Assert.Equal(0, commonService.UpdateAsyncInfoCalls);
        Assert.Null(commonService.LastUpdatedDto);
        AssertOnlyUpdateAsyncVisibilityWasCalled(commonService);

        Assert.Equal("Talleres del Norte", body.CommercialName);
        Assert.Equal("Tallernor", body.TradeName);
        Assert.Equal("5551234567", body.Phones);
        Assert.Equal("contacto@tallernor.example", body.Email);
    }

    [Theory]
    [InlineData(nameof(EnterpriseDTO.CommercialName), 151)]
    [InlineData(nameof(EnterpriseDTO.TradeName), 151)]
    [InlineData(nameof(EnterpriseDTO.StreetNumber), 51)]
    [InlineData(nameof(EnterpriseDTO.BetweenStreets), 151)]
    [InlineData(nameof(EnterpriseDTO.ContactingWith), 151)]
    [InlineData(nameof(EnterpriseDTO.Phones), 151)]
    [InlineData(nameof(EnterpriseDTO.Schedule), 151)]
    [InlineData(nameof(EnterpriseDTO.Atention), 151)]
    [InlineData(nameof(EnterpriseDTO.Neighborhood), 51)]
    [InlineData(nameof(EnterpriseDTO.Location), 51)]
    [InlineData(nameof(EnterpriseDTO.Email), 151)]
    public async Task PropertyOutsideAllowedLengthInTheBody_IsStillDiscarded(string property, int length)
    {
        // Boundary case (RF-6.2, CE-2): a property that would violate the rules of EnterpriseEntity cannot
        // reach the mapper through this endpoint, because it is not projected at all. The request is
        // answered 204 instead of the 400 the record update would answer for the same length.
        var commonService = new FakeEnterpriseCommonService();

        var result = await EnterpriseHandlers.UpdateEnterpriseVisibilityAsync(
            commonService,
            RouteId,
            BodyOutsideAllowedLength(property, length));

        await HandlerTestSupport.AssertNoContentAsync(result);
        AssertOnlyUpdateAsyncVisibilityWasCalled(commonService);

        var received = Assert.IsType<EnterpriseDTO>(commonService.LastVisibilityDto);
        Assert.Equal(RouteId, received.Id);
        Assert.Equal("DISABLED", received.Visibility);
        AssertEveryPropertyExceptIdAndVisibilityIsNull(received);
    }

    [Theory]
    [InlineData("ENABLED")]
    [InlineData("DISABLED")]
    public async Task VisibilityOfTheBody_IsTheOnlyValueHandedToTheUseCase(string visibility)
    {
        // Happy path (RF-6.2): enabling is the same call as disabling, the handler copies the value it
        // received and never rewrites it, so the lifecycle decision of the record stays in the value the
        // consumer sent.
        var commonService = new FakeEnterpriseCommonService();

        var result = await EnterpriseHandlers.UpdateEnterpriseVisibilityAsync(
            commonService,
            RouteId,
            BodyWithEveryPropertyInformed(visibility: visibility));

        await HandlerTestSupport.AssertNoContentAsync(result);
        AssertOnlyUpdateAsyncVisibilityWasCalled(commonService);
        Assert.Equal(visibility, Assert.IsType<EnterpriseDTO>(commonService.LastVisibilityDto).Visibility);
    }

    [Fact]
    public async Task BodyWithoutVisibility_ReachesTheUseCaseWithoutVisibilityAndIsRejectedWithBadRequest()
    {
        // Boundary case RF-6.2 from the other side: the handler does not invent a default visibility when
        // the body omits it, it hands the dto with a null Visibility to the use case and lets the use case
        // report it, which the translation answers with 400 (RF-6.3, RF-8.3, CE-13). Filling a default
        // here would make this endpoint decide the lifecycle state of a record nobody enabled.
        var commonService = new FakeEnterpriseCommonService
        {
            ExceptionToThrow = new HexArchApplicationException(InvalidVisibilityMessage)
        };

        await HandlerTestSupport.AssertTranslationAsync<HexArchApplicationException>(
            () => EnterpriseHandlers.UpdateEnterpriseVisibilityAsync(
                commonService,
                RouteId,
                BodyWithEveryPropertyInformed(visibility: null)),
            StatusCodes.Status400BadRequest);

        AssertOnlyUpdateAsyncVisibilityWasCalled(commonService);
        Assert.Null(Assert.IsType<EnterpriseDTO>(commonService.LastVisibilityDto).Visibility);
    }

    [Theory]
    [InlineData("INVALIDA")]
    [InlineData("cualquierCosa")]
    [InlineData("enabled")]
    [InlineData("")]
    public async Task VisibilityOutsideTheAllowedValues_IsTranslatedToBadRequest(string visibility)
    {
        // Error case (CE-13, RF-6.3, RF-8.3): the use case rejects any Visibility other than ENABLED or
        // DISABLED with ApplicationException and the centralized translation answers 400, the code this
        // endpoint reserves for a rejected visibility. The handler performs no validation of its own: the
        // rejection is raised below it and it performs no modification.
        var commonService = new FakeEnterpriseCommonService
        {
            ExceptionToThrow = new HexArchApplicationException(InvalidVisibilityMessage)
        };

        await HandlerTestSupport.AssertTranslationAsync<HexArchApplicationException>(
            () => EnterpriseHandlers.UpdateEnterpriseVisibilityAsync(
                commonService,
                RouteId,
                BodyWithEveryPropertyInformed(visibility: visibility)),
            StatusCodes.Status400BadRequest);

        AssertOnlyUpdateAsyncVisibilityWasCalled(commonService);
        Assert.Equal(visibility, Assert.IsType<EnterpriseDTO>(commonService.LastVisibilityDto).Visibility);
    }

    [Fact]
    public async Task VisibilityOutsideTheAllowedValues_DeliversTheMessageInSpanish()
    {
        // Error case CE-16 / RF-8.6 on the rejected visibility path: ApiExceptionHandler writes
        // exception.Message verbatim, so the consumer reads the explanation of the use case in Spanish.
        var commonService = new FakeEnterpriseCommonService
        {
            ExceptionToThrow = new HexArchApplicationException(InvalidVisibilityMessage)
        };

        var exception = await Assert.ThrowsAnyAsync<HexArchApplicationException>(
            () => EnterpriseHandlers.UpdateEnterpriseVisibilityAsync(
                commonService,
                RouteId,
                BodyWithEveryPropertyInformed(visibility: "INVALIDA")));
        var response = await TestHttp.TranslateAsync(exception);

        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Equal(InvalidVisibilityMessage, response.Body);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task NonPositiveRouteId_IsTranslatedToBadRequestByTheUseCase(int routeId)
    {
        // Error case RF-8.3: a route identifier that binds as a number but is not greater than zero is
        // rejected by the use case with ApplicationException, and the translation answers 400. A handler
        // that delegated the visibility only and left the identifier out of the delegated dto would let
        // that value through unnoticed.
        var commonService = new FakeEnterpriseCommonService
        {
            ExceptionToThrow = new HexArchApplicationException(NonPositiveIdMessage)
        };

        await HandlerTestSupport.AssertTranslationAsync<HexArchApplicationException>(
            () => EnterpriseHandlers.UpdateEnterpriseVisibilityAsync(
                commonService,
                routeId,
                BodyWithEveryPropertyInformed()),
            StatusCodes.Status400BadRequest);

        AssertOnlyUpdateAsyncVisibilityWasCalled(commonService);
        Assert.Equal(routeId, Assert.IsType<EnterpriseDTO>(commonService.LastVisibilityDto).Id);
    }

    [Fact]
    public async Task NonExistentEnterprise_IsTranslatedToNotFoundAndIsNotRetried()
    {
        // Error case CE-3 / RF-6.4 / RF-8.1: the repository reports that the identifier matches no record
        // and the centralized translation answers 404, the only way this endpoint answers 404. The failure
        // is not retried, so no second modification of a non existing record is attempted, and the route
        // id was the one delegated before the failure.
        var commonService = new FakeEnterpriseCommonService
        {
            ExceptionToThrow = new KeyNotFoundException(
                "No se encontró una empresa con el identificador solicitado.")
        };

        await HandlerTestSupport.AssertTranslationAsync<KeyNotFoundException>(
            () => EnterpriseHandlers.UpdateEnterpriseVisibilityAsync(
                commonService,
                404,
                BodyWithEveryPropertyInformed()),
            StatusCodes.Status404NotFound);

        AssertOnlyUpdateAsyncVisibilityWasCalled(commonService);
        Assert.Equal(404, Assert.IsType<EnterpriseDTO>(commonService.LastVisibilityDto).Id);
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
            () => EnterpriseHandlers.UpdateEnterpriseVisibilityAsync(
                commonService,
                404,
                BodyWithEveryPropertyInformed()));
        var response = await TestHttp.TranslateAsync(exception);

        Assert.Equal(StatusCodes.Status404NotFound, response.StatusCode);
        Assert.Equal(
            "No se encontró una empresa con el identificador solicitado.",
            response.Body);
    }

    [Fact]
    public async Task BusinessFailureOfTheUseCase_IsTranslatedToBadRequest()
    {
        // Error case RF-8.4: a business failure of the application reaches the handler as HexArch
        // ApplicationException, which the centralized translation answers with 400, never with 204.
        var commonService = new FakeEnterpriseCommonService
        {
            ExceptionToThrow = new HexArchApplicationException(
                "No fue posible actualizar la visibilidad de la empresa.")
        };

        await HandlerTestSupport.AssertTranslationAsync<HexArchApplicationException>(
            () => EnterpriseHandlers.UpdateEnterpriseVisibilityAsync(
                commonService,
                RouteId,
                BodyWithEveryPropertyInformed()),
            StatusCodes.Status400BadRequest);

        AssertOnlyUpdateAsyncVisibilityWasCalled(commonService);
    }

    [Fact]
    public async Task AnyOtherBusinessFailure_IsTranslatedToBadRequest()
    {
        // Error case RF-8.4: any other business cause is translated to 400 by the safe fallback of the
        // centralized handler, so changing a visibility never leaks a 500 for it.
        var commonService = new FakeEnterpriseCommonService
        {
            ExceptionToThrow = new InvalidOperationException(
                "No fue posible actualizar la visibilidad de la empresa.")
        };

        await HandlerTestSupport.AssertTranslationAsync<InvalidOperationException>(
            () => EnterpriseHandlers.UpdateEnterpriseVisibilityAsync(
                commonService,
                RouteId,
                BodyWithEveryPropertyInformed()),
            StatusCodes.Status400BadRequest);

        AssertOnlyUpdateAsyncVisibilityWasCalled(commonService);
    }

    [Fact]
    public async Task BusinessFailureOfTheUseCase_DeliversTheMessageInSpanish()
    {
        // Error case CE-16 / RF-8.6 on the 400 path: the message reaches the consumer as written.
        var commonService = new FakeEnterpriseCommonService
        {
            ExceptionToThrow = new HexArchApplicationException(
                "No fue posible actualizar la visibilidad de la empresa.")
        };

        var exception = await Assert.ThrowsAnyAsync<HexArchApplicationException>(
            () => EnterpriseHandlers.UpdateEnterpriseVisibilityAsync(
                commonService,
                RouteId,
                BodyWithEveryPropertyInformed()));
        var response = await TestHttp.TranslateAsync(exception);

        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Equal("No fue posible actualizar la visibilidad de la empresa.", response.Body);
    }

    [Fact]
    public async Task AnyOtherBusinessFailure_DeliversTheMessageInSpanish()
    {
        // Error case CE-16 / RF-8.6 on the safe fallback path.
        var commonService = new FakeEnterpriseCommonService
        {
            ExceptionToThrow = new InvalidOperationException(
                "Se produjo un error inesperado al actualizar la visibilidad de la empresa.")
        };

        var exception = await Assert.ThrowsAnyAsync<InvalidOperationException>(
            () => EnterpriseHandlers.UpdateEnterpriseVisibilityAsync(
                commonService,
                RouteId,
                BodyWithEveryPropertyInformed()));
        var response = await TestHttp.TranslateAsync(exception);

        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Equal(
            "Se produjo un error inesperado al actualizar la visibilidad de la empresa.",
            response.Body);
    }
}