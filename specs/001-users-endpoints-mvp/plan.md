# Plan de Implementacion — Caso de Uso: Usuario

- **Spec que autoriza:** `specs/001-users-endpoints-mvp/spec.md`
- **Constitucion:** `docs/constitution.md`
- **Alcance de este documento:** COMO se implementa la spec. Cita los RF que autoriza cada decision.
- **Estado:** sin bloqueantes. Los tres que se detectaron se resolvieron actualizando la spec (RNF-14, RF-2.5 y RF-8).

---

## 0. Inventario del codigo existente (verificado)

| Elemento | Realidad actual |
| --- | --- |
| `ICommonService<TDTO>` | `AddAsyncInfo(TDTO, ContactDTO?)`, `GetAsyncInfo(TDTO)`, `GetAsyncAllInfo()`, `UpdateAsyncInfo(TDTO)`, `UpdateAsyncVisibility(TDTO)` |
| `CommonService<TEntity, TDTO>` | Implementa `ICommonService<TDTO>`. Props privadas: `_repository` (`IRepository<TEntity,TDTO>`), `_toEntityMapper` (`IMapper<TDTO,TEntity>`), `_toContactEntityMapper` (`IMapper<ContactDTO,ContactEntity>`) |
| `IUserService` | `Login(UserDTO)` -> `Task<UserDTO>`, `AdminPwdConfirmation(UserDTO)` -> `bool` |
| `UserService` | Implementa `IUserService`. Props privadas: `_repository` (`IUserRepository`), `_toEntityMapper` (`IMapper<UserDTO,UserEntity>`) |
| `UserRepository` | Implementa `IRepository<UserEntity,UserDTO>`, `ISignatureRepository<...>`, `IUserRepository`. Prop privada: `_dbContext` (`Sosv6DbContext`) |
| Excepciones de dominio | `EntityException`, `ApplicationException` (ambos heredan de `Exception`) |
| Excepciones de repositorio | `KeyNotFoundException` (no encontrado), `Exception` (contrasena incorrecta), `DbUpdateException` (indice `uq_alias`) |
| Restricciones de unicidad | `HasIndex(e => e.Alias, "uq_alias").IsUnique()` en `Sosv6DbContext` |
| `UserDTOtoEntityMapper` | `IMapper<UserDTO, UserEntity>`. Resuelve precedencia: `Nickname = obj.AdminNickname != null ? obj.AdminNickname : obj.Nickname` y `Password = obj.AdminPwd != null ? obj.AdminPwd : obj.Password`. Por eso `adminv/` no necesita logica de traduccion propia |
| Mappers Model -> DTO | **Eliminados por decision de proyecto.** Ningun repositorio los usa: las consultas proyectan a `UserDTO` con `Select` dentro del propio repositorio |
| Contrasenas | `PasswordHasher` (PBKDF2-SHA256, 100 000 iteraciones, salt 16 B) en `hexArch/data/helpers`; usado dentro de `UserRepository` en CREATE y UPDATE |
| `Sosv6DbContext` | Cadena de conexion **fijada en codigo dentro de `OnConfiguring`** (SQL Server `SOSv6DB`), con usuario y contrasena incrustados. No hay seccion `ConnectionStrings` en `appsettings.json`. **Problema a corregir en la seccion 4.3**: la cadena debe salir del codigo |
| `Program.cs` | 4 lineas: `CreateBuilder`, `Build`, `Run`. Sin DI, sin endpoints, sin autenticacion, sin `AddDbContext` |
| `sosMVP/appsettings.json` | Sin seccion `Jwt` ni seccion `ConnectionStrings`. Ambas se anaden (secciones 3.3 y 4.3) |
| `test/` | Un solo archivo `TypeEntityTests.cs` con 3 tests de dominio. Sin referencia a `sosMVP.csproj` |
| Enums | **No existe ningun `enum` en el proyecto**. `Role` y `Visibility` son `string` con literales `"admin"`, `"user"`, `"ENABLED"`, `"DISABLED"` |

---

## 1. Paquete NuGet para JWT: autorizado por la spec (RNF-14)

**Principio 1 de la constitucion:** "Solo .NET 10, biblioteca estandar, EF Core y xUnit; ninguna dependencia NuGet nueva sin spec que la autorice."

El mecanismo previsto es ese: la spec lo autoriza. Verificado en el equipo, el paquete de referencias de ASP.NET Core 10.0.12 (`Microsoft.AspNetCore.App.Ref`) **no contiene** `Microsoft.AspNetCore.Authentication.JwtBearer.dll` ni ninguna assembly de `System.IdentityModel.Tokens.Jwt`. En .NET 10 ese middleware se distribuye solo como paquete NuGet.

**RNF-14 de la spec** autoriza expresamente `Microsoft.AspNetCore.Authentication.JwtBearer` en `net10.0`, y declara que no se autoriza ningun otro paquete.

Consecuencias tecnicas:

- Se anade un unico `PackageReference` a `sosMVP/sosMVP.csproj`, version `10.0.0` para alinearse con EF Core.
- `System.IdentityModel.Tokens.Jwt` llega como dependencia transitiva de ese paquete; **no** se declara por separado.
- `test/` **no** recibe el paquete: los tests del token trabajan con la fabrica de emision, no con el middleware de validacion.
- Criterio de finalizacion 15 de la spec exige verificar con `dotnet list package` que no aparece ningun paquete fuera de la lista.

**Alternativa descartada:** emitir el token a mano con `System.IdentityModel.Tokens.Jwt` y validar con `Microsoft.AspNetCore.Authentication.JwtBearer` ausente, haciendo la comprobacion de firma en un middleware propio. Se descarto porque obliga a reimplementar la validacion (firma, expiracion, `iss`, `aud`) que el middleware ya resuelve, y porque `dotnet list package` seguiria mostrando los paquetes de identidad, con el mismo coste y mas codigo.

---

## 2. Casos de uso: interfaces, propiedades privadas y metodos a usar

### 2.1 `CommonService<UserEntity, UserDTO>` — endpoints `user/`, `users/`, `userv/`

Se registra como `CommonService<UserEntity, UserDTO>` implementando `ICommonService<UserDTO>`.

| Endpoint | Metodo del caso de uso | Retorno | RF que autoriza |
| --- | --- | --- | --- |
| `POST user/` | `AddAsyncInfo(UserDTO dto, ContactDTO? contact = null)` | `Task` (void) | RF-2.1, RF-2.4 |
| `GET user/{id}` | `GetAsyncInfo(UserDTO dto)` | `Task<UserDTO>` | RF-3.1, RF-3.2 |
| `GET users/` | `GetAsyncAllInfo()` | `Task<IEnumerable<UserDTO>>` | RF-4.1, RF-4.3 |
| `PUT user/{id}` | `UpdateAsyncInfo(UserDTO dto)` | `Task` (void) | RF-5.2, RF-5.5 |
| `PUT userv/{id}` | `UpdateAsyncVisibility(UserDTO dto)` | `Task` (void) | RF-6.2, RF-6.5 |

Propiedades privadas que se conservan tal cual (`readonly`), inyectadas por el constructor:

- `_repository` — `IRepository<UserEntity, UserDTO>`: puerto secundario, satisface `UserRepository` (RNF-2).
- `_toEntityMapper` — `IMapper<UserDTO, UserEntity>`: satisface `UserDTOtoEntityMapper`.
- `_toContactEntityMapper` — `IMapper<ContactDTO, ContactEntity>`: se mantiene por construccion aunque `AddAsyncInfo` de usuario no lo use.

**No se modifican los casos de uso.** Sus firmas ya satisfacen RF-2.4, RF-5.5 y RF-6.5 (los tres son `void`, coherente con la spec).

Comportamientos que el caso de uso ya aporta y que la API solo debe traducir:

- `UpdateAsyncVisibility` valida `Id > 0` y que `Visibility` sea `ENABLED` o `DISABLED`, acumulando mensajes y lanzando `ApplicationException` (cubre RF-6.3, RF-6.6, CE-17).
- `UpdateAsyncInfo` no valida `Role` contra una lista cerrada; eso lo aporta `UserEntity.Role` (longitud <= 10) y lo planifico como filtro explicito en la API (RF-2.6, RNF-11).

### 2.2 `UserService` — endpoints `login/`, `adminv/`

Implementa `IUserService`. Propiedades privadas: `_repository` (`IUserRepository`) y `_toEntityMapper` (`IMapper<UserDTO, UserEntity>`).

| Endpoint | Metodo del caso de uso | Retorno | RF que autoriza |
| --- | --- | --- | --- |
| `POST login/` | `Login(UserDTO dto)` | `Task<UserDTO>` | RF-7.1, RF-7.6, RF-7.8 |
| `POST adminv/` | `AdminPwdConfirmation(UserDTO dto)` | `bool` | RF-8.1, RF-8.2, RF-8.3, RF-8.5 |

Aportes del repositorio que satisfacen la spec sin tocarlo:

- `Login` filtra por `Visibilidad == "ENABLED"`; si no hay coincidencia lanza `KeyNotFoundException` -> `404` (RF-7.2, RF-7.6, CE-10, CE-12).
- `Login` verifica el hash y, si no coincide, lanza `Exception` -> `400` (RF-7.3, CE-11).
- `Login` devuelve un `UserDTO` proyectado con `Id, Name, Surname, Nickname, Role, Signature` y **sin** `Password` (base de RF-7.9, RNF-12).
- `AdminPwdConfirmation` lanza `KeyNotFoundException` si el administrador no existe -> `404` (RF-8.4, CE-14b); devuelve `true`/`false` (RF-8.2, RF-8.3).

**Resolucion de credenciales en `adminv/` (RF-8.5, RF-8.6):** `UserDTOtoEntityMapper` ya resuelve la precedencia: `Nickname = obj.AdminNickname != null ? obj.AdminNickname : obj.Nickname` y `Password = obj.AdminPwd != null ? obj.AdminPwd : obj.Password`. Por tanto `POST adminv/` entrega el `UserDTO` recibido tal cual al caso de uso; el mapper proyecta las credenciales a `Nickname` y `Password`, que es lo que `AdminPwdConfirmation` lee. No hace falta logica de traduccion en el handler (seccion 2.3 eliminada).

El endpoint sigue validando que `AdminNickname` y `AdminPwd` lleguen informados antes de invocar el caso de uso, respondiendo `400` si no (CE-14d). La contrasena **nunca** se toma de los `claims`: RF-7.9 los excluye y RF-8.6 lo prohibe explicitamente.

---

## 3. Sesiones con JWT (RF-1, RF-7, CE-7 a CE-10e)

### 3.1 Construccion del token

1. `POST login/` invoca `IUserService.Login(dto)` con el `UserDTO` recibido.
2. Se construye la lista de claims con **lista de cierre** (no por negacion), cumpliendo RF-7.11 y RNF-12:
   - Permitidos: `Id`, `Name`, `Surname`, `Nickname`, `Role`, `Signature`.
   - Excluidos siempre: `Password`, `ConfPwd`, `AdminPwd`, `AdminNickname`.
3. Se firma el token con clave de 256 bits y `HS256`, `ValidateIssuer`/`ValidateAudience` activos, vida acotada.

**Por que lista de cierre y no una lista de prohibidos** (decision tecnica): la unica lectura de usuario disponible es la de `UserRepository`, que proyecta el `UserDTO` con `Select` dentro de la propia consulta. Si en el futuro algun camino de lectura empieza a projectar tambien `Contrasena`, una lista de prohibidos dejaria pasar la credencial. Con lista de cierre, anadir un campo sensible al `UserDTO` no filtra nada hasta que se incorpore a proposito. Cubre RF-7.9, RF-7.11, RNF-12, CE-10d, CE-10e.

**Alternativa descartada:** serializar el `UserDTO` entero a JSON dentro de un unico claim. Se descarto porque incluiria `Password` y `ConfPwd` en cuanto la consulta los trajera, violando RNF-12, y porque un unico claim grande y opaco impide que el middleware de autorizacion lea el rol.

### 3.2 Control de acceso (RF-1, RF-7.10)

- Politica `AdminOnly` con `RequireClaim("Role", "admin")`, aplicada por el grupo de rutas protegidas.
- `login` se declara **fuera** del grupo protegido (RF-1.7, RF-7.5).
- La autenticacion previa produce `401` por ausencia o expiracion de token (RF-1.1, RF-1.2, CE-7, CE-19).
- La politica produce `403` por rol distinto de `admin` o por sesion sin claim de rol legible (RF-1.3, RF-1.4, RF-1.5, RF-1.6, CE-8, CE-9).
- El orden "sesion valida, luego rol" lo impone el propio pipeline de ASP.NET Core: `UseAuthentication()` antes de `UseAuthorization()`.

**Alternativa descartada:** una clase `AdminFilter : IEndpointFilter`. Se descarto porque `AddAuthentication`/`AddAuthorization` ya resuelven el orden exigido por RF-1.3, y un filtro propio habria que replicar a mano la distincion `401`/`403` que la spec hace explicita.

### 3.3 Vigencia de la sesion

Duracion fijada por la spec: **30 minutos** (RF-7.7), sin renovacion por actividad (RF-7.7b). Se configura en `appsettings.json` bajo `Jwt:SessionMinutes` y se lee con `IOptions<JwtOptions>`, de modo que el valor vive en configuracion y no en el codigo. Cubre RF-7.7, CE-19, CE-19b.

Sobre el efecto de cambiar `Role` o `Visibility` con una sesion viva: la spec lo resuelve en RF-7.10b y CE-19c. El `Role` de los`claims` es la unica fuente de decision y `Visibility` no participa en el control de acceso (RF-7.12), por lo que un token ya emitido conserva su rol hasta expirar. La revocacion inmediata exigiria consultar el almacen en cada peticion, lo que RNF-2 y la prohibicion de tocar `hexArch/repository` hacen inviable en este MVP.

**Alternativa descartada:** renovacion automatica de la sesion. Se descarto porque anade una ruta de emision adicional no contemplada en la spec y complica el caso `401` de CE-19 sin aportar valor al MVP.

---

## 4. Endpoints: metodo de extension con `IEndpointRouteBuilder`

**Decision tecnica:** los siete endpoints se registran en una clase de extension `static` sobre `IEndpointRouteBuilder`, en `sosMVP/Extensions/UserEndpointsExtensions.cs`, expuesta como `app.MapUserEndpoints()`. `Program.cs` queda con las llamadas de composicion.

**Por que** (decision tecnica, alternativa descartada): el SDK de minimal API ya provee `IEndpointRouteBuilder` como punto de extension estable (`Microsoft.AspNetCore.Routing`), con firmas en `RequestDelegate` generadas en tiempo de compilacion, que aportan de serie el binding de cuerpo JSON, el binding de `{id}` y la produccion de `400` automatico ante un cuerpo o ruta malformados. La alternativa descartada son controllers (`[ApiController]`): obligan a registrar un `AddControllers` y attributes, y rompen la coherencia con una base de codigo que ya es minimal API. Una tercera opcion descartada es escribir los `MapGet`/`MapPost` a mano en `Program.cs`, que es justo la saturacion que se quiere evitar.

### 4.1 Estructura de `Program.cs` tras el cambio

```
string connectionString = builder.Configuration.GetConnectionString("Sosv6Db") ?? throw ...;
builder.Services.AddDbContext<Sosv6DbContext>(options => options.UseSqlServer(connectionString));
builder.Services.Configure<JwtOptions>(...)
builder.Services.AddScoped<UserRepository>()
builder.Services.AddScoped<IUserService, UserService>()
builder.Services.AddScoped<IMapper<UserDTO, UserEntity>, UserDTOtoEntityMapper>()
builder.Services.AddScoped<IMapper<ContactDTO, ContactEntity>, ContactDTOtoEntityMapper>()
builder.Services.AddScoped<CommonService<UserEntity, UserDTO>>()
builder.Services.AddScoped<ICommonService<UserDTO>>(sp => sp.GetRequiredService<CommonService<UserEntity, UserDTO>>())
builder.Services.AddAuthentication(JwtBearerDefaults...).AddJwtBearer(...)
builder.Services.AddAuthorizationBuilder().AddPolicy("AdminOnly", ...)

var app = builder.Build();
app.UseExceptionHandler(...)      // ver 4.2
app.UseAuthentication();
app.UseAuthorization();
app.MapUserEndpoints();
app.Run();
```

Todos los registros son `scoped`, cumpliendo el principio 3. No hay un solo `new` sobre casos de uso ni DTOs en los endpoints.

`AddScoped<CommonService<UserEntity, UserDTO>>()` requiere un tipo cerrado por `IRepository<UserEntity, UserDTO>`, que se registra explicitamente. El alias de interfaz se registra con una fabrica que resuelve **la misma instancia**, no una segunda, para que el endpoint y sus pruebas trabajen siempre contra un unico caso de uso por peticion.

### 4.2 Traduccion de excepciones a codigos HTTP (RF-9)

Manejo centralizado en un `static IApplicationBuilder UseExceptionHandler(...)` con un `IExceptionHandler` registrado, en vez de `try/catch` repetido en cada endpoint. Traduce:

| Excepcion | Codigo | RF |
| --- | --- | --- |
| `KeyNotFoundException` | `404` | RF-9.1 |
| `EntityException` | `400` | RF-9.2 |
| `ApplicationException` | `400` | RF-9.3 |
| `Exception` | `400` | RF-9.4 |
| `DbUpdateException` con `SqlException` 2601/2627 sobre `uq_alias` | `500` | RF-9.5, RF-2.2, CE-4 |

El cuerpo es un mensaje en espanol (RF-9.6, RNF-6), tomado del `Message` de la excepcion, que el repositorio y el dominio ya redactan en espanol.

**Por que** (decision tecnica, alternativa descartada): un filtro o middleware unico evita siete bloques `try/catch` identicos y garantiza que un caso de error nuevo no se olvide. Se descarta `try/catch` por endpoint: duplicaria la tabla de traduccion siete veces y cualquier divergencia entre endpoints violaria RF-9.

`DbUpdateException` se discrimina por el codigo de error de SQL Server (2601 indice unico, 2627 restriccion unica) y por la presencia de `uq_alias`; el resto de `DbUpdateException` cae en `500` generico, nunca en `400`, para no enmascarar un fallo de infraestructura como un error del usuario.

### 4.3 Contexto de datos y DI: cadena de conexion en `appsettings.json`

**Decision tecnica:** la cadena de conexion se traslada desde `Sosv6DbContext.OnConfiguring` a `sosMVP/appsettings.json`, y se registra el contexto mediante `AddDbContext` con un `lambda` que la toma de la configuracion.

Situacion actual: `Sosv6DbContext` lleva la cadena **fijada en codigo** en `OnConfiguring`, con usuario y contrasena incrustados, lo que dispara ademas el aviso `CS0618` del propio SDK. Eso viola el principio de seguridad y hace imposible cambiar de entorno sin recompilar.

La constitucion (principio 5) solo permite modificar `hexArch/data/Models` cuando la spec lo pide de forma explicita. **Esta seccion del plan es esa autorizacion**, junto con RNF-15 de la spec.

#### 4.3.1 Cambios autorizados

| Archivo | Cambio |
| --- | --- |
| `hexArch/data/Models/Sosv6DbContext.cs` | Eliminar el cuerpo de `OnConfiguring` (dejarlo vacio o comentado la configuracion fija). **No se toca** `OnModelCreating` ni los `DbSet` |
| `sosMVP/appsettings.json` | Anadir seccion `ConnectionStrings` con la clave `Sosv6Db` |
| `sosMVP/Program.cs` | Leer la cadena con el builder y registrarla en `AddDbContext` |
| `test/` | Usar la misma clave con una base de pruebas o, preferentemente, no abrir conexion real |

#### 4.3.2 Lectura en `Program.cs`

La propiedad se obtiene una sola vez del builder, antes de `builder.Build()`, y se captura en el `lambda`:

```
string connectionString = builder.Configuration.GetConnectionString("Sosv6Db")
    ?? throw new InvalidOperationException("No se encontro la cadena de conexion 'Sosv6Db'.");

builder.Services.AddDbContext<Sosv6DbContext>(options =>
    options.UseSqlServer(connectionString));
```

Puntos que respeta esta forma:

- `GetConnectionString("Sosv6Db")` lee de `ConnectionStrings:Sosv6Db`. Si la clave falta, la excepcion lanza **al arrancar**, no en la primera peticion (fallo temprano).
- La variable local se captura por cierre en el `lambda`, de modo que `UseSqlServer` recibe el valor ya resuelto y no depende de `builder` dentro del delegado.
- El mensaje de la excepcion va en espanol (RNF-6).
- La lectura ocurre **antes** de `builder.Build()`, que es la unica ventana valida para leer `Configuration`.

#### 4.3.3 Configuracion resultante

`sosMVP/appsettings.json`:

```
"ConnectionStrings": {
  "Sosv6Db": "Server=...;Database=SOSv6DB;User Id=...;Password=...;TrustServerCertificate=True;"
}
```

La credencial no se versiona en claro: en un entorno real se sobreescribe con variable de entorno `ConnectionStrings__Sosv6Db`, que el builder resuelve con prioridad sobre el json. Para el MVP basta el json.

**Alternativa descartada:** mantener la cadena en `OnConfiguring` y limitarnos a no tocarla. Se descarto porque deja usuario y contrasena en el binario, bloquea cualquier configuracion por entorno y no elimina el aviso del compilador. La opcion intermedia que se descarto tambien es leer la cadena en `Program.cs` pero pasarla con `AddDbContext` sin `lambda`, lo cual no es posible porque la sobrecarga que acepta `string` no existe para `DbContextOptions` con las opciones derivadas.

### 4.4 Endpoint por endpoint

| Endpoint | Delegado | Respuesta | RF cubiertos |
| --- | --- | --- | --- |
| `POST user/` | `ICommonService<UserDTO>.AddAsyncInfo(dto)` | `201` sin cuerpo ni `Location` | RF-2.1 a RF-2.6, RNF-11, CE-2b |
| `GET user/{id}` | `GetAsyncInfo(dto con Id de ruta)` | `200` con `UserDTO` | RF-3.1 a RF-3.4 |
| `GET users/` | `GetAsyncAllInfo()` | `200` con coleccion | RF-4.1 a RF-4.3, CE-6, CE-6b |
| `PUT user/{id}` | `UpdateAsyncInfo(dto con Id de ruta)` | `204` sin cuerpo | RF-5.1 a RF-5.6, CE-1 |
| `PUT userv/{id}` | `UpdateAsyncVisibility(dto con Id de ruta)` | `204` sin cuerpo | RF-6.1 a RF-6.7, CE-2, CE-2b, CE-14c |
| `POST login/` | `IUserService.Login(dto)` | `200` con el token | RF-7.0 a RF-7.11, RNF-12, RNF-13, RNF-14, CE-10c |
| `POST adminv/` | `IUserService.AdminPwdConfirmation(dto)` | `200 {"confirmed":true}` / `403` | RF-8.0 a RF-8.6, CE-13, CE-14, CE-14b, CE-14d, CE-14e |

Reglas transversales dentro de los delegados:

- **Id de ruta gana al cuerpo** (RF-3.1, RF-5.1, RF-6.1, CE-1): el endpoint asigna `dto.Id = id` antes de invocar el caso de uso, sin leer el `Id` del cuerpo.
- **`{id}` declarado como `int`**: una ruta no numerica produce `400` automatico del binding, cubriendo RF-3.4, RF-5.6, RF-6.6 y CE-18.
- **`UpdateUserVisibility` proyecta a un DTO nuevo** con solo `Id` y `Visibility` (RF-6.2, CE-2). No se reutiliza el DTO recibido, para que ninguna otra propiedad llegue al caso de uso.
- **Auto-deshabilitacion** (RF-6.7, CE-14c): antes de invocar, se compara el `dto.Id` con el claim `Id` de la sesion; si coinciden y `Visibility` es `DISABLED`, se responde `403` sin tocar la base. La comparacion ocurre en la API porque es una regla de autorizacion, no de negocio de la entidad.
- **`POST user/` valida `Role`** contra `{"admin", "user"}` antes de invocar (RF-2.6, RNF-11). Es un filtro explicito porque `UserEntity.Role` solo limita longitud.
- **`POST adminv/` valida que `AdminNickname` y `AdminPwd` lleguen informados** (RF-8.5, CE-14d) y entrega el DTO sin transformar; el mapper ya resuelve la precedencia hacia `Nickname` y `Password`. No lee la contrasena de los `claims` (RF-8.6, CE-14e). Detalle en la seccion 2.2.
- **`adminv/` sigue siendo un endpoint protegido** (RF-1.1 a RF-1.6): se declara dentro del grupo que exige la politica `AdminOnly`. La sesion acredita who invoca; el DTO aporta la contrasena. Solo `login` queda exento (RF-1.7).
- **No se devuelven entidades:** los tres endpoints de escritura responden sin cuerpo (RF-2.4, RF-2.5, RF-5.5, RF-6.5, CE-2b).

### 4.6 Contrato de respuestas con `Produces` en cada endpoint

**Decision tecnica:** cada uno de los siete `Map*` encadena `Produces` para declarar su contrato de respuestas. Sin esto, el SDK asume `200` generico y cualquier consumidor de la documentacion openAPI recibe una descripcion que contradice la spec.

`Produces` es un metodo de extension sobre `IEndpointConventionBuilder` (`Microsoft.AspNetCore.Http`), asi que encadena directamente sobre el `RouteHandlerBuilder` que devuelven `MapGet`, `MapPost` y `MapPut`. La forma con cuerpo usa el tipo generico; la forma sin cuerpo usa la sobrecarga de solo codigo.

#### 4.6.1 Declaracion por endpoint

Cada linea declara los codigos que la spec asigna a ese endpoint, incluidos los que produce el pipeline de autenticacion y autorizacion (RF-1.1 a RF-1.6).

```
app.MapGet("/users/", UsersHandler)
   .Produces<IEnumerable<UserDTO>>(StatusCodes.Status200OK)
   .Produces(StatusCodes.Status401Unauthorized)
   .Produces(StatusCodes.Status403Forbidden);
```

| Endpoint | Declaracion | Origen de cada codigo |
| --- | --- | --- |
| `POST user/` | `.Produces(StatusCodes.Status201Created)` + `.Produces(400)` + `.Produces(401)` + `.Produces(403)` + `.Produces(500)` | `201` RF-2.4; `400` RF-2.3 y RF-2.6; `401`/`403` RF-1.2 y RF-1.4; `500` RF-2.2 y RF-9.5 |
| `GET user/{id}` | `.Produces<UserDTO>(StatusCodes.Status200OK)` + `.Produces(400)` + `.Produces(401)` + `.Produces(403)` + `.Produces(404)` | `200` RF-3.1; `400` RF-3.4; `401`/`403` RF-1.2 y RF-1.4; `404` RF-3.3 |
| `GET users/` | `.Produces<IEnumerable<UserDTO>>(StatusCodes.Status200OK)` + `.Produces(401)` + `.Produces(403)` | `200` RF-4.1; `401`/`403` RF-1.2 y RF-1.4 |
| `PUT user/{id}` | `.Produces(StatusCodes.Status204NoContent)` + `.Produces(400)` + `.Produces(401)` + `.Produces(403)` + `.Produces(404)` + `.Produces(500)` | `204` RF-5.5; `400` RF-5.4 y RF-5.6; `401`/`403` RF-1.2 y RF-1.4; `404` RF-5.3; `500` RF-9.5 |
| `PUT userv/{id}` | `.Produces(StatusCodes.Status204NoContent)` + `.Produces(400)` + `.Produces(401)` + `.Produces(403)` + `.Produces(404)` | `204` RF-6.5; `400` RF-6.3 y RF-6.6; `401`/`403` RF-1.2, RF-1.4 y RF-6.7; `404` RF-6.4 |
| `POST login/` | `.Produces<TokenResponse>(StatusCodes.Status200OK)` + `.Produces(400)` + `.Produces(404)` | `200` RF-7.1; `400` RF-7.3; `404` RF-7.2. **Sin `401` ni `403`**: es el unico endpoint sin sesion previa (RF-1.7, RF-7.5) |
| `POST adminv/` | `.Produces<AdminConfirmation>(StatusCodes.Status200OK)` + `.Produces(400)` + `.Produces(401)` + `.Produces(403)` + `.Produces(404)` | `200` RF-8.2; `400` RF-8.5; `401` RF-1.2; `403` RF-8.3 y RF-1.4; `404` RF-8.4 |

#### 4.6.2 Reglas de uso

- **Se declara en el endpoint, no en el grupo.** `Produces` aplicado sobre el `RouteGroupBuilder` se propaga a todas las rutas del grupo y les impondrá el mismo codigo, lo que contradice la spec (un grupo no puede ser `201` y `204` a la vez). El grupo protegido lleva solo `.RequireAuthorization(...)`.
- **Los tres endpoints sin cuerpo no declaran tipo.** `POST user/`, `PUT user/{id}` y `PUT userv/{id}` usan la sobrecarga de solo codigo, porque la spec prohibe devolver cuerpo (RF-2.5, RF-5.5, RF-6.5). Declarar `Produces<AlgunaCosa>(201)` seria una contradiccion entre la documentacion y el comportamiento real.
- **`Produces` no ejecuta nada.** Solo anade metadata `IProducesResponseTypeMetadata` al endpoint. El `401` y el `403` los sigue produciendo el middleware de autenticacion y autorizacion; declararlos en `Produces` documenta, no implementa.
- **`401` y `403` se declaran tambien.** Es lo correcto: el consumidor ve que el endpoint exige sesion antes de invocarlo.
- **`login` es la excepcion deliberada** y por eso su fila no lleva `401` ni `403`.
- **`ProducesProblem` queda fuera.** Los errores se construyen a mano en `ExceptionHandlerExtensions` (seccion 4.2) con el mensaje en espanol (RNF-6), no como `ProblemDetails`. Anadir `ProducesProblem` declararia un `ProblemDetails` que la API nunca devuelve.

**Alternativa descartada:** declarar el contrato con un filtro de endpoint propio. Se descarto porque `Produces` ya existe en el framework, no requiere paquetes y no altera el comportamiento en tiempo de ejecucion.

#### 4.6.3 Efecto real: requiere la generacion openAPI

`Produces` solo es visible si el documento openAPI se genera. Con .NET 10 eso exige `builder.Services.AddOpenApi()` y `app.MapOpenApi()`, que a su vez requieren el paquete `Microsoft.AspNetCore.OpenApi`. **Ese paquete no esta autorizado** por RNF-14, que es el unico `PackageReference` permitido.

Por eso este plan **no anade el paquete**. `Produces` se declara igualmente, porque:

1. La metadata queda en el `EndpointDataSource` y es consultable sin ningun paquete, lo que permite verificarla en un test (seccion 6.2).
2. Si mas adelante se autoriza el paquete, el documento sale correcto sin tocar los siete endpoints.

Si quieres que la documentacion openAPI este disponible en el MVP, hace falta una RNF que autorice `Microsoft.AspNetCore.OpenApi` `10.0.0`; es un unico paquete del mismo framework, pero la constitucion no me deja anadirlo sin tu instruccion.

### 4.7 Cabecera `Location`: eliminado por decision de spec

La spec ya no exige `Location` (RF-2.5 reescrito: la respuesta se limita al codigo `201`, sin cuerpo ni cabecera). Por tanto **no se emite ninguna cabecera `Location`** en `POST user/`.

Esto elimina el bloqueante que se habia detectado: no hace falta tocar `hexArch/repository` para recuperar el `Id` generado, porque ya no se necesita. El repositorio permanece intacto conforme al principio 5.

---
## 5. Estructura de archivos prevista

| Archivo | Accion | Contenido | Constitucion |
| --- | --- | --- | --- |
| `sosMVP/sosMVP.csproj` | modificar | Unico `PackageReference`: `Microsoft.AspNetCore.Authentication.JwtBearer` `10.0.0` | RNF-14, principio 1 |
| `test/test.csproj` | modificar | Anadir `ProjectReference` a `sosMVP.csproj`; sin paquetes nuevos | Principio 4 |
| `sosMVP/Program.cs` | modificar | Composicion de DI, autenticacion, autorizacion y una llamada a `MapUserEndpoints()` | Principio 3 |
| `sosMVP/Extensions/UserEndpointsExtensions.cs` | crear | `static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder)` con los 7 `Map*`, cada uno encadenando su `Produces` (seccion 4.6) | Principios 1, 3 |
| `sosMVP/Handlers/UserHandlers.cs` | crear | Delegados de los endpoints como metodos nombrados, con la logica de negocio de la capa HTTP | Principio 1 |
| `sosMVP/Extensions/ExceptionHandlerExtensions.cs` | crear | `UseExceptionHandler` y el `IExceptionHandler` de la tabla de RF-9 | Principios 1, 3 |
| `sosMVP/Security/JwtOptions.cs` | crear | Clase de opciones de emision (issuer, audience, clave, minutos de vigencia) | Principio 6 |
| `sosMVP/Security/JwtTokenFactory.cs` | crear | Firma del token y construccion de claims por lista de cierre | RF-7.8 a RF-7.11, RNF-12 |
| `sosMVP/Security/AdminAuthorization.cs` | crear | Politica `AdminOnly` y lectura del claim de rol | RF-1.3 a RF-1.6 |
| `sosMVP/Extensions/ServiceCollectionExtensions.cs` | crear | `AddUserModule()` con todos los `AddScoped` | Principios 1, 3 |
| `sosMVP/appsettings.json` | modificar | Secciones `Jwt` (issuer, audience, vigencia) y `ConnectionStrings` con la clave `Sosv6Db` | RNF-14, RNF-15 |
| `test/` | crear archivos | Suites descritas en la seccion 6 | Principio 4 |
| `hexArch/repository/**` | **no tocar** | Consultas existentes | Principio 5 |
| `hexArch/data/Models/**` | modificar | Solo `Sosv6DbContext.OnConfiguring`: se retira la cadena fija. Autorizado por RNF-15 y seccion 4.3. `OnModelCreating` y `DbSet` intactos | Principio 5 |
| `hexArch/application/**`, `hexArch/domain/**` | **no tocar** | Casos de uso y entidades ya satisfacen la spec | RNF-1 |
| `hexArch/data/mappers/modelToDto/**` | **eliminar** | Mappers de entidad a DTO sin uso; las consultas ya proyectan a DTO con `Select` | Decision de proyecto, RNF-2 |

La logica de los endpoints vive en `UserHandlers`, no dentro de lambdas de `MapGet`/`MapPost`. Motivo: una lambda inline no es invocable desde un test xUnit sin levantar la aplicacion completa; un metodo nombrado que recibe las interfaces y devuelve `IResult` si lo es. Esto habilita la estrategia de tests de la seccion 6 sin anadir paquetes.

**Nota sobre mappers.** Solo se registran los que van de DTO a entidad (`UserDTOtoEntityMapper`, `ContactDTOtoEntityMapper`). Los mappers de entidad a DTO fueron eliminados por decision de proyecto, ya que ningun repositorio los utiliza: las consultas proyectan directamente a `UserDTO` con `Select` dentro del propio repositorio. No se registra ningun `IMapper<UserEntity, UserDTO>`; el `DbContext` no lo necesita.

---

## 6. Estrategia de tests (principio 4)

**Restriccion de la constitucion:** solo xUnit. `Microsoft.AspNetCore.Mvc.Testing` y `Microsoft.AspNetCore.TestHost` estan prohibidos, asi que no habra pruebas de integracion HTTP reales ni `WebApplicationFactory`. La consecuencia honesta: los codigos `401` y `403` se verifican probando la logica de autorizacion, no el pipeline completo. Queda anotado como limitacion conocida.

Ademas, `test/test.csproj` **no referencia `sosMVP.csproj`**. Anadir esa `ProjectReference` es lo que habilita estas pruebas; es un cambio de proyecto, no un paquete nuevo, por lo que no requiere autorizacion de spec. Requiere exposing la clase de extension como `public` (ya lo sera) y que `sosMVP` exponga las clases necesarias.

### 6.1 Suites

| Suite | Sujeto | Cubre |
| --- | --- | --- |
| `UserHandlersInsertUserTests` | `UserHandlers.InsertUserAsync` con `ICommonService<UserDTO>` falso | RF-2.1 a RF-2.6, CE-2b, CE-4, CE-15 |
| `UserHandlersGetUserTests` | `UserHandlers.GetUserAsync` | RF-3.1 a RF-3.4, CE-1, CE-3, CE-18 |
| `UserHandlersGetUsersTests` | `UserHandlers.GetUsersAsync` | RF-4.1 a RF-4.3, CE-6, CE-6b |
| `UserHandlersUpdateUserTests` | `UserHandlers.UpdateUserAsync` | RF-5.1 a RF-5.6, CE-1, CE-5, CE-15 |
| `UserHandlersUpdateUserVisibilityTests` | `UserHandlers.UpdateUserVisibilityAsync` | RF-6.1 a RF-6.7, CE-2, CE-2b, CE-14c, CE-17 |
| `UserHandlersLoginTests` | `UserHandlers.LoginAsync` + `JwtTokenFactory` | RF-7.0 a RF-7.11, RNF-12, CE-10, CE-11, CE-12, CE-10b, CE-10d, CE-10e |
| `UserHandlersAdminVerificationTests` | `UserHandlers.AdminVerificationAsync` | RF-8.0 a RF-8.6, CE-13, CE-14, CE-14b, CE-14d, CE-14e |
| `AdminAuthorizationTests` | `AdminAuthorization` sobre un `ClaimsPrincipal` fabricado | RF-1.3 a RF-1.6, CE-8, CE-9 |
| `JwtTokenFactoryTests` | Claims emitidos y expiracion | RF-7.7, RF-7.7b, RF-7.9, RF-7.11, RF-7.12, CE-19, CE-19b, CE-10b, CE-10e, CE-10f |
| `ExceptionTranslationTests` | `IExceptionHandler` con cada excepcion | RF-9.1 a RF-9.6, CE-16 |
| `UserServiceTests` | `UserService` con `IUserRepository` falso | Delega en RF-7.1, RF-8.1 |
| `CommonServiceUserTests` | `CommonService<UserEntity, UserDTO>` con repositorio falso | Delega en RF-6.3 y validaciones heredadas |
| `UserEntityTests` (nuevo) | `UserEntity` | Reglas de longitud, `Role`, base de RF-2.6 y RNF-11 |
| `TypeEntityTests` (existente) | Sin cambios | Se mantiene |

### 6.2 Requisitos por endpoint (constitucion, principio 4)

Cada endpoint necesita como minimo tres pruebas: camino feliz, caso limite y caso de error. Ejemplo Applied al endpoint mas delicado:

- `UpdateUserVisibilityAsync`:
  - **Feliz:** DTO con `Id` y `Visibility = "DISABLED"` -> `204`, y el caso de uso recibe solo esas dos propiedades (CE-2).
  - **Limite:** `Id` de la ruta que coincide con el claim de la sesion y `Visibility = "DISABLED"` -> `403`, y el caso de uso **no** es invocado (RF-6.7, CE-14c).
  - **Error:** el caso de uso lanza `KeyNotFoundException` -> el manejador la traduce a `404` (RF-6.4, CE-5).

Ademas, un test dedicado a la metadata de `Produces` (seccion 4.6): se construye el `EndpointDataSource` del servicio, se busca cada `RouteEndpoint` por su ruta y se comprueba que su metadata `IProducesResponseTypeMetadata` contenga exactamente los codigos de la tabla 4.6.1. Es posible sin anadir paquetes y sin generar el documento openAPI.

### 6.3 Doubles de prueba

`test/Fakes/` con implementaciones de `IRepository<UserEntity, UserDTO>`, `IUserRepository` e `IMapper<,>` que registran las llamadas recibidas y devuelven valores programados. Se prefieren a un `InMemory` de EF Core porque `UpdateAsyncInfo` y `UpdateAsyncVisibility` del repositorio usan `ExecuteUpdateAsync`, que no es soportado por el proveedor en memoria; ademas, los fakes no tocan la base de datos real.

Cada test crea sus propios datos (criterio 13 de finalizacion), sin sembrado compartido.

---

## 7. Matriz de cobertura RF -> parte del plan

| RF | Criterio cubierto en |
| --- | --- |
| RF-1.1, RF-1.2 | 3.2 (politica y autenticacion), 4.4 |
| RF-1.3, RF-1.4, RF-1.5, RF-1.6 | 3.2, 4.1, suite `AdminAuthorizationTests` |
| RF-1.7 | 3.2, 4.4 (`login` fuera del grupo protegido) |
| RF-2.1, RF-2.4 | 2.1, 4.4 |
| RF-2.2 | 4.2 (`DbUpdateException` -> `500`) |
| RF-2.3, RF-2.6 | 2.1, 4.4 (validacion de `Role`) |
| RF-2.5 | 4.7 (sin `Location` por decision de spec) |
| RF-3.1, RF-3.2 | 2.1, 4.4 (Id de ruta gana) |
| RF-3.3 | 4.2 (`KeyNotFoundException` -> `404`) |
| RF-3.4 | 4.4 (`{id}` como `int`) |
| RF-4.1, RF-4.2, RF-4.3 | 2.1, 4.4 (filtro `ENABLED` ya en el repositorio) |
| RF-5.1, RF-5.2, RF-5.5 | 2.1, 4.4 |
| RF-5.3, RF-5.4 | 4.2 |
| RF-5.6 | 4.4 |
| RF-6.1, RF-6.2, RF-6.5 | 2.1, 4.4 (proyeccion de dos propiedades) |
| RF-6.3, RF-6.6 | 2.1, 4.2 |
| RF-6.4 | 4.2 |
| RF-6.7 | 4.4 (comparacion con el claim `Id`) |
| RF-7.0 | 3.1, 4.4 |
| RF-7.1, RF-7.4, RF-7.6, RF-7.8 | 2.2, 3.1 |
| RF-7.2, RF-7.3 | 2.2, 4.2 |
| RF-7.5 | 3.2 |
| RF-7.7, RF-7.7b | 3.3 (30 min, sin renovacion) |
| RF-7.9, RF-7.11 | 3.1 (lista de cierre) |
| RF-7.10, RF-7.10b | 3.2 (rol de los claims como unica fuente) |
| RF-7.12 | 3.1 y 3.2 (`Visibility` ausente de los claims y del control de acceso) |
| RF-8.0 | 4.4 (`POST` con cuerpo), 2.2 |
| RF-8.1 | 2.2 |
| RF-8.2, RF-8.3 | 2.2, 4.4 |
| RF-8.4 | 4.2 |
| RF-8.5, RF-8.6 | 2.2 (precedencia resuelta por `UserDTOtoEntityMapper`), 4.4 |
| RF-9.1 a RF-9.6 | 4.2, suite `ExceptionTranslationTests` |
| RNF-1, RNF-2 | Seccion 2 (inyeccion de puertos), seccion 5 |
| RNF-3 | 4.1 (todos los registros `scoped`) |
| RNF-4, RNF-5 | Sin cambios: ya se cumple |
| RNF-6 | Seccion 4.2 (mensajes en espanol), identificadores en ingles en toda la seccion 5 |
| RNF-7 | Delegado en la coherencia de `AddAsyncInfo` y `UpdateAsyncInfo` |
| RNF-8 | 4.4 (sin entidad devuelta en escrituras) |
| RNF-9 | Seccion 5 (contrato fijo en la spec) |
| RNF-10 | Ya cubierto por `PasswordHasher` dentro del repositorio; no se toca |
| RNF-11 | 4.4 (validacion explicita de `Role` en `POST user/`) |
| RNF-12 | 3.1 (lista de cierre) |
| RNF-13 | 4.4 (`login` y `adminv/` por `POST` con credenciales en el cuerpo) |
| RNF-14 | Seccion 1 (unico `PackageReference` autorizado) |
| RNF-15 | Seccion 4.3 (cadena en `appsettings.json` y `AddDbContext` con lambda), seccion 5 |

---

## 8. Estado de los bloqueantes

Los tres bloqueantes detectados quedaron resueltos por actualizacion de la spec:

1. **Paquete NuGet para JWT — resuelto.** RNF-14 autoriza expresamente `Microsoft.AspNetCore.Authentication.JwtBearer`. El plan usa ese paquete y ninguno mas (seccion 1).
2. **Cabecera `Location` — resuelto.** RF-2.5 ya no la exige; la respuesta se limita al `201`. Seccion 4.7.
3. **`AdminVerification` con credenciales — resuelto.** RF-8 pasa a `POST`, recibe `AdminNickname` y `AdminPwd` en el cuerpo, sigue protegido por sesion de rol `admin` y nunca toma la contrasena de los `claims` (RF-8.0, RF-8.5, RF-8.6). Seccion 2.2.

**Ya no hay bloqueantes para empezar a implementar.**

Ademas, estas dos dudas de la seccion 9 de la spec siguen abiertas y este plan las asume provisionalmente: comportamiento de `GetUser` ante un usuario `DISABLED` (asumido: lo devuelve, porque `GetAsyncInfo` no filtra por `Visibilidad` y el repositorio es inmodificable) y si el administrador puede editar su propio `Alias` o contrasena (asumido: si, no hay regla que lo impida y anadirla exigiria tocar el caso de uso o el repositorio). La duda sobre la vigencia de la sesion quedo resuelta en RF-7.7 y RF-7.7b.

---

## 9. Orden de ejecucion

1. Anadir el `PackageReference` de `Microsoft.AspNetCore.Authentication.JwtBearer` `10.0.0` a `sosMVP/sosMVP.csproj`, autorizado por RNF-14.
2. Retirar la cadena fija de `Sosv6DbContext.OnConfiguring` y anadir `ConnectionStrings:Sosv6Db` a `appsettings.json`, autorizado por RNF-15.
3. `sosMVP/Extensions/ServiceCollectionExtensions.cs` y `JwtOptions`.
4. `sosMVP/Security/JwtTokenFactory.cs` y `AdminAuthorization.cs`.
5. `sosMVP/Handlers/UserHandlers.cs` con los siete delegados.
6. `sosMVP/Extensions/UserEndpointsExtensions.cs` con los siete `Map*` y su `Produces` en cada uno.
7. `sosMVP/Extensions/ExceptionHandlerExtensions.cs` con la tabla RF-9.
8. Cableado final en `Program.cs`: lectura de la cadena con el builder, `AddDbContext` con `lambda`, autenticacion, autorizacion y seccion `Jwt` en `appsettings.json`.
9. Anadir la `ProjectReference` a `sosMVP.csproj` en `test/test.csproj`.
10. Suites de `test/` de la seccion 6.1, empezando por `AdminVerification` (precedencia `AdminNickname`/`AdminPwd`), `Login` (lista de cierre de claims) y `UpdateUserVisibility` (auto-deshabilitacion).
11. `dotnet build` y `dotnet test` en verde.
12. `dotnet list package` para confirmar el criterio de finalizacion 15 de la spec.
13. Comprobar que el aviso del compilador sobre la cadena de conexion desaparecio (criterio 18 de la spec).
14. Contrastar cada criterio de finalizacion (seccion 8 de la spec, 18 puntos) contra las pruebas escritas.
