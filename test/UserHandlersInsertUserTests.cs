using HexArch.Application.DTOs;
using HexArch.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SosMVP.Handlers;
using test.Fakes;
using test.Support;
using HexArchApplicationException = HexArch.Domain.Exceptions.ApplicationException;

namespace test;

public class UserHandlersInsertUserTests
{
    private static UserDTO ValidDto(string role = "admin")
    {
        return new UserDTO
        {
            Name = "Nombre Completo",
            Surname = "Apellido Completo Del Usuario",
            Nickname = "nuevoUsuario",
            Password = "clave-de-prueba-123",
            Role = role,
            Signature = "Firma del usuario"
        };
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("user")]
    public async Task AdminRole_AnswersCreatedWithoutBodyAndWithoutLocation(string role)
    {
        var commonService = new FakeCommonService();
        var dto = ValidDto(role);

        var result = await UserHandlers.InsertUserAsync(commonService, dto);

        await HandlerTestSupport.AssertCreatedWithoutLocationAsync(result);
        Assert.Equal(1, commonService.AddAsyncInfoCalls);
        Assert.Same(dto, commonService.LastAddedDto);
        Assert.Null(commonService.LastAddedContact);
    }

    [Fact]
    public async Task ValidRole_DelegatesTheWholeDtoToTheUseCase()
    {
        var commonService = new FakeCommonService();
        var dto = ValidDto("user");

        await UserHandlers.InsertUserAsync(commonService, dto);

        var received = Assert.IsType<UserDTO>(commonService.LastAddedDto);
        Assert.Equal(dto.Name, received.Name);
        Assert.Equal(dto.Surname, received.Surname);
        Assert.Equal(dto.Nickname, received.Nickname);
        Assert.Equal(dto.Password, received.Password);
        Assert.Equal(dto.Role, received.Role);
        Assert.Equal(dto.Signature, received.Signature);
    }

    [Theory]
    [InlineData("root")]
    [InlineData("Admin")]
    [InlineData("ADMIN")]
    [InlineData("administrador")]
    [InlineData("")]
    [InlineData(null)]
    public async Task RoleOutsideTheAllowedList_AnswersBadRequestAndDoesNotInvokeTheUseCase(string? role)
    {
        var commonService = new FakeCommonService();

        var result = await UserHandlers.InsertUserAsync(commonService, ValidDto(role!));

        var response = await TestHttp.ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Equal(0, commonService.AddAsyncInfoCalls);
    }

    [Fact]
    public async Task RejectedRole_DeliversTheMessageInSpanish()
    {
        var commonService = new FakeCommonService();

        var result = await UserHandlers.InsertUserAsync(commonService, ValidDto("root"));

        await HandlerTestSupport.AssertBodyIsInSpanishAsync(result);
    }

    [Fact]
    public async Task EntityRuleViolation_IsTranslatedToBadRequest()
    {
        var commonService = new FakeCommonService
        {
            ExceptionToThrow = new EntityException("El apodo debe tener entre 10 y 100 caracteres.")
        };

        await HandlerTestSupport.AssertTranslationAsync<EntityException>(
            () => UserHandlers.InsertUserAsync(commonService, ValidDto()),
            StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task ApplicationRuleViolation_IsTranslatedToBadRequest()
    {
        var commonService = new FakeCommonService
        {
            ExceptionToThrow = new HexArchApplicationException("El usuario ya está registrado.")
        };

        await HandlerTestSupport.AssertTranslationAsync<HexArchApplicationException>(
            () => UserHandlers.InsertUserAsync(commonService, ValidDto()),
            StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task DuplicateAlias_IsTranslatedToInternalServerError()
    {
        var commonService = new FakeCommonService
        {
            ExceptionToThrow = new DbUpdateException("No se pudo guardar el usuario porque el alias ya está registrado.")
        };

        await HandlerTestSupport.AssertTranslationAsync<DbUpdateException>(
            () => UserHandlers.InsertUserAsync(commonService, ValidDto()),
            StatusCodes.Status500InternalServerError);
    }

    [Fact]
    public async Task RecordNotFound_IsTranslatedToNotFound()
    {
        var commonService = new FakeCommonService
        {
            ExceptionToThrow = new KeyNotFoundException("No se encontró un usuario con la información solicitada.")
        };

        await HandlerTestSupport.AssertTranslationAsync<KeyNotFoundException>(
            () => UserHandlers.InsertUserAsync(commonService, ValidDto()),
            StatusCodes.Status404NotFound);
    }
}