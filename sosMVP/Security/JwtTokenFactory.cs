using System.Globalization;
using System.Security.Claims;
using System.Text;
using HexArch.Application.DTOs;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace SosMVP.Security;

public class JwtTokenFactory
{
    public const string IdClaimType = "Id";
    public const string NameClaimType = "Name";
    public const string SurnameClaimType = "Surname";
    public const string NicknameClaimType = "Nickname";
    public const string RoleClaimType = "Role";
    public const string SignatureClaimType = "Signature";

    private readonly JwtOptions _options;
    private readonly JsonWebTokenHandler _tokenHandler = new();

    public JwtTokenFactory(IOptions<JwtOptions> options)
    {
        _options = options.Value;
    }

    public string CreateToken(UserDTO dto)
    {
        var now = DateTime.UtcNow;

        return _tokenHandler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Subject = new ClaimsIdentity(BuildClaims(dto)),
            IssuedAt = now,
            NotBefore = now,
            Expires = now.AddMinutes(_options.SessionMinutes),
            SigningCredentials = new SigningCredentials(CreateSigningKey(_options), SecurityAlgorithms.HmacSha256)
        });
    }

    public static IReadOnlyList<Claim> BuildClaims(UserDTO dto)
    {
        var claims = new List<Claim>(6);

        AddClaim(claims, IdClaimType, dto.Id?.ToString(CultureInfo.InvariantCulture));
        AddClaim(claims, NameClaimType, dto.Name);
        AddClaim(claims, SurnameClaimType, dto.Surname);
        AddClaim(claims, NicknameClaimType, dto.Nickname);
        AddClaim(claims, RoleClaimType, dto.Role);
        AddClaim(claims, SignatureClaimType, dto.Signature);

        return claims;
    }

    public TokenValidationParameters CreateValidationParameters()
    {
        return CreateValidationParameters(_options);
    }

    public static TokenValidationParameters CreateValidationParameters(JwtOptions options)
    {
        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = options.Issuer,
            ValidateAudience = true,
            ValidAudience = options.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = CreateSigningKey(options),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            NameClaimType = NameClaimType,
            RoleClaimType = RoleClaimType
        };
    }

    private static SymmetricSecurityKey CreateSigningKey(JwtOptions options)
    {
        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Key));
    }

    private static void AddClaim(List<Claim> claims, string type, string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            claims.Add(new Claim(type, value));
        }
    }
}
