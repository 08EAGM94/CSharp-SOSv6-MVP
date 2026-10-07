# Tareas — Spec 001: Caso de uso Usuario

- **Base:** `specs/001-users-endpoints-mvp/spec.md` + `specs/001-users-endpoints-mvp/plan.md`.
- **Orden:** de dependencia. Solo se ejecuta una tarea cada vez; se marca `[x]` y se PARA.
- **TDD:** primero los tests de la tarea (en rojo), después el código. Nunca se cierra una tarea con `dotnet test` en rojo.
- **Comprobación de cierre de cada tarea:** `dotnet build` (0 errores) y `dotnet test test/test.csproj` (0 fallos).
- **Nota de alcance:** las fases 0-8 corresponden al alcance original de la spec (siete endpoints, ya ejecutado). El cambio de spec que anade `InsertSignature` (RF-10), `GetSignature` (RF-11) y el control de acceso RF-1.8 se refleja en la **fase 9**, unica pendiente.

---

## Fase 0 — Infraestructura autorizada

- **Estado:** completada.

### T-01 Anadir el paquete JWT autorizado

- [X] Anadir `PackageReference` de `Microsoft.AspNetCore.Authentication.JwtBearer` version `10.0.0` a `sosMVP/sosMVP.csproj`.
- [X] No declarar `System.IdentityModel.Tokens.Jwt` por separado: llega como dependencia transitiva.
- [X] No anadir el paquete a `test/test.csproj`.

**RF cubiertos:** RNF-14.

**Hecho cuando:** `dotnet list sosMVP/sosMVP.csproj package` muestra `Microsoft.AspNetCore.Authentication.JwtBearer 10.0.0` y ninguna otra referencia nueva.

---

### T-02 Sacar la cadena de conexion del contexto

- [X] Anadir la seccion `ConnectionStrings` con la clave `Sosv6Db` a `sosMVP/appsettings.json`.
- [X] Vaciar el cuerpo de `Sosv6DbContext.OnConfiguring` en `hexArch/data/Models/Sosv6DbContext.cs`.
- [X] No tocar `OnModelCreating` ni los `DbSet`.

**RF cubiertos:** RNF-15, RNF-5.

**Hecho cuando:** `grep` de "Server=" sobre `hexArch/data/Models/Sosv6DbContext.cs` no devuelve nada, el aviso del compilador sobre la conexion ha desaparecido y la persistencia sigue pasando por Entity Framework Core.

---

### T-03 Exponer la referencia al proyecto web desde los tests

- [X] Anadir `ProjectReference` a `sosMVP.csproj` en `test/test.csproj`.
- [X] No anadir ningun paquete nuevo a `test/test.csproj`.

**RF cubiertos:** RNF-4.

**Hecho cuando:** una clase de `test/` puede escribir `using` de un tipo publico de `sosMVP` y `dotnet build` sigue en verde.

---

## Fase 1 — Seguridad

- **Estado:** completada.

### T-04 Crear `JwtOptions`

- [X] Crear `sosMVP/Security/JwtOptions.cs` con issuer, audience, clave de 256 bits y minutos de vigencia.
- [X] Anadir la seccion `Jwt` a `appsettings.json` con esos mismos valores, siendo `SessionMinutes` igual a `30`.
- [X] Registrar con `builder.Services.Configure<JwtOptions>(...)`.

**RF cubiertos:** RF-7.7, RF-7.7b.

**Hecho cuando:** una instancia de `IOptions<JwtOptions>` resuelta por DI devuelve `SessionMinutes == 30`.

---

### T-05 Crear la fabrica de emision de token

- [X] Crear `sosMVP/Security/JwtTokenFactory.cs` con un metodo que reciba el `UserDTO` y devuelva el token firmado.
- [X] Construir los`claims` por lista de cierre: solo`Id`, `Name`, `Surname`, `Nickname`, `Role` y `Signature`.
- [X] Firmar con clave de 256 bits y `HS256`; activar `ValidateIssuer` y `ValidateAudience`.
- [X] Expirar el token a los 30 minutos de emision, sin renovar.

**RF cubiertos:** RF-7.4, RF-7.7, RF-7.7b, RF-7.8, RF-7.9, RF-7.11, RF-7.12, RNF-12.

**Hecho cuando:** un test emite un token desde un DTO con todas las propiedades informadas y el payload contiene exactamente los seis claims permitidos, ninguno de ellos `Password`, `ConfPwd` ni `Visibility`.

---

### T-06 Crear la politica de rol administrador

- [X] Crear `sosMVP/Security/AdminAuthorization.cs` con la politica `AdminOnly` basada en `RequireClaim("Role", "admin")`.
- [X] Exponer una lectura del claim de rol para los handlers que la necesiten.
- [X] No consultar el almacen ni `Visibility` en ningun punto de esta decision.

**RF cubiertos:** RF-1.3, RF-1.4, RF-1.5, RF-1.6, RF-7.10, RF-7.10b, RF-7.12.

**Hecho cuando:** un `ClaimsPrincipal` con `Role = admin` satisface la politica, uno con `Role = user` no, y uno sin claim de rol tampoco produce excepcion.

---

## Fase 2 — Fakes de prueba

- **Estado:** completada.

### T-07 Crear los dobles de repositorio y mapper

- [X] Crear `test/Fakes/` con una implementacion falsa de `IRepository<UserEntity, UserDTO>` que registre el DTO recibido y devuelva valores programados.
- [X] Crear la implementacion falsa de `IUserRepository` con el mismo comportamiento.
- [X] Crear las implementaciones falsas de `IMapper<UserDTO, UserEntity>` e `IMapper<ContactDTO, ContactEntity>`.
- [X] No usar el proveedor `InMemory` de EF Core: `ExecuteUpdateAsync` no lo soporta.

**RF cubiertos:** RNF-4.

**Hecho cuando:** un test instancia los fakes, invoca un caso de uso y puede leer el DTO exacto que recibio.

---

## Fase 3 — Delegados de los endpoints

- **Estado:** completada.

### T-08 `UserHandlers`: delegate de `POST user/`

- [X] Crear `sosMVP/Handlers/UserHandlers.cs` con un metodo `InsertUserAsync` que reciba `ICommonService<UserDTO>`.
- [X] Validar que `Role` sea `admin` o `user` antes de invocar, respondiendo `400` si no lo es.
- [X] Invocar `AddAsyncInfo` y responder `201` sin cuerpo ni cabecera `Location`.

**RF cubiertos:** RF-2.1, RF-2.4, RF-2.5, RF-2.6, RF-2.3, RNF-11, CE-2b, CE-15.

**Hecho cuando:** el test del camino feliz observa `201` sin cuerpo, y un `Role = "root"` produce `400` sin invocar el caso de uso.

---

### T-09 `UserHandlers`: delegate de `GET user/{id}`

- [X] Anadir `GetUserAsync` que reciba `ICommonService<UserDTO>` y el `Id` de ruta.
- [X] Asignar `dto.Id = id` antes de invocar, ignorando cualquier `Id` del cuerpo.
- [X] Responder `200` con el `UserDTO` devuelto.

**RF cubiertos:** RF-3.1, RF-3.2, CE-1.

**Hecho cuando:** un test con `Id` de ruta `7` y `Id` de cuerpo `99` observa que el caso de uso recibe `Id == 7`.

---

### T-10 `UserHandlers`: delegate de `GET users/`

- [X] Anadir `GetUsersAsync` que invoque `GetAsyncAllInfo()`.
- [X] Responder `200` con la coleccion, incluso cuando este vacia.

**RF cubiertos:** RF-4.1, RF-4.2, RF-4.3, CE-6, CE-6b.

**Hecho cuando:** el caso de uso devuelve lista vacia y el handler responde `200` con `[]` en lugar de `404`.

---

### T-11 `UserHandlers`: delegate de `PUT user/{id}`

- [X] Anadir `UpdateUserAsync` que asigne `dto.Id = id` antes de invocar `UpdateAsyncInfo`.
- [X] Responder `204` sin cuerpo.

**RF cubiertos:** RF-5.1, RF-5.2, RF-5.5, CE-1, CE-2b.

**Hecho cuando:** el test observa `204` y que el caso de uso recibio el `Id` de la ruta, no el del cuerpo.

---

### T-12 `UserHandlers`: delegate de `PUT userv/{id}`

- [X] Anadir `UpdateUserVisibilityAsync` que proyecte un DTO nuevo con solo `Id` y `Visibility`.
- [X] Comparar `id` con el claim `Id` de la sesion; si coinciden y `Visibility == DISABLED`, responder `403` sin invocar el caso de uso.
- [X] Responder `204` sin cuerpo cuando la modificacion se complete.

**RF cubiertos:** RF-6.1, RF-6.2, RF-6.5, RF-6.7, CE-1, CE-2, CE-2b, CE-14c.

**Hecho cuando:** el caso limite del plan se cumple: `Id` propio con `DISABLED` produce `403` y el caso de uso no llega a invocarse; ademas, un DTO con propiedades extra observa que el caso de uso solo recibe `Id` y `Visibility`.

---

### T-13 `UserHandlers`: delegate de `POST login/`

- [X] Anadir `LoginAsync` que reciba `IUserService` y `JwtTokenFactory`.
- [X] Invocar `Login` con el `UserDTO` del cuerpo y emitir el token a partir del DTO devuelto.
- [X] Responder `200` con el token.

**RF cubiertos:** RF-7.0, RF-7.1, RF-7.2, RF-7.3, RF-7.5, RNF-13.

**Hecho cuando:** un `login` exitoso responde `200` con un token no vacio, y `KeyNotFoundException` o `Exception` del caso de uso se propagan sin ser capturados en el handler.

---

### T-14 `UserHandlers`: delegate de `POST adminv/`

- [X] Anadir `AdminVerificationAsync` que valide que `AdminNickname` y `AdminPwd` lleguen informados, respondiendo `400` si falta cualquiera.
- [X] Entregar el `UserDTO` sin transformar al caso de uso: el mapper ya resuelve la precedencia hacia `Nickname` y `Password`.
- [X] Responder `200` con `{"confirmed":true}` cuando el caso de uso devuelve `true`, y `403` cuando devuelve `false`.
- [X] No leer la contrasena de los`claims`.

**RF cubiertos:** RF-8.0, RF-8.1, RF-8.2, RF-8.3, RF-8.5, RF-8.6, CE-13, CE-14, CE-14d, CE-14e.

**Hecho cuando:** un DTO con `AdminNickname` y `AdminPwd` observa que el caso de uso recibe el DTO intacto, y `false` del caso de uso produce `403`; un DTO sin `AdminPwd` produce `400` sin invocar el caso de uso.

---

## Fase 4 — Traduccion de errores

- **Estado:** completada.

### T-15 Crear el manejador centralizado de excepciones

- [X] Crear `sosMVP/Extensions/ExceptionHandlerExtensions.cs` con un `static IApplicationBuilder UseExceptionHandler(...)`.
- [X] Traducir `KeyNotFoundException` a `404`, `EntityException` a `400`, `ApplicationException` a `400`, `Exception` a `400`.
- [X] Traducir `DbUpdateException` con `SqlException` 2601 o 2627 sobre `uq_alias` a `500`, y el resto de `DbUpdateException` tambien a `500`.
- [X] Responder el `Message` de la excepcion como cuerpo, en espanol.
- [X] No usar `ProducesProblem`: la API no devuelve `ProblemDetails`.

**RF cubiertos:** RF-9.1, RF-9.2, RF-9.3, RF-9.4, RF-9.5, RF-9.6, RNF-6, RF-2.2, CE-4, CE-16.

**Hecho cuando:** un test por cada fila de la tabla de la seccion 4.2 del plan observa el codigo esperado, y el caso de `DbUpdateException` no caido nunca devuelve `400`.

---

## Fase 5 — Registro de los endpoints

- **Estado:** completada.

### T-16 Crear `MapUserEndpoints` con los siete `Map*`

- [X] Crear `sosMVP/Extensions/UserEndpointsExtensions.cs` con `static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder)`.
- [X] Declarar un grupo protegido con `.RequireAuthorization("AdminOnly")` para los seis endpoints de administracion.
- [X] Declarar `login` **fuera** del grupo protegido.
- [X] Aplicar `.Produces` en cada uno de los siete endpoints segun la tabla 4.6.1 del plan, sin aplicarlo sobre el grupo.
- [X] Usar la sobrecarga de solo codigo en los tres endpoints sin cuerpo.

**RF cubiertos:** RF-1.1, RF-1.7, RF-7.5, RNF-13, RNF-8.

**Hecho cuando:** un test recorre el `EndpointDataSource` y encuentra las siete rutas con los metodos HTTP correctos, y cada una tiene exactamente los codigos de `Produces` de la tabla 4.6.1 del plan.

---

## Fase 6 — Composicion

- **Estado:** completada.

### T-17 Componer DI en `ServiceCollectionExtensions`

- [X] Crear `sosMVP/Extensions/ServiceCollectionExtensions.cs` con `AddUserModule()`.
- [X] Registrar con ambito `scoped`: `UserRepository`, `IUserService` -> `UserService`, `ICommonService<UserDTO>` -> `CommonService<UserEntity, UserDTO>`, y el alias con fabrica que resuelva la misma instancia.
- [X] Registrar solo los mappers DTO -> entidad: `UserDTOtoEntityMapper` y `ContactDTOtoEntityMapper`.
- [X] No registrar ningun `IMapper<UserEntity, UserDTO>`: los mappers de entidad a DTO fueron eliminados.
- [X] No usar `new` sobre casos de uso ni DTOs.

**RF cubiertos:** RNF-1, RNF-2, RNF-3.

**Hecho cuando:** un test resuelve `ICommonService<UserDTO>` dos veces dentro del mismo ambito y obtiene la misma instancia, y el contenedor arranca sin pedir `IMapper<UserEntity, UserDTO>`.

---

### T-18 Cablear `Program.cs`

- [X] Leer la cadena con `builder.Configuration.GetConnectionString("Sosv6Db")` antes de `builder.Build()`, lanzando excepcion en espanol si falta.
- [X] Registrar el contexto con `AddDbContext<Sosv6DbContext>(options => options.UseSqlServer(connectionString))`, capturando la variable local en el lambda.
- [X] Anadir `AddAuthentication(...).AddJwtBearer(...)` con issuer, audience y clave.
- [X] Anadir la politica `AdminOnly` y el builder de autorizacion.
- [X] Invocar `UseExceptionHandler(...)`, `UseAuthentication()`, `UseAuthorization()`, `MapUserEndpoints()` y `Run()`, en ese orden.

**RF cubiertos:** RNF-3, RNF-14, RNF-15, RF-1.1, RF-1.2.

**Hecho cuando:** la aplicacion arranca, `UseAuthentication` precede a `UseAuthorization` en el codigo, y borrar la clave `Sosv6Db` del json produce una excepcion al arrancar y no en la primera peticion.

---

## Fase 7 — Pruebas

- **Estado:** completada.

### T-19 Pruebas de la politica de autorizacion

- [X] Crear `test/AdminAuthorizationTests.cs` sobre un `ClaimsPrincipal` fabricado.
- [X] Cubrir rol `admin`, rol distinto, ausencia de claim y sesion corrupta.

**RF cubiertos:** RF-1.3, RF-1.4, RF-1.5, RF-1.6, RF-7.10, RF-7.10b, CE-8, CE-9.

**Hecho cuando:** los cuatro escenarios pasan y ninguno consulta el almacen.

---

### T-20 Pruebas de la fabrica de token

- [X] Crear `test/JwtTokenFactoryTests.cs`.
- [X] Verificar la lista de cierre de claims con un DTO completo.
- [X] Verificar que `Visibility` no aparece en los`claims`.
- [X] Verificar la expiracion a los 30 minutos.
- [X] Verificar que un token emitido antes de un cambio de `Role` conserva el rol emitido hasta vencer.
- [X] Anotar como limitacion conocida que el `401` real lo produce el middleware de autenticacion y no se puede probar sin `WebApplicationFactory`, que la constitucion prohibe.

**RF cubiertos:** RF-7.7, RF-7.7b, RF-7.9, RF-7.11, RF-7.12, CE-7, CE-19, CE-19b, CE-19c, CE-10b, CE-10d, CE-10e, CE-10f, CE-10g.

**Hecho cuando:** el payload no contiene `Password`, `ConfPwd` ni `Visibility`, el test de expiracion falla a los 30 minutos sin renovacion, y el token emitido antes de degradar el rol mantiene `Role = admin` hasta su vencimiento.

---

### T-21 Pruebas de traduccion de excepciones

- [X] Crear `test/ExceptionTranslationTests.cs` con un caso por fila de la tabla 4.2 del plan.

**RF cubiertos:** RF-9.1, RF-9.2, RF-9.3, RF-9.4, RF-9.5, RF-9.6, CE-16.

**Hecho cuando:** las cinco filas de la tabla pasan y todos los mensajes se entregan en espanol.

---

### T-22 Pruebas de los handlers, camino feliz de cada endpoint

- [X] Crear una suite por endpoint segun la tabla 6.1 del plan, cubriendo el camino feliz de los siete.
- [X] Cada test crea sus propios datos, sin sembrado compartido.

**RF cubiertos:** RF-2.1, RF-3.2, RF-4.1, RF-5.2, RF-6.5, RF-7.1, RF-8.2, RNF-7.

**Hecho cuando:** los siete caminos felices pasan y ningun test depende del orden de ejecucion de otro.

---

### T-23 Pruebas de los handlers, caso limite de cada endpoint

- [X] Cubrir el caso limite del plan: auto-deshabilitacion en `PUT userv/{id}`.
- [X] Cubrir `Id` de ruta contra `Id` de cuerpo, y props ignoradas en `userv/{id}`.
- [X] Cubrir `login` con usuario inexistente y credenciales en query.

**RF cubiertos:** CE-1, CE-2, CE-2b, CE-10, CE-10c, CE-12, CE-14c.

**Hecho cuando:** cada caso limite observa el resultado esperado y, cuando aplica, que el caso de uso no fue invocado.

---

### T-24 Pruebas de los handlers, caso de error de cada endpoint

- [X] Traducir cada excepcion del caso de uso al codigo esperado en cada endpoint.
- [X] Cubrir `Alias` duplicado en `POST user/` con `500`.

**RF cubiertos:** RF-2.2, RF-2.3, RF-3.3, RF-5.3, RF-5.4, RF-6.3, RF-6.4, RF-7.2, RF-7.3, RF-8.3, RF-8.4, CE-3, CE-4, CE-5, CE-11, CE-13, CE-14b, CE-15, CE-17, CE-18.

**Hecho cuando:** cada endpoint tiene su prueba de error y todas pasan.

---

### T-25 Pruebas de delegacion de los casos de uso

- [X] Crear `test/UserServiceTests.cs` y `test/CommonServiceUserTests.cs` con los repositorios falsos de T-07.
- [X] Crear `test/UserEntityTests.cs` con las reglas de longitud y `Role` de la entidad.

**RF cubiertos:** RF-2.6, RF-6.3, RF-6.6, RF-7.1, RF-8.1, RNF-11.

**Hecho cuando:** las tres suites pasan y `TypeEntityTests` sigue en verde sin cambios.

---

### T-26 Prueba de la metadata de `Produces`

- [X] Recorrer el `EndpointDataSource` y comprobar los`codigos de cada endpoint.
- [X] Comprobar que `login` no declara `401` ni `403`.

**RF cubiertos:** RF-1.7, RF-7.5, RNF-8, RNF-9.

**Hecho cuando:** la metadata de los siete endpoints coincide exactamente con la tabla 4.6.1 del plan.

**Detalles a tomar en cuenta (verificados al implementar T-16):**

- Aplanar dos data sources. `MapGroup(string.Empty)` reparte los endpoints: un `GroupEndpointDataSource` con los cinco del grupo de administracion y un `RouteEndpointDataSource` con `login` y `adminv`. Hay que concatenar `DataSources.SelectMany(s => s.Endpoints)` y `OfType<RouteEndpoint>()`; leer solo uno de los dos deja endpoints sin comprobar.
- La clase base es `RouteEndpoint`, y la ruta se lee de `RoutePattern.RawText`, no de una propiedad `Route`. El metodo HTTP se lee de `Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods`.
- Materializar los endpoints exige que DI tenga registrados `ICommonService<UserDTO>`, `IUserService` y `JwtTokenFactory`. Sin ellos la inferencia de binding los toma como cuerpo y `RequestDelegateFactory` lanza `Failure to infer one or more parameters`. Basta registrarlos con una fabrica que devuelva `null!`; el test no los invoca.
- El SDK anade un `Produces` implicito de `400` con `Type = typeof(void)` a cada endpoint con cuerpo, por el fallo automatico de binding. Duplica la entrada de metadata pero **no anade codigos nuevos**, porque el `400` ya declarado por la spec. Comparar el conjunto de `StatusCode`, no el numero de entradas.
- `typeof(void)` significa "sin cuerpo". Los tres endpoints sin cuerpo (`POST user/`, `PUT user/{id}`, `PUT userv/{id}`) deben aceptarse con `Type is null || Type == typeof(void)`; exigir `null` falla por el `400` implicito.
- Para los cuatro endpoints con tipo, aislar el tipo real descartando `typeof(void)`; el `200` lo aporta `.Produces<T>(...)` y el resto de entradas no llevan tipo.
- La politica se lee de `Metadata.GetOrderedMetadata<IAuthorizeData>()`. `RequireAuthorization(string)` fija `Policy`; la comprobacion por reflexion de `Policies` cubre la sobrecarga con varias politicas.
- Comparar el conjunto ordenado de codigos con la tabla 4.6.1 por ruta **y** metodo, porque `/user/{id}` esta declarado dos veces: `GET` y `PUT` con contratos distintos.

---

## Fase 8 — Cierre

- **Estado:** completada.

### T-27 Limpieza de mappers no usados

- [X] Eliminar `hexArch/data/mappers/modelToDto/`.
- [X] Comprobar que ningun archivo conserva una referencia a esos tipos.

**RF cubiertos:** RNF-2.

**Hecho cuando:** `dotnet build` sigue en verde y `grep` de `ModeltoDTO` sobre todo el repositorio no devuelve nada.

---

### T-28 Verificacion de los paquetes

- [X] Ejecutar `dotnet list package` en la solucion y en `sosMVP`.
- [X] Confirmar que no aparece ningun paquete fuera de la lista de la constitucion mas `JwtBearer`.

**RF cubiertos:** RNF-14.

**Hecho cuando:** el unico paquete anadido por esta historia es `Microsoft.AspNetCore.Authentication.JwtBearer`.

---

### T-29 Contraste contra los criterios de finalizacion

- [X] Recorrer los 18 criterios de la seccion 8 de la spec vigentes al cerrar la fase 8 (hoy la seccion 8 tiene 22: el cambio de spec anadio los criterios 20, 21 y 22 y renumero 8 a 19) y apuntar cada uno a su prueba.
- [X] Confirmar que ninguna contrasena se persiste en texto plano: el hash lo aplica `PasswordHasher` dentro de `UserRepository`, que no se modifica.
- [X] Ejecutar `dotnet build` y `dotnet test` en verde.

**RF cubiertos:** RNF-10 y el resto de identificadores de la spec.

**Hecho cuando:** los 18 criterios tienen una prueba o una comprobacion manual que los respalda, ninguna ruta de escritura guarda la contrasena sin hashear, y `dotnet test` no reporta fallos. El contraste de los criterios tocados por el cambio de spec se cierra en T-36.

---

## Contraste de los criterios de finalizacion (T-29)

| #  | Criterio de la seccion 8                                              | Respaldo                                                                                                                                                                                                                                                                 |
| -- | --------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| 1  | Los nueve endpoints responden conforme a su criterio                  | `test/UserHandlers*Tests.cs` (camino feliz de los nueve; los dos de firma en la fase 9) y `test/UserEndpointsMetadataTests.cs`                                                                                                                                                                      |
| 2  | `401` en los ocho endpoints protegidos sin sesion valida                      | `test/AdminAuthorizationTests.cs` y limitacion anotada en `test/JwtTokenFactoryTests.cs`: el `401` real lo emite el middleware y no se prueba sin `WebApplicationFactory`, que la constitucion prohibe                                                           |
| 3  | `403` sin rol `admin`, sin ejecutar ni consultar                  | `test/AdminAuthorizationTests.cs` (rol `admin`, rol distinto, ausencia de claim, sesion corrupta); las dos rutas de firma exigen sesion sin `403` por rol, comprobado en `test/UserEndpointsMetadataTests.cs` (fase 9)                                                                                                                                                                    |
| 4  | `Id` de ruta no numerico devuelve `400`                           | reflexion sobre el parametro `int id` de cada handler en `test/UserEndpointsMetadataTests.cs` (`RoutesWithIdentifier_ReceiveItAsAnIntegerSoANonNumericValueIsRejected`, los cinco handlers de rutas `{id}`); la ruta `{id}` sin restriccion `:int` llega al enlace y devuelve `400` (decision de T-33, confirmada en T-36)                                                                                                                                                                     |
| 5  | El`Id` de ruta prevalece sobre el del cuerpo                        | `test/UserHandlersGetUserTests.cs`, `test/UserHandlersUpdateUserTests.cs`, `test/UserHandlersUpdateUserVisibilityTests.cs`, `test/UserHandlersInsertSignatureTests.cs` (fase 9, CE-1)                                                                                                                                         |
| 6  | Solo aplica`Visibility` y rechaza auto-deshabilitarse               | `test/UserHandlersUpdateUserVisibilityTests.cs` (limite y error)                                                                                                                                                                                                       |
| 7  | `GetUsers` no devuelve `DISABLED`                                 | contrato del endpoint en`test/UserHandlersGetUsersTests.cs`; el filtro `Visibilidad == ENABLED` vive en `hexArch/repository/UserRepository.cs:66` y solo se ejerce contra base de datos real, prohibited por la constitucion                                       |
| 8  | `GetUser` devuelve el registro aunque tenga `Visibility = DISABLED` (no filtra en consulta individual; `404` solo para ID inexistente) | `test/UserHandlersGetUserTests.cs` (el handler no filtra y traduce `KeyNotFoundException` a `404`); el filtro `Visibilidad == ENABLED` vive en `hexArch/repository/UserRepository.cs` y solo se ejerce contra base de datos real, prohibido por la constitucion |
| 9  | Los fallos se traducen a`404`/`400`/`500` en espanol            | `test/ExceptionTranslationTests.cs` y las pruebas de error de las nueve suites                                                                                                                                                                                         |
| 10 | Contrato completo de`login`                                         | `test/UserHandlersLoginTests.cs` (limite) y `test/JwtTokenFactoryTests.cs` (expiracion a 30 minutos sin renovacion)                                                                                                                                                  |
| 11 | `Claims` por lista de cierre                                        | `test/JwtTokenFactoryTests.cs` (6 claims, sin `Password` ni `ConfPwd`; `Visibility` ausente)                                                                                                                                                                     |
| 12 | `201`/`204`/`200` con y sin cuerpo                              | `test/UserEndpointsMetadataTests.cs` (tipo de cuerpo por endpoint) y nueve suites (los dos de firma en la fase 9)                                                                                                                                                      |
| 13 | Sin contrasenas ni texto plano ni credenciales por URL                | `test/PasswordHasherTests.cs` (nuevo) mas la comprobacion manual de `UserRepository`: las dos rutas de escritura hashean (lineas 28 y 85) y ninguna proyeccion de lectura expone `Contrasena`; `test/UserEndpointsMetadataTests.cs` descarta parametros de query |
| 14 | `UpdateUserVisibility` solo propaga `Id` y `Visibility`         | `test/UserHandlersUpdateUserVisibilityTests.cs` y `test/CommonServiceUserTests.cs`                                                                                                                                                                                   |
| 15 | `AdminVerification` por `POST`, cuerpo, sesion `admin`          | `test/UserHandlersAdminVerificationTests.cs` y `test/UserEndpointsMetadataTests.cs`                                                                                                                                                                                  |
| 16 | `JwtBearer` como unico paquete                                      | T-28:`dotnet list package` por proyecto y en la solucion                                                                                                                                                                                                               |
| 17 | Cada endpoint con limite y error, datos propios                       | nueve suites en`test/` (los dos de firma en la fase 9), sin sembrado compartido                                                                                                                                                                                                                        |
| 18 | Compila y todas las pruebas pasan                                     | `dotnet build` con 0 errores y `dotnet test` sin fallos (1033 pruebas al cerrar la fase 8)                                                                                                                                                                                                |
| 19 | Sin cadena de conexion en el contexto y arranque fallido sin la clave | comprobacion manual:`hexArch/data/Models/Sosv6DbContext.cs` solo declara los dos constructores y no sobreescribe `OnConfiguring`; `sosMVP/Program.cs:10` lanza antes de `builder.Build()`                                                                        |

| 20 | Un administrador puede editar su propia cuenta con `204`; la unica prohibicion es la autodeshabilitacion | `test/UserHandlersUpdateUserTests.cs` (camino feliz `204` sin comprobacion de rol propio) y CE-14c en `test/UserHandlersUpdateUserVisibilityTests.cs` |
| 21 | `InsertSignature` por `PUT userisre/{id}`, sesion sin rol, `204` con solo `Id` y `Signature`, `404` y `400` | `test/UserHandlersInsertSignatureTests.cs`, `test/SignatureServiceTests.cs` y `test/UserEndpointsMetadataTests.cs` (fase 9, T-31 a T-35) |
| 22 | `GetSignature` por `GET usersre/{id}`, sesion sin rol, `200` con cuerpo `Signature`, `404` y `400`, sin filtro de `Visibility` | `test/UserHandlersGetSignatureTests.cs`, `test/SignatureServiceTests.cs` y `test/UserEndpointsMetadataTests.cs` (fase 9, T-32 a T-35) |

**Nota:** la numeracion sigue la seccion 8 de la spec tras el cambio de spec (22 criterios). Las celdas marcadas "fase 9" se confirmaron al cerrar T-36 (1079 pruebas).

**Contrapruebas de regresion:** se mutaron temporalmente un codigo `Produces` y la auto-deshabilitacion para comprobar que las pruebas correspondientes fallan; ambas mutaciones se revirtieron.

**Observacion sobre paquetes (T-28):** `dotnet list package --vulnerable` no reporta vulnerabilidades en ningun proyecto, pero `dotnet restore` emite avisos `NU1903` por `System.Security.Cryptography.Xml` 9.0.0. Llega de forma transitiva por `Microsoft.EntityFrameworkCore.Tools`, declarado en `hexArch/data/Data.csproj` antes de esta historia, y no de `JwtBearer`. Silenciarlo exigiria un `PackageReference` directo que la constitucion no autoriza sin actualizar antes la spec.

---

## Fase 9 — Endpoints de firma (RF-10, RF-11, RF-1.8)

- **Estado:** completada.
- **Origen:** cambio de spec que anade `InsertSignature` (PUT `userisre/{id}`), `GetSignature` (GET `usersre/{id}`) y el control de acceso RF-1.8 (sesion valida sin restriccion de rol). Los casos de uso y las consultas de firma **ya existen** en `hexArch/` (plan, seccion 0): la fase solo los registra, los expone y los prueba.
- **TDD:** primero los tests de la tarea en rojo, despues el codigo. Una tarea, `dotnet build` y `dotnet test` en verde, y se PARA.

---

### T-30 Firma: DI y dobles de prueba

- [X] Registrar en `AddUserModule` (`sosMVP/Extensions/ServiceCollectionExtensions.cs`): `ISignatureRepository<UserEntity, UserDTO>` con fabrica que resuelva la **misma** instancia de `UserRepository` ya registrada, e `ISignatureService<UserDTO>` -> `SignatureService<UserEntity, UserDTO>`, con ambito `scoped`.
- [X] Anadir en `test/Fakes/` los dobles de `ISignatureService<UserDTO>` e `ISignatureRepository<UserEntity, UserDTO>` que registren la llamada recibida y devuelvan valores programados.
- [X] No tocar `hexArch/repository` ni `hexArch/application` (principio 5, RNF-1).

**RF cubiertos:** RF-10, RF-11, RNF-1, RNF-3.

**Hecho cuando:** resolviendo `ISignatureService<UserDTO>` dentro de un ambito, el caso de uso recibe un `ISignatureRepository` que es la misma instancia de `UserRepository` del ambito, el doble de servicio permite leer el DTO exacto que recibe, y `dotnet test` sigue en verde.

**Verificado (T-30):** `test/SignatureModuleRegistrationTests.cs` comprueba que el contenedor arranca con `ValidateOnBuild`, que los tres puertos de usuario resuelven la unica `UserRepository` del ambito, que `ISignatureService<UserDTO>` es `SignatureService<UserEntity, UserDTO>`, que su `_repository` es esa misma instancia, que los dos registros son `scoped` y que se recrean en cada ambito. `test/SignatureFakesTests.cs` cubre contratos, lectura del DTO/entidad exacta, resultado programado y fallo programado de ambos dobles. `dotnet build` con 0 errores y `dotnet test` con 1047 pruebas y 0 fallos.

---

### T-31 `UserHandlers`: delegate de `PUT userisre/{id}`

- [X] Crear los tests rojos `test/UserHandlersInsertSignatureTests.cs` (camino feliz, caso limite y caso de error) y despues anadir `InsertSignatureAsync` en `sosMVP/Handlers/UserHandlers.cs`, que reciba `ISignatureService<UserDTO>`, el `Id` de ruta y el `UserDTO` del cuerpo.
- [X] Proyectar un DTO nuevo con **solo** `Id` (el de la ruta) y `Signature`; ninguna otra propiedad del cuerpo llega al caso de uso.
- [X] Invocar `InsertSignature` y responder `204` sin cuerpo; `Signature` en `null` tambien responde `204` (CE-23).
- [X] No leer ningun claim de rol ni responder `403`: `KeyNotFoundException` y `EntityException` se propagan al manejador centralizado (`404` y `400`, RF-10.4 y RF-10.5).

**RF cubiertos:** RF-10.1, RF-10.2, RF-10.3, RF-10.4, RF-10.5, RF-10.6, RF-1.8, CE-1, CE-22, CE-23, CE-24, CE-25.

**Hecho cuando:** el camino feliz observa `204` y que el caso de uso recibio el `Id` de la ruta con la `Signature` del cuerpo y nada mas; un cuerpo con propiedades extra observa que se ignoran; `Signature = null` observa `204`; `KeyNotFoundException` y `EntityException` se propagan sin ser capturadas en el delegado; ningun test observa `403` ni consulta el rol.

**Verificado (T-31):** 8 pruebas en `test/UserHandlersInsertSignatureTests.cs`: camino feliz `204` con firma de 255 caracteres; solo `Id` y `Signature` al caso de uso (el resto `null`, CE-22); `Id` de ruta gana al del cuerpo (CE-1); `Signature = null` -> `204`; reflexion sobre los parametros del delegado: no hay `ClaimsPrincipal`, luego no puede leer el rol ni devolver `403` (RF-1.8, CE-28); `KeyNotFoundException` -> `404`; `EntityException` por firma de 256 caracteres -> `400`; `Id` de ruta `0` -> `400`. TDD: rojo con 8 errores CS0117 antes de anadir el delegado. `dotnet build` con 0 errores y `dotnet test` con 1055 pruebas y 0 fallos.

---

### T-32 `UserHandlers`: delegate de `GET usersre/{id}` y `SignatureResponse`

- [X] Crear los tests rojos `test/UserHandlersGetSignatureTests.cs` y despues anadir `GetSignatureAsync`, que reciba `ISignatureService<UserDTO>` y el `Id` de ruta; la peticion no tiene cuerpo, asi que el DTO se genera con `new UserDTO { Id = id }`.
- [X] Anadir el record `SignatureResponse(string? Signature)` en `sosMVP/Handlers/UserHandlers.cs`, junto a `TokenResponse` y `AdminConfirmation`, y devolver `200` con `new SignatureResponse(dto.Signature)`: el cuerpo contiene **unicamente** la propiedad `Signature`.
- [X] No filtrar por `Visibility` ni leer el rol: el usuario `DISABLED` responde `200` (CE-26) y `KeyNotFoundException` se propaga (RF-11.4 -> `404`).

**RF cubiertos:** RF-11.1, RF-11.2, RF-11.3, RF-11.4, RF-11.6, RF-1.8, CE-25, CE-26, CE-27.

**Hecho cuando:** el caso de uso recibe un DTO cuya unica propiedad informada es el `Id` de ruta; el cuerpo del `200` serializado solo contiene `Signature`; un usuario `DISABLED` responde `200`; `KeyNotFoundException` se propaga; ningun test observa `403`.

**Verificado (T-32):** 6 pruebas en `test/UserHandlersGetSignatureTests.cs`: el `200` serializa exactamente una propiedad `signature` (CE-27); el DTO recibido solo lleva el `Id` de ruta, todo lo demas `null` (RF-11.1); resultado con `Visibility = DISABLED` sigue respondiendo `200` (CE-26); firma almacenada en `null` responde `200` con `{"signature":null}` (RF-11.2); reflexion sobre los parametros: sin `ClaimsPrincipal`, luego sin `403` (RF-1.8); `KeyNotFoundException` se propaga y se traduce a `404` (RF-11.4, CE-25). TDD: rojo con 6 errores CS0117 antes de anadir el delegado. `dotnet build` con 0 errores y `dotnet test` con 1061 pruebas y 0 fallos.

---

### T-33 Registro de los dos endpoints en `MapUserEndpoints`

- [x] Declarar un segundo grupo `MapGroup(string.Empty).RequireAuthorization()` **sin politica**, para las dos rutas de firma, separado del grupo `AdminOnly` y de `login` (RF-1.8).
- [x] `MapPut("userisre/{id:int}", ...)` con `InsertSignatureAsync` y `.WithName("InsertSignature")`: `.Produces(204)` + `.Produces(400)` + `.Produces(401)` + `.Produces(404)`, **sin `403`** y con la sobrecarga de solo codigo (sin cuerpo).
- [x] `MapGet("usersre/{id:int}", ...)` con `GetSignatureAsync` y `.WithName("GetSignature")`: `.Produces<SignatureResponse>(200)` + `.Produces(400)` + `.Produces(401)` + `.Produces(404)`, **sin `403`**.
- [x] No declarar `Produces` sobre ningun grupo.

**RF cubiertos:** RF-10.6, RF-11.3, RF-1.8, RNF-8, RNF-9, CE-28.

**Hecho cuando:** el `EndpointDataSource` expone las nueve rutas; las dos de firma estan en el grupo con `RequireAuthorization()` sin `Policy`, las seis de administracion siguen en `AdminOnly` y `login` sigue sin `RequireAuthorization`.

**Verificado (T-33):** segundo grupo `signatureEndpoints = MapGroup(string.Empty).RequireAuthorization()` sin politica (mismo patron que `ContactEndpointsExtensions`), `MapPut /userisre/{id}` con `InsertSignatureAsync` y `MapGet /usersre/{id}` con `GetSignatureAsync`, ambos con `Produces` sin `403` y sin `Produces` en los grupos. Pruebas en `test/UserEndpointsMetadataTests.cs`: `AllNineRoutesAreDeclaredWithTheirExpectedMethods` (nueve rutas), `SignatureRoutes_DeclareTheirEndpointName` (`InsertSignature`/`GetSignature`) y `SignatureRoutes_RequireASessionWithoutAnyRolePolicy` (`IAuthorizeData` sin `Policy`); ademas `test/TypeEndpointsMetadataTests.UserEndpointsMetadataRemainIntact` actualizado a nueve. **Decision aprobada:** se uso `{id}` sin restriccion de ruta en lugar de `{id:int}` literal del texto de la tarea, porque `:int` daria `404` (no `400`) para ids no numericos, violando RF-10.7/RF-11.5/CE-18 y el patron de las 40 rutas existentes. TDD: rojo con 6 fallos (7≠9 + rutas inexistentes) antes de implementar. `dotnet build` con 0 errores y `dotnet test` con 1065 pruebas y 0 fallos.

---

### T-34 Ampliar la metadata de `Produces` y el reparto de politicas

- [x] Ampliar `test/UserEndpointsMetadataTests.cs` a las nueve rutas con los codigos exactos de la tabla 4.6.1 del plan: las dos de firma **sin `403`**, `userisre/{id}` sin cuerpo y `usersre/{id}` con tipo `SignatureResponse`.
- [x] Comprobar el reparto de politicas: seis rutas con `Policy = AdminOnly`, dos de firma con `RequireAuthorization()` sin `Policy`, `login` sin `IAuthorizeData`.
- [x] Dejar intactas las pruebas de `test/AdminAuthorizationTests.cs`: la politica `AdminOnly` sigue exigiendo `Role = admin`.

**RF cubiertos:** RF-1.1, RF-1.2, RF-1.3, RF-1.7, RF-1.8, RF-10.6, RF-11.3, RNF-8, RNF-9, CE-7, CE-28.

**Hecho cuando:** la metadata de los nueve endpoints coincide exactamente con la tabla 4.6.1 del plan, `userisre/{id}` y `usersre/{id}` no llevan `IAuthorizeData` con `Policy`, `login` sigue sin `401` ni `403`, y las seis rutas de administracion conservan `AdminOnly`.

**Verificado (T-34):** `ExpectedContract` ampliado a las nueve filas de la tabla 4.6.1 (firma: `PUT [204,400,401,404]`, `GET [200,400,401,404]`, sin `403`); `EveryRoute_DeclaresExactlyTheStatusCodesOfThePlan` con las dos rutas nuevas; `RoutesWithoutBody_DeclareNoResponseType` incluye `userisre/{id}` (solo codigo, sin cuerpo) y `RoutesWithBody_DeclareTheirResponseType` incluye `usersre/{id}` con `SignatureResponse` (CE-27); reparto de politicas explicito en `AdminRoutes_RequireTheAdminOnlyPolicy` (seis rutas con `Assert.Equal(6, ...)` y `Policy = AdminOnly`), firma sin `Policy` (`SignatureRoutes_RequireASessionWithoutAnyRolePolicy`) y `login` sin `IAuthorizeData` (`Login_RequiresNoAuthorizationAtAll`); `test/AdminAuthorizationTests.cs` sin cambios y en verde. TDD: rojo con 1 fallo (`ProtectedRoutes_RequireTheAdminOnlyPolicy` iteraba las rutas de firma) antes de reescribir el reparto. `dotnet build` con 0 errores y `dotnet test` con 1069 pruebas y 0 fallos.

---

### T-35 Pruebas de delegacion de `SignatureService`

- [x] Crear `test/SignatureServiceTests.cs` con `ISignatureRepository` falso y `UserDTOtoEntityMapper` **real**.
- [x] Cubrir que `Signature` de mas de 255 caracteres o `Id` menor que 1 lanzan `EntityException` durante el mapeo (RF-10.5, CE-24).
- [x] Cubrir que la `KeyNotFoundException` del repositorio se propaga en escritura y en lectura (RF-10.4, RF-11.4, CE-25), y que `GetSignature` devuelve el DTO con la firma (RF-11.2, RF-11.3).

**RF cubiertos:** RF-10.2, RF-10.4, RF-10.5, RF-11.2, RF-11.4, CE-24, CE-25.

**Hecho cuando:** las comprobaciones pasan, la suite no abre ninguna conexion a la base de datos y `hexArch/repository` no se ha modificado.

**Verificado (T-35):** creado `test/SignatureServiceTests.cs` con 6 pruebas y `FakeSignatureRepository` + `UserDTOtoEntityMapper` real: delegacion de `InsertSignature` con entidad mapeada solo con `Id` y `Signature` (RF-10.2); `EntityException` por firma de 256 caracteres y por `Id = 0` durante el mapeo sin alcanzar el repositorio (RF-10.5, CE-24); `KeyNotFoundException` propagada en escritura y lectura (RF-10.4, RF-11.4, CE-25); `GetSignature` devuelve el DTO con `Signature` almacenada y delega el `Id` mapeado (RF-11.2, RF-11.3). Sin codigo de production, sin conexion a BD y `hexArch/repository` intacto. Fase roja no aplica (tarea solo de pruebas sobre implementacion existente; las 6 pasan a la primera). `dotnet build` con 0 errores y `dotnet test` con 1075 pruebas y 0 fallos.

---

### T-36 Cierre de la fase 9: contraste y verificacion

- [x] Contrastar los criterios de finalizacion tocados por el cambio de spec (1, 2, 3, 4, 5, 12, 17, 18, 21 y 22) contra las pruebas de la fase 9 y confirmar la tabla de contraste de T-29.
- [x] Ejecutar `dotnet build` (0 errores) y `dotnet test test/test.csproj` (0 fallos).
- [x] Confirmar con `dotnet list package` que la fase 9 no ha anadido ningun paquete.

**RF cubiertos:** RF-1.1 a RF-1.8, RF-10, RF-11, RNF-14.

**Hecho cuando:** los 22 criterios de la seccion 8 de la spec tienen una prueba o una comprobacion que los respalda, no hay paquetes nuevos y `dotnet test` no reporta fallos.

**Verificado (T-36):** contraste de los 10 criterios tocados por el cambio de spec:

| # | Criterio | Respaldo de la fase 9 |
| --- | --- | --- |
| 1 | Nueve endpoints conforme a su criterio | nueve suites `test/UserHandlers*Tests.cs` (los dos de firma en T-31/T-32) + `test/UserEndpointsMetadataTests.cs` (nueve rutas) |
| 2 | `401` en los ocho protegidos sin sesion | ocho rutas con `RequireAuthorization` (6 `AdminOnly` + 2 firma) en `UserEndpointsMetadataTests.cs`; limitacion anotada ya en T-29 (el `401` real lo emite el middleware) |
| 3 | `403` sin rol `admin`; firma sin `403` por rol | `test/AdminAuthorizationTests.cs` intacto + `SignatureRoutes_RequireASessionWithoutAnyRolePolicy` y códigos exactos sin `403` (T-34) |
| 4 | `Id` no numerico -> `400` | **hueco cerrado en T-36**: reflexion `int id` ampliada a `InsertSignatureAsync` y `GetSignatureAsync`, y ruta requerida `{id}` anadida a las dos rutas de firma (4 casos nuevos); fila 4 de la tabla T-29 corregida de `{id:int}` a `{id}` |
| 5 | `Id` de ruta prevalece sobre el del cuerpo | `RouteId_OverridesTheIdOfTheBody` en `UserHandlersInsertSignatureTests.cs` (+ los tres de admin ya en T-29) |
| 12 | `204`/`200` con y sin cuerpo | tabla 4.6.1 verificada en T-34: `userisre` `204` sin tipo, `usersre` `200` con `SignatureResponse` |
| 17 | Limite y error con datos propios | `UserHandlersInsertSignatureTests.cs` (7: firma de 256 -> `400`, `Id` 0 -> `400`, `404`, delegado sin sesion) y `UserHandlersGetSignatureTests.cs` (6: `DISABLED` -> `200`, firma nula -> `200`, `404`, delegado sin sesion); `test/SignatureServiceTests.cs` (6) sin datos compartidos |
| 18 | Compila y pruebas pasan | `dotnet build` 0 errores; `dotnet test test/test.csproj` -> 1079 pruebas, 0 fallos |
| 21 | `InsertSignature` completo | `UserHandlersInsertSignatureTests.cs` (solo `Id`+`Signature`, `204` sin cuerpo, `404`, `400`) + `SignatureServiceTests.cs` (RF-10.2/10.4/10.5) + metadata `PUT userisre` sin `403` |
| 22 | `GetSignature` completo | `UserHandlersGetSignatureTests.cs` (`200` con `Signature`, `404`, `DISABLED` sin filtrar) + `SignatureServiceTests.cs` (RF-11.2/11.4) + metadata `GET usersre` con `SignatureResponse` sin `403` |

Los otros 12 criterios (6-11, 13-16, 19, 20) quedan confirmados con la tabla de T-29, sin cambios de la fase 9 que los afecten. Tabla T-29 corregida en la fila 4 (decision de T-33: `{id}` sin restriccion, no `{id:int}`) y nota actualizada a las 1079 pruebas. Paquetes: `dotnet list sdd-mvp-test.slnx package` muestra solo `Microsoft.AspNetCore.Authentication.JwtBearer` (sosMVP), el stack de pruebas (test) y EF Core (Data, preexistente); ningun `.csproj` modificado en la fase 9 -> 0 paquetes nuevos (RNF-14). Fase 9 marcada como completada.
