using HexArch.Application.DTOs;
using HexArch.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SosMVP.Handlers;
using test.Fakes;
using test.Support;

namespace test;

/// <summary>
/// Subject: <c>TypeHandlers.InsertTypeAsync</c> with a fake <c>ICommonService&lt;TypeDTO&gt;</c>.
/// Covers the three cases the plan asks for: happy path, boundary case and error case
/// (RF-2.1 to RF-2.6, RF-8.2, RF-8.5, CE-4, CE-11).
/// </summary>
public class TypeHandlersInsertTypeTests
{
    private static TypeDTO ValidDto(string? type = "Herramientas especiales", string? visibility = null)
    {
        return new TypeDTO
        {
            Id = null,
            Type = type,
            Visibility = visibility
        };
    }

    private static DbUpdateException UniqueIndexConflict()
    {
        var sqlServerViolation = new InvalidOperationException(
            "Violation of UNIQUE KEY constraint 'uq_tipo'. SQL error codes 2601 or 2627.");

        return new DbUpdateException(
            "No se pudo registrar el tipo porque ya existe un tipo con ese nombre.",
            sqlServerViolation);
    }

    [Fact]
    public async Task ValidType_IsAnsweredWithCreatedWithoutBodyAndWithoutLocation()
    {
        var commonService = new FakeTypeCommonService();

        var result = await TypeHandlers.InsertTypeAsync(commonService, ValidDto());

        await HandlerTestSupport.AssertCreatedWithoutLocationAsync(result);
    }

    [Fact]
    public async Task ValidType_IsDelegatedOnceToTheUseCaseWithoutContact()
    {
        var commonService = new FakeTypeCommonService();
        var dto = ValidDto();

        await TypeHandlers.InsertTypeAsync(commonService, dto);

        Assert.Equal(1, commonService.AddAsyncInfoCalls);
        Assert.Same(dto, commonService.LastAddedDto);
        Assert.Null(commonService.LastAddedContact);
        Assert.Equal(0, commonService.UpdateAsyncInfoCalls);
        Assert.Equal(0, commonService.UpdateAsyncVisibilityCalls);
    }

    [Fact]
    public async Task ValidType_ReachesTheUseCaseUnchanged()
    {
        var commonService = new FakeTypeCommonService();
        var dto = ValidDto();

        await TypeHandlers.InsertTypeAsync(commonService, dto);

        var received = Assert.IsType<TypeDTO>(commonService.LastAddedDto);
        Assert.Null(received.Id);
        Assert.Equal(dto.Type, received.Type);
        Assert.Equal(dto.Visibility, received.Visibility);
    }

    [Fact]
    public async Task VisibilitySentInTheBody_IsNotValidatedNorRewrittenByTheHandler()
    {
        // Boundary case: the handler neither validates nor decides Visibility. RF-2.2 assigns that
        // decision to the use case, and TypeRepository.AddAsyncInfo forces "ENABLED" when writing.
        var commonService = new FakeTypeCommonService();
        var dto = ValidDto(visibility: "DISABLED");

        var result = await TypeHandlers.InsertTypeAsync(commonService, dto);

        await HandlerTestSupport.AssertCreatedWithoutLocationAsync(result);
        Assert.Equal(1, commonService.AddAsyncInfoCalls);
        Assert.Equal("DISABLED", Assert.IsType<TypeDTO>(commonService.LastAddedDto).Visibility);
    }

    [Fact]
    public async Task DuplicateTypeOnTheUniqueIndex_IsTranslatedToInternalServerErrorAndIsNotRetried()
    {
        // Boundary case: persistence rejects the duplicate with SQL codes 2601/2627 on uq_tipo,
        // so the handler must not retry and must not write a second record (RF-2.6, RF-8.5, CE-4).
        var commonService = new FakeTypeCommonService { ExceptionToThrow = UniqueIndexConflict() };

        await HandlerTestSupport.AssertTranslationAsync<DbUpdateException>(
            () => TypeHandlers.InsertTypeAsync(commonService, ValidDto()),
            StatusCodes.Status500InternalServerError);

        Assert.Equal(1, commonService.AddAsyncInfoCalls);
    }

    [Fact]
    public async Task TypeOutsideTheAllowedLength_IsTranslatedToBadRequest()
    {
        // Error case: a Type shorter than five characters violates TypeEntity rules, which the
        // use case reports as EntityException; the handler lets it reach the global translation.
        var commonService = new FakeTypeCommonService
        {
            ExceptionToThrow = new EntityException("El tipo debe tener entre 5 y 50 caracteres.")
        };

        await HandlerTestSupport.AssertTranslationAsync<EntityException>(
            () => TypeHandlers.InsertTypeAsync(commonService, ValidDto(type: "ABC")),
            StatusCodes.Status400BadRequest);

        Assert.Equal(1, commonService.AddAsyncInfoCalls);
    }

    [Fact]
    public async Task EntityRuleViolation_DeliversTheMessageInSpanish()
    {
        var commonService = new FakeTypeCommonService
        {
            ExceptionToThrow = new EntityException("El tipo debe tener entre 5 y 50 caracteres.")
        };

        var exception = await Assert.ThrowsAnyAsync<EntityException>(
            () => TypeHandlers.InsertTypeAsync(commonService, ValidDto(type: "ABC")));
        var response = await TestHttp.TranslateAsync(exception);

        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Equal("El tipo debe tener entre 5 y 50 caracteres.", response.Body);
    }
}
