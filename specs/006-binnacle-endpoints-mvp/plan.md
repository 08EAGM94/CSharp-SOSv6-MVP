# Plan — Spec 006: Caso de uso Bitácora

- **Spec:** `specs/006-binnacle-endpoints-mvp/spec.md` (Estado: borrador).
- **Estado del plan:** borrador. No se implementa ninguna tarea hasta que el usuario lo apruebe.
- **Constitución:** `docs/constitution.md`. Verificación al final de cada tarea: `dotnet build` y `dotnet test test/test.csproj`.

---

## 1. Punto de partida

| Componente | Estado actual | RF que ya cubre |
|---|---|---|
| `hexArch/domain/entities/BinnacleEntity.cs` | Existe, sin cambios (setters con `EntityException`) | RF-2.3, RF-3.5, RF-6.5, RF-7.5, RF-8.5, RF-10.5, RF-11.5 |
| `hexArch/application/.../primaryPorts/IBinnacleService.cs` | Existe, sin cambios | Interfaz del caso de uso |
| `hexArch/application/useCases/BinnacleService.cs` | Existe; valida `Id` y `Status` (RF-6.3) e `Id` y `Visibility` (RF-7.3). **Falta:** validación de `page`/`elemsKey` y normalización del alta | RF-6.3, RF-7.3 |
| `hexArch/data/.../BinnacleDTOtoEntityMapper.cs` | Existe, sin cambios (`CancelDesc` → `Hints`) | RF-10.3 (mapper a entidad) |
| `hexArch/repository/BinnacleRepository.cs` | Existe con los cambios ya hechos para esta spec (constantes `"FollowupList"`/`"BinnaclesReport"`, filtro de visibilidad, control de estatus). **No se modifica más** (D2) | RF-2.2 (estatus, visibilidad, fecha de inicio), RF-3.3/3.4, RF-4.4/4.5/4.6, RF-5.3/5.4/5.6, RF-6.2/6.4, RF-7.4/7.8, RF-8.3/8.4/8.7, RF-9.3/9.4/9.5, RF-10.3/10.4/10.7, RF-11.3/11.4/11.7 |
| `sosMVP/Extensions/ExceptionHandlerExtensions.cs` | Existe, sin cambios (`KeyNotFound`→404, `EntityException`/`ApplicationException`/genérica→400, `DbUpdateException`→500) | RF-12.1 a RF-12.5 |
| `sosMVP/Security/AdminAuthorization.cs` | Existe (`ReadUserIdClaim`, política `AdminOnly`) | RF-1.3 a RF-1.7 (mecanismo) |
| Handlers, endpoints y DI de bitácora en `sosMVP` | **No existen** | — |
| Pruebas de bitácora en `test/` | **No existen** | — |

**Trabajo que queda:** la capa `sosMVP` (handlers, rutas, DI, `Program.cs`), dos ajustes en `BinnacleService` y toda la batería de tests.

---

## 2. Archivos y responsabilidades

| # | Archivo | Acción | Responsabilidad | RF |
|---|---|---|---|---|
| 1 | `sosMVP/Handlers/BinnacleHandlers.cs` | Nuevo | 10 handlers estáticos + record `BinnaclesReportRequest`. Leen el `claim` `Id` de la sesión, fijan `Id` de ruta, delegan en `IBinnacleService` y devuelven el código de éxito. | RF-1.7, RF-2.1, RF-2.5, RF-2.6, RF-3.1, RF-3.2, RF-4.1, RF-5.1, RF-6.1, RF-7.1, RF-7.2, RF-8.1, RF-9.1, RF-10.1, RF-11.1 y todos los `2xx` |
| 2 | `sosMVP/Extensions/BinnacleEndpointsExtensions.cs` | Nuevo | Dos grupos de rutas (autenticado / `AdminOnly`), `Produces(...)` exactos por endpoint y `WithName(...)`. | RF-1.1, RF-1.3, RF-1.4, RF-1.6, RF-13.1, RF-13.2, RF-13.3 |
| 3 | `sosMVP/Extensions/ServiceCollectionExtensions.cs` | Modificar | Añadir `AddBinnacleModule()`: registro `scoped` de repositorio, mapper y caso de uso. | RNF-2, RNF-3 |
| 4 | `sosMVP/Program.cs` | Modificar | Llamar `AddBinnacleModule()` y `MapBinnacleEndpoints()`. | RNF-3, RF-1 |
| 5 | `hexArch/application/useCases/BinnacleService.cs` | Modificar | (a) `NormalizeForInsert` antes de dar de alta; (b) validación de `page`/`elemsKey` en `GetAsyncAllInfo`; (c) extraer `IsValidStatus`/`IsValidVisibility` como helpers puros (ya estaban inline). | RF-2.2, RF-4.3, RF-5.2, RF-6.3, RF-7.3, RF-12.3 |
| 6 | `test/Fakes/FakeBinnacleRepository.cs` | Nuevo | Fake de `IBinnacleRepository` para probar `BinnacleService` sin base de datos; registra el DTO de entrada y las excepciones configurables. | Soporte de `BinnacleServiceTests` |
| 7 | `test/Fakes/FakeBinnacleService.cs` | Nuevo | Fake de `IBinnacleService` que registra llamadas, devuelve resultados configurables y lanza excepciones configurables. | Soporte de todos los tests |
| 8 | `test/Support/BinnacleEndpointTestHost.cs` | Nuevo | Host de pruebas con JWT real, política `AdminOnly` y despacho por ruta (patrón de `ContactEndpointTestHost`). | RF-1 |
| 9 | `test/BinnacleEndpointsMetadataTests.cs` | Nuevo | Rutas, nombres públicos, códigos `Produces`, grupo de autorización y parámetros de ruta `int`. | RF-13, RF-1.3/1.6, CE-7 |
| 10 | `test/BinnacleAuthorizationTests.cs` | Nuevo | 401 sin sesión / sesión caducada / firma alterada / sesión sin `claim` `Id`; 403 sin rol `admin` en los 3 endpoints administradores; aceptación de rol `user` en los 7 restantes; el caso de uso no se alcanza. | RF-1.1 a RF-1.8, CE-6, CE-8, CE-9, CE-10, CE-11, CE-18 |
| 11 | `test/BinnacleHandlers*Tests.cs` (uno por endpoint) | Nuevo | Ensamblado del DTO, códigos de éxito, traducción de errores y casos límite de cada endpoint. | RF-2 a RF-12 (ver §7) |
| 12 | `test/BinnacleServiceTests.cs` | Nuevo | Validaciones del caso de uso y normalización del alta. | RF-2.2, RF-4.3, RF-5.2, RF-6.3, RF-7.3, RF-12.3 |
| 13 | `test/BinnacleEntityTests.cs` | Nuevo | Reglas de negocio de la entidad aplicables a bitácora. | RF-2.3, RF-8.5, RF-10.5, RF-11.5, RF-3.5 |
| 14 | `test/BinnacleModuleRegistrationTests.cs` | Nuevo | Comprobación de registros `scoped`. | RNF-3 |

---

## 3. Interfaz

### 3.1 Rutas y grupos (contrato de `BinnacleEndpointsExtensions`)

| Grupo | Ruta | Método | `WithName` | `Produces(...)` | Handler |
|---|---|---|---|---|---|
| autenticado | `/binnacle/` | POST | `InsertBinnacle` | 201, 400, 401, 500 | `InsertBinnacleAsync` |
| autenticado | `/binnacle/{id}` | GET | `GetBinnacle` | 200, 400, 401, 404 | `GetBinnacleAsync` |
| autenticado | `/binnaclesfu/{page}/{elemsKey}` | GET | `FollowupList` | 200, 400, 401 | `FollowupListAsync` |
| `AdminOnly` | `/binnaclesr/` | POST | `BinnaclesReport` | 200, 400, 401, 403 | `BinnaclesReportAsync` |
| `AdminOnly` | `/binnacle/{id}` | PUT | `UpdateBinnacle` | 204, 400, 401, 403, 404 | `UpdateBinnacleAsync` |
| `AdminOnly` | `/binnaclev/{id}` | PUT | `UpdateBinnacleVisibility` | 204, 400, 401, 403, 404 | `UpdateBinnacleVisibilityAsync` |
| autenticado | `/binnaclefup/{id}` | PUT | `FollowupPartial` | 204, 400, 401, 404 | `FollowupPartialAsync` |
| autenticado | `/binnaclera/{id}` | PUT | `ResetActivities` | 204, 400, 401, 404 | `ResetActivitiesAsync` |
| autenticado | `/binnaclecb/{id}` | PUT | `CancelBinnacle` | 204, 400, 401, 404 | `CancelBinnacleAsync` |
| autenticado | `/binnaclefsh/{id}` | PUT | `FinishBinnacle` | 204, 400, 401, 404 | `FinishBinnacleAsync` |

Los códigos coinciden con la tabla de RF-13. Los GET con `{id}`, `{page}` y `{elemsKey}` se declaran como `int` (el binding devuelve `400` ante un valor no numérico, CE-7).

### 3.2 Firmas de los handlers

```csharp
Task<IResult> InsertBinnacleAsync (IBinnacleService service, ClaimsPrincipal principal, BinnacleDTO dto)
Task<IResult> GetBinnacleAsync    (IBinnacleService service, ClaimsPrincipal principal, int id)
Task<IResult> FollowupListAsync   (IBinnacleService service, ClaimsPrincipal principal, int page, int elemsKey)
Task<IResult> BinnaclesReportAsync(IBinnacleService service, BinnaclesReportRequest request)
Task<IResult> UpdateBinnacleAsync (IBinnacleService service, int id, BinnacleDTO dto)
Task<IResult> UpdateBinnacleVisibilityAsync(IBinnacleService service, int id, BinnacleDTO dto)
Task<IResult> FollowupPartialAsync(IBinnacleService service, ClaimsPrincipal principal, int id, BinnacleDTO dto)
Task<IResult> ResetActivitiesAsync(IBinnacleService service, ClaimsPrincipal principal, int id)   // sin cuerpo
Task<IResult> CancelBinnacleAsync (IBinnacleService service, ClaimsPrincipal principal, int id, BinnacleDTO dto)
Task<IResult> FinishBinnacleAsync (IBinnacleService service, ClaimsPrincipal principal, int id, BinnacleDTO dto)

public sealed record BinnaclesReportRequest(int Page, int ElemsKey, Dictionary<string, string> BinnFilter);
```

`IBinnacleService` **no cambia** (ya declara los diez métodos).

### 3.3 Registro DI (`AddBinnacleModule`)

```csharp
services.AddScoped<BinnacleRepository>();
services.AddScoped<IBinnacleRepository>(sp => sp.GetRequiredService<BinnacleRepository>());
services.AddScoped<IMapper<BinnacleDTO, BinnacleEntity>, BinnacleDTOtoEntityMapper>();
services.AddScoped<IBinnacleService, BinnacleService>();
```

---

## 4. Funciones puras

| Función | Responsabilidad | RF |
|---|---|---|
| `BinnacleHandlers.WithSessionUser(BinnacleDTO dto, int sessionUserId) : BinnacleDTO` | Devuelve el DTO con `UserId` = id de la sesión (el `Id` de ruta lo pone cada handler aparte). | RF-2.1, RF-3.2, RF-8.1, RF-9.1, RF-10.1, RF-11.1 |
| `BinnacleService.NormalizeForInsert(BinnacleDTO dto) : BinnacleDTO` | Vacía `Status`, `ActivitiesDone`, `Hints` y `CancelDesc` del DTO de alta: son valores que fija el sistema. | RF-2.2 |
| `BinnacleService.IsValidPagination(int? page, int? elemsKey) : bool` | `page ≥ 1` y `elemsKey ≥ 1`. | RF-4.3, RF-5.2 |
| `BinnacleService.IsValidStatus(string? status) : bool` | `status` ∈ {en proceso, falta confirmar, cancelado, finalizado}. | RF-6.3 |
| `BinnacleService.IsValidVisibility(string? visibility) : bool` | `visibility` ∈ {ENABLED, DISABLED}. | RF-7.3 |

**Sobre `hoy`:** las dos únicas fechas que fija el sistema (fecha de inicio del alta, fecha de fin del cierre) las calcula hoy `BinnacleRepository` con la fecha del sistema, y el repositorio queda fuera del alcance de este plan (D2). Por eso ninguna función de este plan recibe `hoy`; si en una futura spec se toca el repositorio, `DateOnly hoy` deberá inyectarse desde la capa de aplicación para poder probarse.

---

## 5. Algoritmo en pseudocódigo

```
InsertBinnacleAsync(service, principal, dto):
    userId ← AdminAuthorization.ReadUserIdClaim(principal)
    si userId es nulo → 401 "La sesión no contiene un identificador de usuario válido." y fin
    dto ← WithSessionUser(dto, userId)
    await service.AddAsyncInfo(dto)            // el caso de uso normaliza (RF-2.2)
    → 201 sin cuerpo y sin cabecera Location

GetBinnacleAsync(service, principal, id):
    userId ← ReadUserIdClaim(principal); si nulo → 401 y fin
    dto ← new BinnacleDTO { Id = id, UserId = userId }
    → 200 con await service.GetAsyncInfo(dto)  // 404/400 llegan como excepción

FollowupListAsync(service, principal, page, elemsKey):
    userId ← ReadUserIdClaim(principal); si nulo → 401 y fin
    dto ← new BinnacleDTO { UserId = userId }             // solo UserId (RF-4.1)
    → 200 con await service.GetAsyncAllInfo(dto, page, elemsKey, binnFilter: null, "FollowupList")

BinnaclesReportAsync(service, request):
    → 200 con await service.GetAsyncAllInfo(null, request.Page, request.ElemsKey,
                                             request.BinnFilter, "BinnaclesReport")

UpdateBinnacleAsync(service, id, dto):
    dto.Id ← id
    await service.UpdateAsyncInfo(dto) → 204

UpdateBinnacleVisibilityAsync(service, id, dto):
    dto ← new BinnacleDTO { Id = id, Visibility = dto.Visibility }   // solo esas dos propiedades (RF-7.2)
    await service.UpdateAsyncVisibility(dto) → 204

FollowupPartialAsync(service, principal, id, dto):
    userId ← ReadUserIdClaim(principal); si nulo → 401 y fin
    dto.Id ← id; dto ← WithSessionUser(dto, userId)
    await service.FollowupPartialAsync(dto) → 204

ResetActivitiesAsync(service, principal, id):            // no tiene cuerpo
    userId ← ReadUserIdClaim(principal); si nulo → 401 y fin
    dto ← new BinnacleDTO { Id = id, UserId = userId }
    await service.ResetActivitiesAsync(dto) → 204

CancelBinnacleAsync / FinishBinnacleAsync:               // idéntico a FollowupPartial
    ... → 204

// Cambios en BinnacleService
AddAsyncInfo(dto):
    dto ← NormalizeForInsert(dto)
    await _repository.AddAsyncInfo(_toEntityMapper.Map(dto))

GetAsyncAllInfo(dto, page, elemsKey, binnFilter, controllerAction):
    si !IsValidPagination(page, elemsKey) →
        throw ApplicationException("La página y los elementos por página deben ser números mayores que cero.")
    ... (resto tal cual)

UpdateAsyncInfo(dto):     // ya existe, solo se extrae IsValidStatus/IsValidVisibility
UpdateAsyncVisibility(dto):// ya existe, solo se extrae IsValidVisibility
```

El orden de las respuestas de error lo gobierna `ApiExceptionHandler`: `KeyNotFoundException` → 404, `EntityException`/`ApplicationException`/genérica → 400, `DbUpdateException` → 500 (RF-12).

---

## 6. Decisiones (con su alternativa descartada)

- **D1 — Reglas de estatus (RF-8.7, RF-9.4, RF-10.7, RF-11.7).** Permanecen en `BinnacleRepository`, tal y como están hoy; los tests solo cubren que esas excepciones se traduzcan a `400` con mensaje en español (RF-12.4) desde el endpoint. *Alternativa descartada:* extraer las reglas a funciones puras de dominio (exige modificar el repositorio, D2) y añadir un paquete EF a los tests (viola el principio 1 de la constitución; `ExecuteUpdateAsync` además no funciona en InMemory). *Consecuencia asumida:* la decisión de qué estatus se rechaza no se ejecuta en `dotnet test`; queda anotada como límite de verificación en §7.
- **D2 — §7 de la spec vs. repositorio.** La spec lista «Modificación de `hexArch/repository`» en *Fuera de alcance*, pero RF-4.4, RF-7.8, RF-8.7, RF-9.4, RF-10.7 y RF-11.7 ya están implementados ahí. El usuario decidió **no tocar la spec**: el conflicto queda registrado aquí y este plan **no hace ningún cambio adicional** en `hexArch/repository`. *Alternativa descartada:* eliminar el punto de §7 (requiere petición explícita sobre la spec).
- **D3 — Validación de `page`/`elemsKey` en el caso de uso.** `BinnacleService.GetAsyncAllInfo` lanza `ApplicationException` cuando alguno no es ≥ 1. *Alternativa descartada:* que cada handler devuelva `400` directamente (contradice RF-12, que atribuye esos `400` a `ApplicationException` del caso de uso, y repetiría la regla en dos endpoints).
- **D4 — RF-2.2 se cumple en el caso de uso.** `NormalizeForInsert` vacía `Status`, `ActivitiesDone`, `Hints` y `CancelDesc` antes de mapear, porque `BinnacleRepository.AddAsyncInfo` persiste `ActivitiesDone`/`Hints` tal cual y un `Status` de más de 20 caracteres dispararía un `EntityException` espurio. *Alternativa descartada:* limpiarlo en el handler (mezcla la regla «lo que fija el sistema» con presentación) y limpiarlo en el repositorio (D2).
- **D5 — `BinnaclesReportRequest` como record en `sosMVP`.** Es un wrapper de petición (presentación): `page`, `elemsKey` y `binnFilter`. *Alternativa descartada:* DTO en `HexArch.Application` (la spec lo describe como envoltorio de la petición, no como parte del caso de uso; el caso de uso ya recibe los tres valores como parámetros).
- **D6 — Dos grupos de rutas.** Grupo autenticado + grupo `AdminOnly`, igual que `ContactEndpointsExtensions`. *Alternativa descartada:* aplicar la política endpoint a endpoint (más verboso y propenso a olvidar `RequireAuthorization` en un endpoint).
- **D7 — Tests sin base de datos.** Fakes de `IBinnacleService` + `BinnacleEndpointTestHost` con JWT real, patrón de las specs 001–005. *Alternativa descartada:* conectarse a una base de datos de prueba (el entorno no tiene SQL Server y añadir proveedor NuGet viola el principio 1).

---

## 7. Estrategia de tests con `dotnet test`

Orden por tarea: **primero el test en rojo, después el código**, y al cerrar la tarea `dotnet build` + `dotnet test test/test.csproj` en verde (principio 4).

| Archivo de test | Qué comprueba | RF |
|---|---|---|
| `test/BinnacleEndpointsMetadataTests.cs` | Las 10 rutas existen con método, nombre público y códigos `Produces` exactos; los 7 endpoints no administradores no exigen la política `AdminOnly` y los 3 sí; ningún endpoint es anónimo; `{id}`, `{page}`, `{elemsKey}` son `int` obligatorios. | RF-13.1/13.2/13.3, RF-1.3/1.6, CE-7 |
| `test/BinnacleAuthorizationTests.cs` | 401 sin token, con token caducado y con token de firma alterada; 401 con token válido sin `claim` `Id` (CE-11); 403 en `BinnaclesReport`, `UpdateBinnacle` y `UpdateBinnacleVisibility` con rol `user`, con rol ausente o con valor fuera de `admin`/`user`; los otros 7 aceptan rol `user`; en todos los rechazos el fake no registra ninguna llamada. | RF-1.1 a RF-1.8, CE-6, CE-8, CE-9, CE-10, CE-11, CE-18 |
| `test/BinnacleHandlersInsertBinnacleTests.cs` | `201` sin cuerpo y sin `Location`; el DTO llega con el `UserId` de la sesión ignorando el del cuerpo; `Status`/`ActivitiesDone`/`Hints`/`CancelDesc` vacíos; `EntityException`→400 (CE-15), `ApplicationException`→400, `DbUpdateException`→500 (RF-2.4); mensaje en español; sesión sin `claim` `Id`→401 sin llegar al caso de uso. | RF-2.1 a RF-2.6, RF-1.7, CE-15, CE-16, CE-17, CE-22 |
| `test/BinnacleHandlersGetBinnacleTests.cs` | DTO con `Id` de ruta y `UserId` de sesión; `200` con la bitácora; `KeyNotFoundException`→404 (CE-4, CE-5); `EntityException`→400 con `id` cero/negativo (CE-7); `401` sin `claim` `Id`. | RF-3.1 a RF-3.5, RF-1.7, CE-4, CE-5, CE-7 |
| `test/BinnacleHandlersFollowupListTests.cs` | DTO con **solo** `UserId`; argumentos `page`, `elemsKey`, `"FollowupList"` y `binnFilter` nulo; `200` con `PaginationResult`; `ApplicationException`→400 con `page`/`elemsKey` ≤ 0 (CE-7); `401` sin `claim` `Id`. | RF-4.1 a RF-4.4, RF-1.7, CE-7 |
| `test/BinnacleHandlersBinnaclesReportTests.cs` | El wrapper entrega `page`, `elemsKey`, `binnFilter` y la constante `"BinnaclesReport"` con DTO nulo; `200` con `PaginationResult`; `ApplicationException`→400; con sesión de rol `user` llega `403` sin ejecutar el caso de uso. | RF-5.1 a RF-5.3, RF-5.5, RF-1.4, CE-7, CE-9 |
| `test/BinnacleHandlersUpdateBinnacleTests.cs` | `Id` de la ruta sustituye al del cuerpo; `204` sin cuerpo; `ApplicationException` (Status nulo/desconocido)→400 (CE-12); `KeyNotFoundException`→404 (CE-4); `EntityException`→400 con `id` inválido; `403` sin rol `admin`. | RF-6.1 a RF-6.7, CE-1, CE-4, CE-7, CE-12, CE-25 |
| `test/BinnacleHandlersUpdateVisibilityTests.cs` | El DTO que llega al caso de uso contiene únicamente `Id` y `Visibility` (CE-2); `204`; `ApplicationException` (visibility distinta de ENABLED/DISABLED)→400 (CE-13); `KeyNotFoundException`→404; `403` sin rol `admin`. | RF-7.1 a RF-7.7, CE-1, CE-2, CE-4, CE-13 |
| `test/BinnacleHandlersFollowupPartialTests.cs` | `Id` de ruta + `UserId` de sesión; `204` sin cuerpo; `KeyNotFoundException`→404 (CE-4); `EntityException`→400 (CE-15); **la excepción de estatus prohibido que lanza el repositorio se traduce a `400` con mensaje en español** (RF-8.7 vía RF-12.4, D1); `401` sin `claim` `Id`. | RF-8.1 a RF-8.7, RF-1.7, CE-4, CE-15, CE-23 |
| `test/BinnacleHandlersResetActivitiesTests.cs` | El cuerpo no se lee: el DTO solo lleva `Id` y `UserId` (CE-3); `204`; `KeyNotFoundException`→404; `EntityException`→400; traducción a `400` de la excepción de estatus cancelado/finalizado (RF-9.4, D1); `401` sin `claim` `Id`. | RF-9.1 a RF-9.6, CE-3, CE-4, CE-14 |
| `test/BinnacleHandlersCancelBinnacleTests.cs` | `Id` de ruta + `UserId` de sesión; `204`; `KeyNotFoundException`→404; `EntityException`→400 por `CancelDesc` corto (CE-15); traducción a `400` de la excepción de estatus (RF-10.7, D1); `401` sin `claim` `Id`. | RF-10.1 a RF-10.7, CE-1, CE-4, CE-15, CE-26 |
| `test/BinnacleHandlersFinishBinnacleTests.cs` | `Id` de ruta + `UserId` de sesión; `204`; `KeyNotFoundException`→404; `EntityException`→400 por firma > 255 (CE-15); traducción a `400` de la excepción de estatus (RF-11.7, D1); `401` sin `claim` `Id`. | RF-11.1 a RF-11.7, CE-1, CE-4, CE-15, CE-27 |
| `test/BinnacleServiceTests.cs` | `NormalizeForInsert` vacía los campos que fija el sistema; `page`/`elemsKey` ≤ 0 → `ApplicationException` (RF-12.3); `Status` nulo/desconocido → `ApplicationException`; `Visibility` fuera de ENABLED/DISABLED → `ApplicationException`; mensajes en español. | RF-2.2, RF-4.3, RF-5.2, RF-6.3, RF-7.3, RF-12.3, RF-12.6, CE-12, CE-13 |
| `test/BinnacleEntityTests.cs` | `ContactId` ≤ 0, `Service` < 15, `Amount` ≤ 0 y `CustomerSignature` > 255 → `EntityException`; `ActivitiesDone`/`Hints` con valor y < 15 y `Id`/`UserId` ≤ 0 → `EntityException`; la nulidad de texto se admite. | RF-2.3, RF-8.5, RF-10.5, RF-11.5 |
| `test/BinnacleModuleRegistrationTests.cs` | Los cuatro registros del módulo existen con vida `scoped` y resuelven. | RNF-3 |
| `test/ExceptionTranslationTests.cs` (ya existe) | Se amplía si hace falta con los mensajes propios de bitácora: `KeyNotFound`→404, `EntityException`→400, `ApplicationException`→400, genérica→400, `DbUpdateException`→500, todos en español. | RF-12.1 a RF-12.6 |

**Límites de verificación asumidos (D1, D2, D7):** las consultas de `BinnacleRepository` (filtrado de listados, filtro de visibilidad, control de estatus, actualización dinámica por estatus) no se ejecutan en `dotnet test` porque requieren base de datos y el entorno no puede añadir proveedor. Se verifican por revisión de código contra la spec y, en su superficie de error, por la traducción de excepciones descrita arriba; por eso los criterios de finalización 11, 12, 19 y 20 quedan cubiertos solo en su parte de contrato (códigos, ensamblado del DTO y traducción de errores).

---

## 8. Cobertura de los requisitos no funcionales

- **RNF-1, RNF-2:** sin cambios de capa: `sosMVP` solo habla con `IBinnacleService`; el caso de uso solo habla con `IBinnacleRepository`.
- **RNF-3:** registro `scoped` en `AddBinnacleModule` (§3.3) + `test/BinnacleModuleRegistrationTests.cs`.
- **RNF-4, RNF-10:** no se añade ningún paquete NuGet (D7).
- **RNF-5:** la persistencia sigue siendo EF Core en `BinnacleRepository`.
- **RNF-6:** identificadores en inglés, mensajes de error en español (comprobado en los tests de traducción).
- **RNF-7:** `204`/`201` sin cuerpo y `200` con el objeto devuelto por el caso de uso.
- **RNF-9:** los códigos y nombres documentados son los de la tabla de §3.1.
