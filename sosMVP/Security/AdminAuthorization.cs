using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace SosMVP.Security;
/*
Clase estática (sin estado) que centraliza la lectura de claims del JWT y la política de 
autorización de administrador. Los nombres de los claims no están hardcodeados: se toman de 
JwtTokenFactory (fuente única de verdad).

Miembros y métodos
PolicyName:	Constante "AdminOnly", nombre con el que se registra y se referencia 
la política.
AdminRole: Constante "admin", valor del rol requerido.
BuildPolicy(): Construye una AuthorizationPolicy que exige el claim Role = admin. 
Si el token no lo trae, ASP.NET responde 403 antes de llegar al handler.
ReadRoleClaim(principal): Devuelve el valor del claim Role o null si no existe. Uso 
defensivo: principal?..
ReadUserIdClaim(principal): Lee el claim Id y lo convierte a int? con int.TryParse e 
invariante cultural. Devuelve null si falta o está corrupto (nunca lanza).

Influencia dentro de sosMVP/
Se usa en tres frentes:
1. Registro de la política — Program.cs:48
.AddPolicy(AdminAuthorization.PolicyName, AdminAuthorization.BuildPolicy());
Es el único punto donde la política se define; sin esto, las referencias de abajo fallarían.
2. Grupos de endpoints protegidos (6 archivos Extensions/*EndpointsExtensions.cs) — cada módulo 
crea un grupo con .RequireAuthorization(AdminAuthorization.PolicyName) (ej. UserEndpointsExtensions.cs:14). 
Eso protege con 401/403 todas las rutas de escritura/administración (/user/, /users/, /adminv/, etc.), 
dejando fuera solo /login/ y las de firma.
3. Lógica dentro de los handlers (Handlers/):
- TypeHandlers.cs:36 — ReadRoleClaim + AdminRole: el listado completo del catálogo exige rol admin dentro 
del handler (403 con mensaje en español).
- UserHandlers.cs:56 — ReadUserIdClaim: impide que un admin deshabilite su propia cuenta (compara el Id de 
la sesión con el id de la ruta).
- BinnacleHandlers.cs (7 usos) — ReadUserIdClaim: obtiene el usuario de la sesión para registrar la bitácora 
y acotar sus consultas; si es null, devuelve 401.
Idea global: JwtTokenFactory emite los claims, AdminAuthorization los lee/valida, y Program.cs los conecta 
al pipeline. Es la pieza que traduce "quién es el usuario en este request" en decisiones de autorización (401 
sin sesión válida, 403 sin rol admin).
*/
public static class AdminAuthorization
{
    public const string PolicyName = "AdminOnly";
    public const string AdminRole = "admin";

    public static AuthorizationPolicy BuildPolicy()
    {
        return new AuthorizationPolicyBuilder()
            .RequireClaim(JwtTokenFactory.RoleClaimType, AdminRole)
            .Build();
    }

    public static string? ReadRoleClaim(ClaimsPrincipal? principal)
    {
        return principal?.FindFirst(JwtTokenFactory.RoleClaimType)?.Value;
    }

    public static int? ReadUserIdClaim(ClaimsPrincipal? principal)
    {
        var raw = principal?.FindFirst(JwtTokenFactory.IdClaimType)?.Value;

        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)
            ? id
            : null;
    }
}
