using HexArch.Application.DTOs;
using Microsoft.IdentityModel.Tokens;
using SosMVP.Security;
using test.Support;
using OptionsFactory = Microsoft.Extensions.Options.Options;

namespace test;

/// <summary>
/// Covers the claim closure list, the session lifetime and the role that travels in the token.
/// Known limitation: the real 401 is produced by the authentication middleware, so it cannot be
/// exercised here because the constitution only allows xUnit and forbids WebApplicationFactory.
/// What is verifiable without a host is the expiration moment: a token older than thirty minutes
/// fails validation, and that failure is the one the middleware turns into 401 (RF-1.2, CE-7, CE-19).
/// </summary>
public class JwtTokenFactoryTests
{
    private static readonly string[] ClosedClaimList =
    [
        JwtTokenFactory.IdClaimType,
        JwtTokenFactory.NameClaimType,
        JwtTokenFactory.SurnameClaimType,
        JwtTokenFactory.NicknameClaimType,
        JwtTokenFactory.RoleClaimType,
        JwtTokenFactory.SignatureClaimType
    ];

    private static readonly string[] NeverEmittedClaims =
    [
        "Password",
        "ConfPwd",
        "AdminPwd",
        "AdminNickname",
        "Visibility"
    ];

    [Fact]
    public void Payload_CarriesExactlyTheSixAllowedClaims()
    {
        var token = JwtTestTokens.Factory().CreateToken(JwtTestTokens.FullDto());

        var claimTypes = JwtTestTokens.ReadClaimTypes(token);

        Assert.Equal(ClosedClaimList.OrderBy(type => type, StringComparer.Ordinal),
            claimTypes.OrderBy(type => type, StringComparer.Ordinal));
    }

    [Fact]
    public void Payload_TransportsTheUserValuesOfTheReturnedDto()
    {
        var dto = JwtTestTokens.FullDto();

        var token = JwtTestTokens.Factory().CreateToken(dto);

        Assert.Equal("7", JwtTestTokens.ReadClaim(token, JwtTokenFactory.IdClaimType));
        Assert.Equal(dto.Name, JwtTestTokens.ReadClaim(token, JwtTokenFactory.NameClaimType));
        Assert.Equal(dto.Surname, JwtTestTokens.ReadClaim(token, JwtTokenFactory.SurnameClaimType));
        Assert.Equal(dto.Nickname, JwtTestTokens.ReadClaim(token, JwtTokenFactory.NicknameClaimType));
        Assert.Equal(dto.Role, JwtTestTokens.ReadClaim(token, JwtTokenFactory.RoleClaimType));
        Assert.Equal(dto.Signature, JwtTestTokens.ReadClaim(token, JwtTokenFactory.SignatureClaimType));
    }

    [Theory]
    [InlineData("Password")]
    [InlineData("ConfPwd")]
    [InlineData("AdminPwd")]
    [InlineData("AdminNickname")]
    [InlineData("Visibility")]
    public void Payload_NeverCarriesTheSensitiveClaim(string claimType)
    {
        var dto = JwtTestTokens.FullDto();
        dto.Password = "clave-de-prueba-123";
        dto.ConfPwd = "clave-de-prueba-123";
        dto.AdminPwd = "clave-del-admin-123";
        dto.AdminNickname = "adminPrincipal";
        dto.Visibility = "ENABLED";

        var token = JwtTestTokens.Factory().CreateToken(dto);

        Assert.Null(JwtTestTokens.ReadClaim(token, claimType));
    }

    [Fact]
    public void Payload_OmitsEveryClaimOutsideTheClosedList()
    {
        var token = JwtTestTokens.Factory().CreateToken(JwtTestTokens.FullDto());

        var userClaimTypes = JwtTestTokens.ReadUserClaims(token)
            .Select(claim => claim.Type)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Assert.All(userClaimTypes, type => Assert.Contains(type, ClosedClaimList));
        Assert.DoesNotContain(userClaimTypes, type => NeverEmittedClaims.Contains(type, StringComparer.Ordinal));
    }

    [Fact]
    public async Task Token_ExpiresThirtyMinutesAfterItsEmission()
    {
        var factory = JwtTestTokens.Factory();
        var token = factory.CreateToken(JwtTestTokens.FullDto());

        var result = await JwtTestTokens.ValidateAsync(factory, token);

        Assert.True(result.IsValid);

        var stillValidBeforeExpiry = await JwtTestTokens.ValidateAsync(
            factory,
            token,
            DateTime.UtcNow.AddMinutes(JwtTestTokens.SessionMinutes).AddSeconds(-1));

        Assert.True(stillValidBeforeExpiry.IsValid);

        var failedAtExpiry = await JwtTestTokens.ValidateAsync(
            factory,
            token,
            DateTime.UtcNow.AddMinutes(JwtTestTokens.SessionMinutes));

        Assert.False(failedAtExpiry.IsValid);
    }

    [Fact]
    public async Task Token_ExpiresWithoutAnyRenewalOnActivity()
    {
        var factory = JwtTestTokens.Factory();
        var token = factory.CreateToken(JwtTestTokens.FullDto());

        for (int minute = 1; minute < JwtTestTokens.SessionMinutes; minute++)
        {
            var activity = await JwtTestTokens.ValidateAsync(factory, token, DateTime.UtcNow.AddMinutes(minute));

            Assert.True(activity.IsValid);
        }

        var expired = await JwtTestTokens.ValidateAsync(
            factory,
            token,
            DateTime.UtcNow.AddMinutes(JwtTestTokens.SessionMinutes + 1));

        Assert.False(expired.IsValid);
    }

    [Fact]
    public async Task Token_FailsValidationOnceItIsOlderThanThirtyMinutes()
    {
        var factory = JwtTestTokens.Factory(sessionMinutes: 0);
        var token = factory.CreateToken(JwtTestTokens.FullDto());

        var result = await JwtTestTokens.ValidateAsync(factory, token);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void ClaimsList_OmitsThePropertiesWithoutContent()
    {
        var claims = JwtTokenFactory.BuildClaims(new UserDTO { Id = 3, Role = "user" });

        Assert.Equal(
            [JwtTokenFactory.IdClaimType, JwtTokenFactory.RoleClaimType],
            claims.Select(claim => claim.Type));
    }

    [Fact]
    public async Task TokenEmittedBeforeTheRoleChanges_KeepsTheIssuedRoleUntilItExpires()
    {
        var factory = JwtTestTokens.Factory();
        var dto = JwtTestTokens.FullDto();
        dto.Role = "admin";

        var token = factory.CreateToken(dto);

        dto.Role = "user";
        dto.Visibility = "DISABLED";

        var degraded = await JwtTestTokens.ValidateAsync(factory, token, DateTime.UtcNow.AddMinutes(JwtTestTokens.SessionMinutes - 1));

        Assert.True(degraded.IsValid);
        Assert.Equal("admin", JwtTestTokens.ReadClaim(token, JwtTokenFactory.RoleClaimType));
    }

    [Fact]
    public void ValidationParameters_VerifyTheIssuerTheAudienceTheSignatureAndTheLifetime()
    {
        var parameters = JwtTokenFactory.CreateValidationParameters(JwtTestTokens.Options());

        Assert.True(parameters.ValidateIssuer);
        Assert.Equal(JwtTestTokens.Issuer, parameters.ValidIssuer);
        Assert.True(parameters.ValidateAudience);
        Assert.Equal(JwtTestTokens.Audience, parameters.ValidAudience);
        Assert.True(parameters.ValidateIssuerSigningKey);
        Assert.True(parameters.ValidateLifetime);
        Assert.Equal(TimeSpan.Zero, parameters.ClockSkew);
        Assert.Equal(256, ((SymmetricSecurityKey)parameters.IssuerSigningKey!).KeySize);
    }

    [Fact]
    public async Task TokenSignedWithAnotherKey_IsRejected()
    {
        var factory = JwtTestTokens.Factory();
        var otherOptions = JwtTestTokens.Options();
        otherOptions.Key = "Otra-Clave-De-Firma-2026-MVP!!!1";

        var forgedToken = new JwtTokenFactory(OptionsFactory.Create(otherOptions)).CreateToken(JwtTestTokens.FullDto());

        var result = await JwtTestTokens.ValidateAsync(factory, forgedToken);

        Assert.False(result.IsValid);
    }
}