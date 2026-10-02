using HexArch.Application.DTOs;
using HexArch.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using System.Text.Json;
using SosMVP.Handlers;
using test.Fakes;
using test.Support;

namespace test;

public class UserHandlersGetUserTests
{
    private static UserDTO StoredUser()
    {
        return new UserDTO
        {
            Id = 7,
            Name = "Nombre Completo",
            Surname = "Apellido Completo Del Usuario",
            Nickname = "consultado",
            Role = "user",
            Signature = "Firma del usuario"
        };
    }

    [Fact]
    public async Task ExistingUser_AnswersOkWithTheDtoReturnedByTheUseCase()
    {
        var commonService = new FakeCommonService { InfoResult = StoredUser() };

        var result = await UserHandlers.GetUserAsync(commonService, 7);

        var response = await TestHttp.ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);

        var body = HandlerTestSupport.ReadJsonBody(response.Body);
        Assert.Equal(7, body.GetProperty("id").GetInt32());
        Assert.Equal("consultado", body.GetProperty("nickname").GetString());
        Assert.Equal("user", body.GetProperty("role").GetString());
        Assert.Equal(1, commonService.GetAsyncInfoCalls);
    }

    [Fact]
    public async Task RouteId_OverridesEverythingTheRequestBodyCarried()
    {
        var commonService = new FakeCommonService { InfoResult = StoredUser() };

        await UserHandlers.GetUserAsync(commonService, 7);

        var requested = Assert.IsType<UserDTO>(commonService.LastRequestedDto);
        Assert.Equal(7, requested.Id);
    }

    [Fact]
    public async Task OnlyTheRouteIdReachesTheUseCase()
    {
        var commonService = new FakeCommonService { InfoResult = StoredUser() };

        await UserHandlers.GetUserAsync(commonService, 7);

        var requested = Assert.IsType<UserDTO>(commonService.LastRequestedDto);
        Assert.Null(requested.Name);
        Assert.Null(requested.Surname);
        Assert.Null(requested.Nickname);
        Assert.Null(requested.Password);
        Assert.Null(requested.Role);
        Assert.Null(requested.Signature);
        Assert.Null(requested.Visibility);
    }

    [Fact]
    public async Task AnsweredBody_CarriesNoCredentialValue()
    {
        var commonService = new FakeCommonService { InfoResult = StoredUser() };

        var result = await UserHandlers.GetUserAsync(commonService, 7);

        var response = await TestHttp.ExecuteAsync(result);
        var body = HandlerTestSupport.ReadJsonBody(response.Body);

        Assert.Equal(JsonValueKind.Null, body.GetProperty("password").ValueKind);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("confPwd").ValueKind);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("adminPwd").ValueKind);
        Assert.DoesNotContain("clave-de-prueba-123", response.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnknownUser_IsTranslatedToNotFound()
    {
        var commonService = new FakeCommonService
        {
            ExceptionToThrow = new KeyNotFoundException("No se encontró un usuario con la información solicitada.")
        };

        await HandlerTestSupport.AssertTranslationAsync<KeyNotFoundException>(
            () => UserHandlers.GetUserAsync(commonService, 404),
            StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task EntityRuleViolation_IsTranslatedToBadRequest()
    {
        var commonService = new FakeCommonService
        {
            ExceptionToThrow = new EntityException("El identificador debe ser un número mayor que cero.")
        };

        await HandlerTestSupport.AssertTranslationAsync<EntityException>(
            () => UserHandlers.GetUserAsync(commonService, 0),
            StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task AnyOtherBusinessFailure_IsTranslatedToBadRequest()
    {
        var commonService = new FakeCommonService
        {
            ExceptionToThrow = new Exception("No se pudo consultar la información del usuario.")
        };

        await HandlerTestSupport.AssertTranslationAsync<Exception>(
            () => UserHandlers.GetUserAsync(commonService, 7),
            StatusCodes.Status400BadRequest);
    }
}