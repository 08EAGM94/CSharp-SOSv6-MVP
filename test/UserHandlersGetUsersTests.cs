using HexArch.Application.DTOs;
using HexArch.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using SosMVP.Handlers;
using test.Fakes;
using test.Support;

namespace test;

public class UserHandlersGetUsersTests
{
    [Fact]
    public async Task RegisteredUsers_AreAnsweredWithOkAndTheCollection()
    {
        var commonService = new FakeCommonService
        {
            AllResult =
            [
                new UserDTO { Id = 1, Name = "Nombre Completo", Nickname = "primero" },
                new UserDTO { Id = 2, Name = "Nombre Completo", Nickname = "segundo" }
            ]
        };

        var result = await UserHandlers.GetUsersAsync(commonService);

        var response = await TestHttp.ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.Equal(1, commonService.GetAsyncAllInfoCalls);
    }

    [Fact]
    public async Task NoEnabledUsers_AreAnsweredWithOkAndAnEmptyCollection()
    {
        var commonService = new FakeCommonService { AllResult = [] };

        var result = await UserHandlers.GetUsersAsync(commonService);

        var response = await TestHttp.ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.Equal("[]", response.Body);
    }

    [Fact]
    public async Task Listing_AsksTheUseCaseOnceAndWithoutAnyFilter()
    {
        var commonService = new FakeCommonService();

        await UserHandlers.GetUsersAsync(commonService);

        Assert.Equal(1, commonService.GetAsyncAllInfoCalls);
    }

    [Fact]
    public async Task Listing_CarriesNoCredentialValue()
    {
        var commonService = new FakeCommonService
        {
            AllResult = [new UserDTO { Id = 1, Name = "Nombre Completo", Nickname = "primero" }]
        };

        var result = await UserHandlers.GetUsersAsync(commonService);

        var response = await TestHttp.ExecuteAsync(result);
        var body = HandlerTestSupport.ReadJsonBody(response.Body);
        var first = body.EnumerateArray().First();

        Assert.Equal(JsonValueKind.Null, first.GetProperty("password").ValueKind);
        Assert.Equal(JsonValueKind.Null, first.GetProperty("confPwd").ValueKind);
        Assert.DoesNotContain("clave-de-prueba-123", response.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EntityRuleViolation_IsTranslatedToBadRequest()
    {
        var commonService = new FakeCommonService
        {
            ExceptionToThrow = new EntityException("El usuario no cumple las reglas de negocio.")
        };

        await HandlerTestSupport.AssertTranslationAsync<EntityException>(
            () => UserHandlers.GetUsersAsync(commonService),
            StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task PersistenceFailure_IsTranslatedToInternalServerError()
    {
        var commonService = new FakeCommonService
        {
            ExceptionToThrow = new DbUpdateException("Fallo de infraestructura al listar los usuarios.")
        };

        await HandlerTestSupport.AssertTranslationAsync<DbUpdateException>(
            () => UserHandlers.GetUsersAsync(commonService),
            StatusCodes.Status500InternalServerError);
    }
}