using System.Security.Claims;
using HexArch.Application.DTOs;
using Microsoft.AspNetCore.Http;
using SosMVP.Handlers;
using SosMVP.Security;
using test.Fakes;
using test.Support;

namespace test;

public class UserHandlersRefreshTests
{
    private static UserDTO FreshUser()
    {
        return new UserDTO
        {
            Id = 7,
            Name = "Nombre Completo",
            Surname = "Apellido Completo Del Usuario",
            Nickname = "operador",
            Role = "user",
            Signature = "Firma del operador"
        };
    }

    private static ClaimsPrincipal Session(string? rawId, string? role = "admin")
    {
        var claims = new List<Claim>();

        if (rawId is not null)
        {
            claims.Add(new Claim(JwtTokenFactory.IdClaimType, rawId));
        }

        if (role is not null)
        {
            claims.Add(new Claim(JwtTokenFactory.RoleClaimType, role));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "TestSession"));
    }

    [Fact]
    public async Task ValidSession_IsAnsweredWithOkAndANonEmptyTokenForTheUserOfTheClaim()
    {
        var commonService = new FakeCommonService { InfoResult = FreshUser() };

        var result = await UserHandlers.RefreshAsync(commonService, Session("7"), JwtTestTokens.Factory());

        var response = await TestHttp.ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(HandlerTestSupport.ReadToken(response.Body)));
        Assert.Equal(7, Assert.IsType<UserDTO>(commonService.LastRequestedDto).Id);
    }

    [Fact]
    public async Task NewToken_CarriesTheRoleOfTheStoreNotTheOneOfTheIncomingSession()
    {
        var commonService = new FakeCommonService { InfoResult = FreshUser() };

        var result = await UserHandlers.RefreshAsync(commonService, Session("7", role: "admin"), JwtTestTokens.Factory());

        var response = await TestHttp.ExecuteAsync(result);
        var token = HandlerTestSupport.ReadToken(response.Body);

        Assert.Equal("user", JwtTestTokens.ReadClaim(token, "Role"));
    }

    [Fact]
    public async Task NewSession_ExpiresThirtyMinutesAfterItsEmission()
    {
        var commonService = new FakeCommonService { InfoResult = FreshUser() };
        var tokenFactory = JwtTestTokens.Factory();

        var result = await UserHandlers.RefreshAsync(commonService, Session("7"), tokenFactory);

        var response = await TestHttp.ExecuteAsync(result);
        var token = HandlerTestSupport.ReadToken(response.Body);

        var validNow = await JwtTestTokens.ValidateAsync(tokenFactory, token);
        var expired = await JwtTestTokens.ValidateAsync(tokenFactory, token, DateTime.UtcNow.AddMinutes(JwtTestTokens.SessionMinutes + 1));

        Assert.True(validNow.IsValid);
        Assert.False(expired.IsValid);
    }

    [Fact]
    public async Task NewToken_RespectsTheClosingListOfClaims()
    {
        var stored = FreshUser();
        stored.Password = "clave-que-no-debe-propagarse";
        stored.ConfPwd = "clave-que-no-debe-propagarse";
        stored.Visibility = "ENABLED";
        var commonService = new FakeCommonService { InfoResult = stored };

        var result = await UserHandlers.RefreshAsync(commonService, Session("7"), JwtTestTokens.Factory());

        var response = await TestHttp.ExecuteAsync(result);
        var token = HandlerTestSupport.ReadToken(response.Body);
        var claimTypes = JwtTestTokens.ReadClaimTypes(token);

        Assert.Equal(
            new[] { "Id", "Name", "Surname", "Nickname", "Role", "Signature" }.OrderBy(t => t, StringComparer.Ordinal),
            claimTypes.OrderBy(t => t, StringComparer.Ordinal));
        Assert.Null(JwtTestTokens.ReadClaim(token, "Password"));
        Assert.Null(JwtTestTokens.ReadClaim(token, "ConfPwd"));
        Assert.Null(JwtTestTokens.ReadClaim(token, "Visibility"));
    }

    [Fact]
    public async Task SessionWithoutIdClaim_IsAnsweredWithUnauthorizedAndDoesNotConsultTheStore()
    {
        var commonService = new FakeCommonService { InfoResult = FreshUser() };

        var result = await UserHandlers.RefreshAsync(commonService, Session(null), JwtTestTokens.Factory());

        var response = await TestHttp.ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status401Unauthorized, response.StatusCode);
        Assert.Equal(0, commonService.GetAsyncInfoCalls);
    }

    [Fact]
    public async Task SessionWithNonNumericIdClaim_IsAnsweredWithUnauthorizedAndDoesNotConsultTheStore()
    {
        var commonService = new FakeCommonService { InfoResult = FreshUser() };

        var result = await UserHandlers.RefreshAsync(commonService, Session("abc"), JwtTestTokens.Factory());

        var response = await TestHttp.ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status401Unauthorized, response.StatusCode);
        Assert.Equal(0, commonService.GetAsyncInfoCalls);
    }

    [Fact]
    public async Task DisabledUser_IsTranslatedToNotFoundAndEmitsNoSession()
    {
        var stored = FreshUser();
        stored.Visibility = "DISABLED";
        var commonService = new FakeCommonService { InfoResult = stored };

        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => UserHandlers.RefreshAsync(commonService, Session("7"), JwtTestTokens.Factory()));

        var response = await TestHttp.TranslateAsync(exception);

        Assert.Equal(StatusCodes.Status404NotFound, response.StatusCode);
        Assert.Equal(1, commonService.GetAsyncInfoCalls);
    }

    [Fact]
    public async Task DeletedUser_IsTranslatedToNotFound()
    {
        var commonService = new FakeCommonService
        {
            ExceptionToThrow = new KeyNotFoundException("No se encontró un usuario con la información solicitada.")
        };

        await HandlerTestSupport.AssertTranslationAsync<KeyNotFoundException>(
            () => UserHandlers.RefreshAsync(commonService, Session("7"), JwtTestTokens.Factory()),
            StatusCodes.Status404NotFound);
    }
}
