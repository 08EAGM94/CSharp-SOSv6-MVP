using HexArch.Application.DTOs;
using HexArch.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SosMVP.Handlers;
using test.Fakes;
using test.Support;

namespace test;

public class UserHandlersUpdateUserTests
{
    private static UserDTO BodyWithAnotherId()
    {
        return new UserDTO
        {
            Id = 99,
            Name = "Nombre Actualizado",
            Surname = "Apellido Actualizado Del Usuario",
            Nickname = "actualizado",
            Password = "clave-actualizada-123",
            Role = "user",
            Signature = "Firma actualizada"
        };
    }

    [Fact]
    public async Task ExistingUser_IsAnsweredWithNoContentAndWithoutBody()
    {
        var commonService = new FakeCommonService();

        var result = await UserHandlers.UpdateUserAsync(commonService, 7, BodyWithAnotherId());

        await HandlerTestSupport.AssertNoContentAsync(result);
        Assert.Equal(1, commonService.UpdateAsyncInfoCalls);
    }

    [Fact]
    public async Task RouteId_OverridesTheIdOfTheBody()
    {
        var commonService = new FakeCommonService();

        await UserHandlers.UpdateUserAsync(commonService, 7, BodyWithAnotherId());

        var received = Assert.IsType<UserDTO>(commonService.LastUpdatedDto);
        Assert.Equal(7, received.Id);
    }

    [Fact]
    public async Task TheRestOfTheBodyReachesTheUseCaseUnchanged()
    {
        var commonService = new FakeCommonService();
        var dto = BodyWithAnotherId();

        await UserHandlers.UpdateUserAsync(commonService, 7, dto);

        var received = Assert.IsType<UserDTO>(commonService.LastUpdatedDto);
        Assert.Equal(dto.Name, received.Name);
        Assert.Equal(dto.Surname, received.Surname);
        Assert.Equal(dto.Nickname, received.Nickname);
        Assert.Equal(dto.Password, received.Password);
        Assert.Equal(dto.Role, received.Role);
        Assert.Equal(dto.Signature, received.Signature);
    }

    [Fact]
    public async Task UnknownUser_IsTranslatedToNotFoundAndDoesNotWrite()
    {
        var commonService = new FakeCommonService
        {
            ExceptionToThrow = new KeyNotFoundException("No se pudo actualizar la información del usuario porque no existe un usuario con ese identificador.")
        };

        await HandlerTestSupport.AssertTranslationAsync<KeyNotFoundException>(
            () => UserHandlers.UpdateUserAsync(commonService, 404, BodyWithAnotherId()),
            StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task EntityRuleViolation_IsTranslatedToBadRequestAndDoesNotWrite()
    {
        var commonService = new FakeCommonService
        {
            ExceptionToThrow = new EntityException("El apodo debe tener entre 10 y 100 caracteres.")
        };

        await HandlerTestSupport.AssertTranslationAsync<EntityException>(
            () => UserHandlers.UpdateUserAsync(commonService, 7, BodyWithAnotherId()),
            StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task DuplicateAlias_IsTranslatedToInternalServerError()
    {
        var commonService = new FakeCommonService
        {
            ExceptionToThrow = new DbUpdateException("No se pudo actualizar el usuario porque el alias ya está registrado.")
        };

        await HandlerTestSupport.AssertTranslationAsync<DbUpdateException>(
            () => UserHandlers.UpdateUserAsync(commonService, 7, BodyWithAnotherId()),
            StatusCodes.Status500InternalServerError);
    }
}