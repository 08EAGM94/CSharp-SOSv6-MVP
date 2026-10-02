using System.Security.Claims;
using HexArch.Application.DTOs;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SosMVP.Security;
using OptionsFactory = Microsoft.Extensions.Options.Options;

namespace test.Support;

public static class JwtTestTokens
{
    public const string Issuer = "SosMVP.Tests";
    public const string Audience = "SosMVP.TestClients";
    public const string Key = "Sosv6-Jwt-Signing-Key-2026-MVP!!";
    public const int SessionMinutes = 30;

    private static readonly HashSet<string> ProtocolClaimTypes = new(StringComparer.Ordinal)
    {
        "aud",
        "exp",
        "iat",
        "iss",
        "nbf"
    };

    public static JwtOptions Options(int sessionMinutes = SessionMinutes)
    {
        return BuildOptions(sessionMinutes);
    }

    public static JwtTokenFactory Factory(int sessionMinutes = SessionMinutes)
    {
        return new JwtTokenFactory(OptionsFactory.Create(BuildOptions(sessionMinutes)));
    }

    private static JwtOptions BuildOptions(int sessionMinutes = SessionMinutes)
    {
        return new JwtOptions
        {
            Issuer = Issuer,
            Audience = Audience,
            Key = Key,
            SessionMinutes = sessionMinutes
        };
    }

    public static UserDTO FullDto()
    {
        return new UserDTO
        {
            Id = 7,
            Name = "Nombre Completo",
            Surname = "Apellido Completo Del Usuario",
            Nickname = "administrador",
            Password = "clave-de-prueba-123",
            Role = "admin",
            Signature = "Firma del administrador",
            Visibility = "ENABLED",
            ConfPwd = "clave-de-prueba-123",
            AdminNickname = "adminPrincipal",
            AdminPwd = "clave-del-admin-123"
        };
    }

    public static async Task<TokenValidationResult> ValidateAsync(
        JwtTokenFactory factory,
        string token,
        DateTime? simulatedNow = null)
    {
        var parameters = factory.CreateValidationParameters();

        if (simulatedNow is not null)
        {
            parameters.LifetimeValidator = (notBefore, expires, _, _) =>
                simulatedNow >= notBefore && simulatedNow < expires;
        }

        return await new JsonWebTokenHandler().ValidateTokenAsync(token, parameters);
    }

    public static IReadOnlyList<Claim> ReadClaims(string token)
    {
        var payload = new JsonWebToken(token);

        return payload.Claims.ToList();
    }

    public static IReadOnlyList<string> ReadClaimTypes(string token)
    {
        return ReadUserClaims(token)
            .Select(claim => claim.Type)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    public static IReadOnlyList<Claim> ReadUserClaims(string token)
    {
        return new JsonWebToken(token).Claims
            .Where(claim => !ProtocolClaimTypes.Contains(claim.Type))
            .ToList();
    }

    public static string? ReadClaim(string token, string claimType)
    {
        return ReadClaims(token).FirstOrDefault(claim => claim.Type == claimType)?.Value;
    }
}