using HexArch.Application.DTOs;
using Microsoft.AspNetCore.Http;
using SosMVP.Handlers;
using test.Fakes;
using test.Support;

namespace test;

public class UserHandlersAdminVerificationTests
{
    private static UserDTO AdminCredentials(string nickname = "adminPrincipal", string password = "clave-del-admin-123")
    {
        return new UserDTO { AdminNickname = nickname, AdminPwd = password };
    }

    [Fact]
    public async Task MatchingPassword_AreAnsweredWithOkAndConfirmedTrue()
    {
        var userService = new FakeUserService { AdminPwdConfirmationResult = true };

        var result = UserHandlers.AdminVerificationAsync(userService, AdminCredentials());

        var response = await TestHttp.ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.Equal("{\"confirmed\":true}", response.Body);
        Assert.Equal(1, userService.AdminPwdConfirmationCalls);
    }

    [Fact]
    public async Task CredentialsOfTheBody_AreDelegatedUnchanged()
    {
        var userService = new FakeUserService { AdminPwdConfirmationResult = true };
        var dto = AdminCredentials();

        UserHandlers.AdminVerificationAsync(userService, dto);

        var received = Assert.IsType<UserDTO>(userService.LastAdminPwdDto);
        Assert.Same(dto, received);
        Assert.Equal(dto.AdminNickname, received.AdminNickname);
        Assert.Equal(dto.AdminPwd, received.AdminPwd);
    }

    [Fact]
    public async Task Confirmation_ContrastsTheAdminPasswordOfTheBody()
    {
        var userService = new FakeUserService { AdminPwdConfirmationResult = true };
        var dto = AdminCredentials();
        dto.Password = "clave-que-no-debe-usarse-123";
        dto.ConfPwd = "clave-que-no-debe-usarse-123";

        UserHandlers.AdminVerificationAsync(userService, dto);

        var received = Assert.IsType<UserDTO>(userService.LastAdminPwdDto);
        Assert.Equal("adminPrincipal", received.AdminNickname);
        Assert.Equal("clave-del-admin-123", received.AdminPwd);
    }

    [Fact]
    public async Task MismatchingPassword_AreAnsweredWithForbidden()
    {
        var userService = new FakeUserService { AdminPwdConfirmationResult = false };

        var result = UserHandlers.AdminVerificationAsync(userService, AdminCredentials(password: "clave-equivocada-123"));

        var response = await TestHttp.ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status403Forbidden, response.StatusCode);
        Assert.Equal(1, userService.AdminPwdConfirmationCalls);
    }

    [Fact]
    public async Task MismatchingPassword_DeliversTheMessageInSpanish()
    {
        var userService = new FakeUserService { AdminPwdConfirmationResult = false };

        var result = UserHandlers.AdminVerificationAsync(userService, AdminCredentials(password: "clave-equivocada-123"));

        await HandlerTestSupport.AssertBodyIsInSpanishAsync(result);
    }

    [Theory]
    [InlineData(null, "clave-del-admin-123")]
    [InlineData("", "clave-del-admin-123")]
    [InlineData("   ", "clave-del-admin-123")]
    [InlineData("adminPrincipal", null)]
    [InlineData("adminPrincipal", "")]
    [InlineData("adminPrincipal", "   ")]
    public async Task MissingCredentials_AreAnsweredWithBadRequestAndDoNotInvokeTheUseCase(string? nickname, string? password)
    {
        var userService = new FakeUserService { AdminPwdConfirmationResult = true };
        var dto = new UserDTO { AdminNickname = nickname, AdminPwd = password };

        var result = UserHandlers.AdminVerificationAsync(userService, dto);

        var response = await TestHttp.ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Equal(0, userService.AdminPwdConfirmationCalls);
    }

    [Fact]
    public async Task MissingCredentials_DeliverTheMessageInSpanish()
    {
        var userService = new FakeUserService();

        var result = UserHandlers.AdminVerificationAsync(userService, new UserDTO { AdminNickname = "adminPrincipal" });

        await HandlerTestSupport.AssertBodyIsInSpanishAsync(result);
    }

    [Fact]
    public async Task UnknownAdministrator_IsTranslatedToNotFound()
    {
        var userService = new FakeUserService
        {
            ExceptionToThrow = new KeyNotFoundException("No se encontró un usuario con la información solicitada.")
        };

        await HandlerTestSupport.AssertTranslationAsync<KeyNotFoundException>(
            () => Task.FromResult(UserHandlers.AdminVerificationAsync(userService, AdminCredentials(nickname: "administradorInexistente"))),
            StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task PersistenceFailure_IsTranslatedToInternalServerError()
    {
        var userService = new FakeUserService
        {
            ExceptionToThrow = new Microsoft.EntityFrameworkCore.DbUpdateException("Fallo de infraestructura al verificar al administrador.")
        };

        await HandlerTestSupport.AssertTranslationAsync<Microsoft.EntityFrameworkCore.DbUpdateException>(
            () => Task.FromResult(UserHandlers.AdminVerificationAsync(userService, AdminCredentials())),
            StatusCodes.Status500InternalServerError);
    }
}