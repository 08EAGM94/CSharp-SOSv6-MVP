using HexArch.Application.DTOs;
using Microsoft.AspNetCore.Http;
using SosMVP.Handlers;
using test.Fakes;
using test.Support;

namespace test;

public class UserHandlersLoginTests
{
    private static UserDTO Credentials(string nickname = "operador", string password = "clave-de-prueba-123")
    {
        return new UserDTO { Nickname = nickname, Password = password };
    }

    private static UserDTO AuthenticatedUser()
    {
        return new UserDTO
        {
            Id = 7,
            Name = "Nombre Completo",
            Surname = "Apellido Completo Del Usuario",
            Nickname = "operador",
            Role = "admin",
            Signature = "Firma del operador"
        };
    }

    [Fact]
    public async Task ValidCredentials_AreAnsweredWithOkAndANonEmptyToken()
    {
        var userService = new FakeUserService { LoginResult = AuthenticatedUser() };
        var tokenFactory = JwtTestTokens.Factory();

        var result = await UserHandlers.LoginAsync(userService, tokenFactory, Credentials());

        var response = await TestHttp.ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(HandlerTestSupport.ReadToken(response.Body)));
        Assert.Equal(1, userService.LoginCalls);
    }

    [Fact]
    public async Task CredentialsOfTheBody_AreDelegatedUnchanged()
    {
        var userService = new FakeUserService { LoginResult = AuthenticatedUser() };
        var dto = Credentials();

        await UserHandlers.LoginAsync(userService, JwtTestTokens.Factory(), dto);

        var received = Assert.IsType<UserDTO>(userService.LastLoginDto);
        Assert.Equal(dto.Nickname, received.Nickname);
        Assert.Equal(dto.Password, received.Password);
    }

    [Fact]
    public async Task IssuedToken_CarriesTheRoleOfTheAuthenticatedUser()
    {
        var userService = new FakeUserService { LoginResult = AuthenticatedUser() };
        var tokenFactory = JwtTestTokens.Factory();

        var result = await UserHandlers.LoginAsync(userService, tokenFactory, Credentials());

        var response = await TestHttp.ExecuteAsync(result);
        var token = HandlerTestSupport.ReadToken(response.Body);

        Assert.Equal("admin", JwtTestTokens.ReadClaim(token, "Role"));
    }

    [Fact]
    public async Task IssuedToken_NeverExposesAnyCredential()
    {
        var userService = new FakeUserService { LoginResult = AuthenticatedUser() };
        var dto = Credentials();
        dto.Visibility = "ENABLED";
        var tokenFactory = JwtTestTokens.Factory();

        var result = await UserHandlers.LoginAsync(userService, tokenFactory, dto);

        var response = await TestHttp.ExecuteAsync(result);
        var token = HandlerTestSupport.ReadToken(response.Body);

        Assert.Null(JwtTestTokens.ReadClaim(token, "Password"));
        Assert.Null(JwtTestTokens.ReadClaim(token, "ConfPwd"));
        Assert.Null(JwtTestTokens.ReadClaim(token, "Visibility"));
        Assert.DoesNotContain("clave-de-prueba-123", response.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task IssuedSession_ExpiresThirtyMinutesAfterItsEmission()
    {
        var userService = new FakeUserService { LoginResult = AuthenticatedUser() };
        var tokenFactory = JwtTestTokens.Factory();

        var result = await UserHandlers.LoginAsync(userService, tokenFactory, Credentials());

        var response = await TestHttp.ExecuteAsync(result);
        var token = HandlerTestSupport.ReadToken(response.Body);

        var validNow = await JwtTestTokens.ValidateAsync(tokenFactory, token);
        var expired = await JwtTestTokens.ValidateAsync(tokenFactory, token, DateTime.UtcNow.AddMinutes(JwtTestTokens.SessionMinutes + 1));

        Assert.True(validNow.IsValid);
        Assert.False(expired.IsValid);
    }

    [Fact]
    public async Task UnknownOrDisabledUser_IsTranslatedToNotFoundAndEmitsNoSession()
    {
        var userService = new FakeUserService
        {
            ExceptionToThrow = new KeyNotFoundException("No se encontró un usuario con la información solicitada.")
        };

        await HandlerTestSupport.AssertTranslationAsync<KeyNotFoundException>(
            () => UserHandlers.LoginAsync(userService, JwtTestTokens.Factory(), Credentials("deshabilitado")),
            StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task WrongPassword_IsTranslatedToBadRequestAndEmitsNoSession()
    {
        var userService = new FakeUserService
        {
            ExceptionToThrow = new Exception("La contraseña escrita no corresponde a la del usuario.")
        };

        await HandlerTestSupport.AssertTranslationAsync<Exception>(
            () => UserHandlers.LoginAsync(userService, JwtTestTokens.Factory(), Credentials(password: "clave-equivocada-123")),
            StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task PersistenceFailure_IsTranslatedToInternalServerError()
    {
        var userService = new FakeUserService
        {
            ExceptionToThrow = new Microsoft.EntityFrameworkCore.DbUpdateException("Fallo de infraestructura al autenticar.")
        };

        await HandlerTestSupport.AssertTranslationAsync<Microsoft.EntityFrameworkCore.DbUpdateException>(
            () => UserHandlers.LoginAsync(userService, JwtTestTokens.Factory(), Credentials()),
            StatusCodes.Status500InternalServerError);
    }
}