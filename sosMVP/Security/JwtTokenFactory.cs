using System.Globalization;
using System.Security.Claims;
using System.Text;
using HexArch.Application.DTOs;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace SosMVP.Security;
/*
Constantes (IdClaimType, NameClaimType, ... SignatureClaimType) — Definen los nombres de 
los 6 claims que lleva el token. Son la fuente única de verdad: las usan también 
AdminAuthorization (lectura de Role/Id) y los tests, así que si cambian aquí, cambian en 
todo el sistema.

Constructor JwtTokenFactory(IOptions<JwtOptions>) — Recibe la configuración JWT (Issuer, 
Audience, Key, SessionMinutes) inyectada por DI desde la sección Jwt del appsettings 
(registrada en Program.cs:15). Desempaqueta options.Value en _options.

CreateToken(UserDTO) — Método principal de emisión. Construye un SecurityTokenDescriptor 
con:
- Issuer/Audience de la configuración.
- Subject = claims construidos con BuildClaims(dto).
- IssuedAt/NotBefore = ahora, Expires = ahora + SessionMinutes.
- Firma HMAC-SHA256 con la clave simétrica de JwtOptions.Key.
Devuelve el JWT como string. Lo invoca UserHandlers.LoginAsync (UserHandlers.cs:83) al hacer 
login.

BuildClaims(UserDTO) (estático) — Convierte el UserDTO en la lista de 6 claims usando "lista 
de cierre": solo añade claims con valor no nulo/vacío vía AddClaim. Al ser estático se usa 
directamente en tests sin crear instancia.

CreateValidationParameters() (instancia) — Fachada que delega a la versión estática con 
_options. Existe para que consumidores (test) con la inyección de IOptions<JwtOptions> 
no tengan que pasar la config a mano.

CreateValidationParameters(JwtOptions) (estático) — Devuelve los parámetros con los que el 
middleware valida tokens entrantes: valida issuer, audience, clave firmante y vida útil, 
con ClockSkew = TimeSpan.Zero (sin margen de tolerancia) y mapea NameClaimType/RoleClaimType 
a nuestros claims para que ClaimsPrincipal.Identity.Name y roles funcionen.

CreateSigningKey(JwtOptions) (privado, estático) — Convierte options.Key (string UTF8) en 
SymmetricSecurityKey. Es el punto único de la clave: se usa tanto para firmar como para 
validar, lo que garantiza simetría.

AddClaim(List<Claim>, type, value) (privado, estático) — Guardia: solo agrega el claim si 
el valor no es nulo ni vacío. Evita claims vacíos en el token.

Influencia en Program.cs
1. Línea 15 — builder.Services.Configure<JwtOptions>(...) alimenta la configuración que luego 
consumirá la factory.
2. Línea 37 — AddJwtBearer() habilita el middleware de autenticación, que necesitará 
parámetros de validación.
3. Líneas 39-44 — Aquí está el uso directo: 
JwtTokenFactory.CreateValidationParameters(jwt.Value) configura 
JwtBearerOptions.TokenValidationParameters. Es decir, Program.cs solo usa la parte 
estática/validación; la parte de emisión (CreateToken) no aparece aquí porque se inyecta 
como Scoped en ServiceCollectionExtensions.cs:37 y se consume en el endpoint de login.
4. MapInboundClaims = false (línea 42) — Desactiva el mapeo automático de claims de 
ASP.NET, para que lleguen exactamente con nuestros tipos (Id, Role, ...) definidos en las 
constantes.
En resumen: Program.cs conecta la factory con el pipeline de autenticación — si 
CreateValidationParameters no coincidiera con lo que CreateToken firma (misma key, issuer, 
audience), ningún request autenticado pasaría. AdminAuthorization (línea 48) complementa 
esto exigiendo el claim Role que la factory emite.
*/
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
