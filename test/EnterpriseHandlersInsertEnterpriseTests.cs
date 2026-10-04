using HexArch.Application.DTOs;
using HexArch.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SosMVP.Handlers;
using test.Fakes;
using test.Support;

namespace test;

/// <summary>
/// Subject: <c>EnterpriseHandlers.InsertEnterpriseAsync</c> with a fake <c>ICommonService&lt;EnterpriseDTO&gt;</c>.
/// Covers the cases the plan asks for: happy path, boundary cases and error cases
/// (RF-2.1 to RF-2.9, RF-8.2, RF-8.5, RF-8.6, CE-4, CE-11, CE-12, CE-16, CE-17, CE-18).
/// </summary>
public class EnterpriseHandlersInsertEnterpriseTests
{
    private static EnterpriseDTO ValidDto(string? visibility = null)
    {
        return new EnterpriseDTO
        {
            Id = null,
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

    private static ContactDTO ValidContact()
    {
        return new ContactDTO
        {
            Id = null,
            EnterpriseId = null,
            FullName = "María de los Ángeles Ramírez Soto",
            Visibility = null
        };
    }

    /// <summary>
    /// The body of POST /enterprise/ is a single object (RF-2.1, RF-2.2): the enterprise is required
    /// and the contact is the optional part of that same object.
    /// </summary>
    private static InsertEnterpriseRequest Body(EnterpriseDTO enterprise, ContactDTO? contact = null)
    {
        return new InsertEnterpriseRequest(enterprise, contact);
    }

    private static DbUpdateException UniqueIndexConflict()
    {
        var sqlServerViolation = new InvalidOperationException(
            "Violation of UNIQUE KEY constraint 'uq_empresa'. SQL error codes 2601 or 2627.");

        return new DbUpdateException(
            "No se pudo registrar la empresa porque ya existe una empresa con esos datos.",
            sqlServerViolation);
    }

    /// <summary>
    /// Insert must reach the use case through <c>AddAsyncInfo</c> only once, and must not touch
    /// any other member of the primary port (RF-2.1).
    /// </summary>
    private static void AssertOnlyAddAsyncInfoWasCalled(FakeEnterpriseCommonService commonService)
    {
        Assert.Equal(1, commonService.AddAsyncInfoCalls);
        Assert.Equal(0, commonService.GetAsyncInfoCalls);
        Assert.Equal(0, commonService.GetAsyncAllInfoCalls);
        Assert.Equal(0, commonService.UpdateAsyncInfoCalls);
        Assert.Equal(0, commonService.UpdateAsyncVisibilityCalls);
    }

    [Fact]
    public async Task ValidEnterprise_IsAnsweredWithCreatedWithoutBodyAndWithoutLocation()
    {
        // Happy path: RF-2.1 registers the enterprise and RF-2.6/RF-2.7 answer 201 with neither
        // body nor Location header, because the insert produces no object of return.
        var commonService = new FakeEnterpriseCommonService();

        var result = await EnterpriseHandlers.InsertEnterpriseAsync(commonService, Body(ValidDto()));

        await HandlerTestSupport.AssertCreatedWithoutLocationAsync(result);
    }

    [Fact]
    public async Task ValidEnterprise_IsDelegatedOnceToTheUseCaseWithContactWhenFullNameProvided()
    {
        // Happy path with the optional contact: RF-2.2 also creates the associated contact,
        // so the handler delegates the received ContactDTO to the use case (RF-2.1).
        var commonService = new FakeEnterpriseCommonService();
        var dto = ValidDto();
        var contact = ValidContact();

        await EnterpriseHandlers.InsertEnterpriseAsync(commonService, Body(dto, contact));

        AssertOnlyAddAsyncInfoWasCalled(commonService);
        Assert.Same(dto, commonService.LastAddedDto);

        var receivedContact = Assert.IsType<ContactDTO>(commonService.LastAddedContact);
        Assert.Same(contact, receivedContact);
        Assert.Equal(contact.FullName, receivedContact.FullName);
    }

    [Fact]
    public async Task ValidEnterprise_IsDelegatedOnceToTheUseCaseWithoutContactWhenFullNameMissing()
    {
        // The body carries a contact without FullName, so no contact may reach the use case
        // (RF-2.9) and the enterprise is delegated alone (RF-2.1).
        var commonService = new FakeEnterpriseCommonService();
        var dto = ValidDto();

        await EnterpriseHandlers.InsertEnterpriseAsync(
            commonService,
            Body(dto, new ContactDTO { FullName = null }));

        AssertOnlyAddAsyncInfoWasCalled(commonService);
        Assert.Same(dto, commonService.LastAddedDto);
        Assert.Null(commonService.LastAddedContact);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ContactDtoWithEmptyFullName_IsIgnored_OnlyEnterpriseCreated(string? fullName)
    {
        // Boundary case CE-18 / RF-2.9: a null, empty or blank FullName makes the handler ignore
        // the contact, otherwise the mapper would build a ContactEntity and the setter would
        // answer 400 instead of creating only the enterprise.
        var commonService = new FakeEnterpriseCommonService();

        var result = await EnterpriseHandlers.InsertEnterpriseAsync(
            commonService,
            Body(ValidDto(), new ContactDTO { FullName = fullName }));

        await HandlerTestSupport.AssertCreatedWithoutLocationAsync(result);
        AssertOnlyAddAsyncInfoWasCalled(commonService);
        Assert.Null(commonService.LastAddedContact);
    }

    [Theory]
    [InlineData("ENABLED")]
    [InlineData("DISABLED")]
    [InlineData("VALOR_NO_ADMITIDO")]
    public async Task VisibilitySentInBody_IsNotValidatedNorRewrittenByHandler(string visibility)
    {
        // Boundary case RF-2.3: forcing Visibility to "ENABLED" belongs to the use case, so the
        // handler neither rejects a value outside ENABLED/DISABLED nor overwrites a valid one.
        var commonService = new FakeEnterpriseCommonService();

        var result = await EnterpriseHandlers.InsertEnterpriseAsync(commonService, Body(ValidDto(visibility)));

        await HandlerTestSupport.AssertCreatedWithoutLocationAsync(result);
        AssertOnlyAddAsyncInfoWasCalled(commonService);
        Assert.Equal(visibility, Assert.IsType<EnterpriseDTO>(commonService.LastAddedDto).Visibility);
    }

    [Fact]
    public async Task DuplicateEnterpriseOnUniqueIndex_IsTranslatedToInternalServerErrorAndIsNotRetried()
    {
        // Boundary case CE-4 / RF-2.8: the persistence engine rejects the duplicate with SQL codes
        // 2601/2627, which RF-8.5 translates to 500. The handler must not retry, so no second
        // record is attempted.
        var commonService = new FakeEnterpriseCommonService { ExceptionToThrow = UniqueIndexConflict() };

        await HandlerTestSupport.AssertTranslationAsync<DbUpdateException>(
            () => EnterpriseHandlers.InsertEnterpriseAsync(commonService, Body(ValidDto())),
            StatusCodes.Status500InternalServerError);

        Assert.Equal(1, commonService.AddAsyncInfoCalls);
    }

    [Theory]
    [InlineData(151)]
    public async Task EnterpriseOutsideAllowedLengths_IsTranslatedToBadRequest(int commercialNameLength)
    {
        // Error case CE-11 / RF-2.4: CommercialName accepts at most 150 characters, so EnterpriseEntity
        // reports the violation as EntityException, which RF-8.2 translates to 400 with no record written.
        var commonService = new FakeEnterpriseCommonService
        {
            ExceptionToThrow = new EntityException("El nombre comercial debe tener un máximo de 150 caracteres.")
        };
        var dto = ValidDto();
        dto.CommercialName = new string('A', commercialNameLength);

        await HandlerTestSupport.AssertTranslationAsync<EntityException>(
            () => EnterpriseHandlers.InsertEnterpriseAsync(commonService, Body(dto)),
            StatusCodes.Status400BadRequest);

        AssertOnlyAddAsyncInfoWasCalled(commonService);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(151)]
    public async Task ContactFullNameOutsideAllowedLength_IsTranslatedToBadRequest(int fullNameLength)
    {
        // Error case CE-12 / RF-2.5: FullName accepts between 10 and 150 characters. The contact is
        // ignored only when FullName is null/blank, so a too short or too long FullName reaches the
        // use case, ContactEntity reports EntityException and neither enterprise nor contact is created.
        var commonService = new FakeEnterpriseCommonService
        {
            ExceptionToThrow = new EntityException("El nombre completo debe tener entre 10 y 150 caracteres.")
        };
        var contact = ValidContact();
        contact.FullName = new string('A', fullNameLength);

        await HandlerTestSupport.AssertTranslationAsync<EntityException>(
            () => EnterpriseHandlers.InsertEnterpriseAsync(commonService, Body(ValidDto(), contact)),
            StatusCodes.Status400BadRequest);

        AssertOnlyAddAsyncInfoWasCalled(commonService);
        Assert.NotNull(commonService.LastAddedContact);
    }

    [Fact]
    public async Task UseCaseFailureWhileCreatingTheContact_IsTranslatedToInternalServerErrorAndIsNotRetried()
    {
        // Boundary case CE-17: the enterprise and its contact are written in one atomic transaction,
        // so a persistence failure while inserting the contact has to surface as 500 (RF-8.5) reaching
        // the consumer. The rollback itself is a guarantee of the persistence layer (repository is
        // untouchable, Constitution p5) and is out of the reach of a fake based suite, so what is
        // asserted here is the observable part: the failure propagates, and AddAsyncInfo is called
        // exactly once, with no retry that could duplicate the enterprise.
        var commonService = new FakeEnterpriseCommonService
        {
            ExceptionToThrow = new DbUpdateException(
                "No se pudo registrar el contacto asociado a la empresa.")
        };

        await HandlerTestSupport.AssertTranslationAsync<DbUpdateException>(
            () => EnterpriseHandlers.InsertEnterpriseAsync(commonService, Body(ValidDto(), ValidContact())),
            StatusCodes.Status500InternalServerError);

        Assert.Equal(1, commonService.AddAsyncInfoCalls);
        Assert.NotNull(commonService.LastAddedContact);
    }

    [Fact]
    public async Task EntityRuleViolation_DeliversMessageInSpanish()
    {
        // Error case CE-16 / RF-8.6: ApiExceptionHandler writes exception.Message verbatim, so the
        // message the consumer reads must already be written in Spanish.
        var commonService = new FakeEnterpriseCommonService
        {
            ExceptionToThrow = new EntityException("El nombre comercial debe tener un máximo de 150 caracteres.")
        };

        var exception = await Assert.ThrowsAnyAsync<EntityException>(
            () => EnterpriseHandlers.InsertEnterpriseAsync(commonService, Body(ValidDto())));
        var response = await TestHttp.TranslateAsync(exception);

        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Equal("El nombre comercial debe tener un máximo de 150 caracteres.", response.Body);
    }
}