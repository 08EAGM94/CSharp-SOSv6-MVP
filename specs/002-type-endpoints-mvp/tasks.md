# Tareas — Caso de Uso: Tipo

- **Spec:** `specs/002-type-endpoints-mvp/spec.md`
- **Plan:** `specs/002-type-endpoints-mvp/plan.md`
- **Constitución:** `docs/constitution.md`
- **Alcance:** QUIEN hace QUÉ, en orden de dependencia. Cada tarea dura entre 20 y 30 minutos.

Reglas transversales que aplican a todas las tareas:
- Ninguna tarea modifica `hexArch/repository/**` ni `hexArch/application/**` ni `hexArch/domain/**` (principios 2 y 5).
- Solo `sosMVP/` y `test/` reciben código nuevo, salvo donde una tarea lo autoriza explícitamente.
- Todo identificador y comentario en inglés; todo mensaje al usuario final en español (RNF-6).
- Cada tarea termina con `dotnet build` en verde antes de marcarse.
- **Tests primero (en rojo), luego código hasta verde** — cada tarea de handler incluye sus propios tests (camino feliz + caso límite + caso de error).

---

## Fase 0 — Fakes y registro DI (fundación)

### T-01 Crear fakes de repositorio, mapper y casos de uso para Tipo

- [x] Crear `test/Fakes/FakeTypeRepository.cs` que implemente `IRepository<TypeEntity, TypeDTO>` **e** `ISelectRepository<TypeDTO>`. Estilo `FakeCommonRepository`: contadores de llamadas (`AddAsyncInfoCalls`, `GetAsyncInfoCalls`, `GetAsyncAllInfoCalls`, `UpdateAsyncInfoCalls`, `UpdateAsyncVisibilityCalls`), propiedades `LastAddedEntity`, `LastRequestedEntity`, `LastUpdatedEntity`, `LastVisibilityDto`, resultados programables (`InfoResult`, `AllResult`), `ExceptionToThrow`.
- [x] Crear `test/Fakes/FakeTypeMapper.cs` que implemente `IMapper<TypeDTO, TypeEntity>`. Estilo `FakeUserMapper`: campo `MapFunc` inyectable (`Func<TypeDTO, TypeEntity>`), `Map` delega a `MapFunc`.
- [x] Crear `test/Fakes/FakeTypeCommonService.cs` que implemente `ICommonService<TypeDTO>`. Estilo `FakeCommonService`: contadores, `LastAddedDto`, `LastRequestedDto`, `LastUpdatedDto`, `LastVisibilityDto`, `InfoResult`, `AllResult`, `ExceptionToThrow`.
- [x] Crear `test/Fakes/FakeTypeSelectService.cs` que implemente `ISelectService<TypeDTO>`. Estilo `FakeCommonService` pero solo para `GetAsyncInfoForSelects`: contador, `LastRequested`, `SelectResult`, `ExceptionToThrow`.
- [x] No usar `InMemory` de EF Core.

**RF cubiertos:** RNF-4, principio 4.

**Hecho cuando:** un test instancia los fakes, invoca un caso de uso y puede leer el DTO/entidad exacta que recibió; `FakeTypeRepository` expone ambos contratos (`IRepository` e `ISelectRepository`); `FakeTypeSelectService` no se confunde con `FakeUserService` (este último es falso de `IUserService`).

---

### T-02 Crear `AddTypeModule()` en `ServiceCollectionExtensions`

- [x] Añadir método `public static IServiceCollection AddTypeModule(this IServiceCollection services)` en `sosMVP/Extensions/ServiceCollectionExtensions.cs`.
- [x] Registrar con ámbito `scoped`:
  - `TypeRepository`
  - `IRepository<TypeEntity, TypeDTO>` → fábrica que resuelve `TypeRepository`
  - `ISelectRepository<TypeDTO>` → fábrica que resuelve `TypeRepository`
  - `IMapper<TypeDTO, TypeEntity>` → `TypeDTOtoEntityMapper`
  - `CommonService<TypeEntity, TypeDTO>`
  - `ICommonService<TypeDTO>` → fábrica que resuelve **la misma instancia** de `CommonService<TypeEntity, TypeDTO>`
  - `SelectService<TypeDTO>`
  - `ISelectService<TypeDTO>` → fábrica que resuelve **la misma instancia** de `SelectService<TypeDTO>`
- [x] No usar `new` sobre casos de uso ni DTOs.

**RF cubiertos:** RNF-1, RNF-2, RNF-3.

**Hecho cuando:** un test resuelve `ICommonService<TypeDTO>` dos veces dentro del mismo ámbito y obtiene la misma instancia, y el contenedor arranca sin errores.

> **Nota de implementación (T-02):** `CommonService<TEntity, TDTO>` exige `IMapper<ContactDTO, ContactEntity>` en su constructor; ese mapper **no** se vuelve a registrar aquí porque ya lo registra `AddUserModule()` y `Program.cs` llama siempre a `AddUserModule()` antes que a `AddTypeModule()`. El test compone los dos módulos igual que `Program.cs`. La llamada `builder.Services.AddTypeModule()` queda para **T-09**.

---

## Fase 1 — Handlers con sus tests (cada tarea = handler + sus 3 tests)

### T-03 `InsertTypeAsync` handler + tests

- [x] Crear `sosMVP/Handlers/TypeHandlers.cs` con método `public static async Task<IResult> InsertTypeAsync(ICommonService<TypeDTO> commonService, TypeDTO dto)`.
- [x] Invocar `commonService.AddAsyncInfo(dto)` (el caso de uso fija `Visibility = ENABLED`).
- [x] Responder `Results.StatusCode(StatusCodes.Status201Created)` sin cuerpo ni cabecera `Location`.
- [x] Dejar que las excepciones propaguen al `ExceptionHandlerExtensions` centralizado.
- [x] Crear `test/TypeHandlersInsertTypeTests.cs` con **tres tests**:
  1. **Camino feliz:** DTO válido (`Type` 5–50 chars) → `201`, caso de uso invocado una vez, fake registra DTO recibido con `Visibility = ENABLED` (ignora la del cuerpo).
  2. **Caso límite:** Simular `DbUpdateException` con código SQL 2601/2627 (índice `uq_tipo`) → `500` (RF-2.6, RF-8.5, CE-4).
  3. **Caso de error:** `Type` longitud 3 → `EntityException` propagada → `400` (RF-2.3, RF-8.2, CE-11). Usar `HandlerTestSupport.AssertTranslationAsync`.

**RF cubiertos:** RF-2.1, RF-2.2, RF-2.4, RF-2.5, RF-2.3, RF-2.6, RF-8.2, RF-8.5, CE-4, CE-11.

**Hecho cuando:** los tres tests pasan; el test de límite observa `500` sin duplicar registro; el test de error observa `400` con mensaje en español.

---

### T-04 `GetTypeAsync` handler + tests

- [x] Añadir a `TypeHandlers.cs`: `public static async Task<IResult> GetTypeAsync(ICommonService<TypeDTO> commonService, int id)`.
- [x] Construir `new TypeDTO { Id = id }` e invocar `commonService.GetAsyncInfo(dto)`.
- [x] Responder `Results.Ok(typeDto)`.
- [x] Dejar propagar `KeyNotFoundException` y `EntityException`.
- [x] Crear `test/TypeHandlersGetTypeTests.cs` con **tres tests**:
  1. **Camino feliz:** ID existente → `200` con `TypeDTO` completo (`Id`, `Type`, `Visibility`).
  2. **Caso límite:** ID existente con `Visibility = DISABLED` → `200` con DTO completo (no `404`; RF-3.5, decisión usuario opción A). Verificar que `GetAsyncInfo` recibe DTO solo con `Id`.
  3. **Caso de error:** ID inexistente → `KeyNotFoundException` → `404` (RF-3.3, RF-8.1, CE-3). ID no numérico → binding `int` rechaza con `400` (RF-3.4, CE-13) — este último se verifica en metadata test.

**RF cubiertos:** RF-3.1, RF-3.2, RF-3.3, RF-3.4, RF-3.5, RF-8.1, RF-8.2, CE-1, CE-3, CE-10, CE-13.

**Hecho cuando:** los tres tests pasan; el test de límite confirma que `Visibility = DISABLED` no filtra y devuelve `200`; el test de error confirma `404` para ID inexistente.

---

### T-05 `GetTypesAsync` handler + tests

- [x] Añadir a `TypeHandlers.cs`: `public static async Task<IResult> GetTypesAsync(ICommonService<TypeDTO> commonService)`.
- [x] Invocar `commonService.GetAsyncAllInfo()` y responder `Results.Ok(colección)`.
- [x] Responder `200` con `[]` si no hay tipos.
- [x] Crear `test/TypeHandlersGetTypesTests.cs` con **tres tests**:
  1. **Camino feliz:** Repositorio devuelve lista con varios tipos (al menos uno `DISABLED`) → `200` con colección que incluye `DISABLED` (RF-4.2, CE-6b).
  2. **Caso límite:** Repositorio devuelve lista vacía → `200` con `[]` (RF-4.3, CE-6).
  3. **Caso de error:** (ninguno de negocio propio); verificar que excepción genérica del caso de uso → `400` (RF-8.4).

**RF cubiertos:** RF-4.1, RF-4.2, RF-4.3, CE-6, CE-6b.

**Hecho cuando:** los tres tests pasan; el test de límite confirma `200` con `[]` y no `404`; el test feliz confirma que `DISABLED` se incluye.

---

### T-06 `UpdateTypeAsync` handler + tests

- [x] Añadir a `TypeHandlers.cs`: `public static async Task<IResult> UpdateTypeAsync(ICommonService<TypeDTO> commonService, int id, TypeDTO dto)`.
- [x] Asignar `dto.Id = id` antes de invocar `commonService.UpdateAsyncInfo(dto)`.
- [x] Responder `Results.NoContent()`.
- [x] Dejar propagar `KeyNotFoundException` y `EntityException`.
- [x] Crear `test/TypeHandlersUpdateTypeTests.cs` con **tres tests**:
  1. **Camino feliz:** ID existente, `Type` válido → `204`, caso de uso recibe DTO con `Id` de ruta (RF-5.1, RF-5.2, RF-5.5, CE-1).
  2. **Caso límite:** `Id` ruta ≠ `Id` cuerpo → caso de uso recibe `Id` de ruta (CE-1). Verificar que `LastUpdatedDto.Id == ruta.Id`.
  3. **Caso de error:** ID inexistente → `404` (RF-5.3, RF-8.1, CE-5); `Type` longitud 3 → `400` (RF-5.4, RF-8.2, CE-11). Usar `AssertTranslationAsync`.

**RF cubiertos:** RF-5.1, RF-5.2, RF-5.5, RF-5.6, RF-8.1, RF-8.2, CE-1, CE-5, CE-11.

**Hecho cuando:** los tres tests pasan; el test de límite confirma que el `Id` de la ruta prevalece; los tests de error confirman `404` y `400` con mensajes en español.

---

### T-07 `UpdateTypeVisibilityAsync` handler + tests

- [x] Añadir a `TypeHandlers.cs`: `public static async Task<IResult> UpdateTypeVisibilityAsync(ICommonService<TypeDTO> commonService, int id, TypeDTO dto)`.
- [x] Proyectar **un DTO nuevo** con solo `Id = id` y `Visibility = dto.Visibility`; invocar `commonService.UpdateAsyncVisibility(nuevoDto)`.
- [x] Responder `Results.NoContent()`.
- [x] Dejar propagar `KeyNotFoundException` y `ApplicationException` (validación de `Visibility`).
- [x] Crear `test/TypeHandlersUpdateTypeVisibilityTests.cs` con **tres tests**:
  1. **Camino feliz:** ID existente, `Visibility = DISABLED` → `204`, caso de uso recibe DTO solo con `Id` y `Visibility` (RF-6.1, RF-6.2, RF-6.5, CE-1, CE-2).
  2. **Caso límite:** DTO entrante con `Type = "extra"` → caso de uso recibe DTO que **solo** tiene `Id` y `Visibility` (CE-2). Verificar que `LastVisibilityDto.Type` es null.
  3. **Caso de error:** ID inexistente → `404` (RF-6.4, RF-8.1); `Visibility = "INVALID"` → `ApplicationException` → `400` (RF-6.3, RF-8.3, CE-12); `Id` ≤ 0 → `ApplicationException` → `400` (RF-6.6, RF-8.3). Usar `AssertTranslationAsync`.

**RF cubiertos:** RF-6.1, RF-6.2, RF-6.5, RF-6.6, RF-8.1, RF-8.3, CE-1, CE-2, CE-12.

**Hecho cuando:** los tres tests pasan; el test de límite confirma proyección solo de `Id` y `Visibility`; los tests de error confirman `404` y `400` por validaciones.

---

### T-08 `GetTypesForSelectsAsync` handler + tests

- [x] Añadir a `TypeHandlers.cs`: `public static async Task<IResult> GetTypesForSelectsAsync(ISelectService<TypeDTO> selectService)`.
- [x] Invocar `selectService.GetAsyncInfoForSelects()` y responder `Results.Ok(colección)`.
- [x] Crear `test/TypeHandlersGetTypesForSelectsTests.cs` con **tres tests**:
  1. **Camino feliz:** Repositorio devuelve tipos `ENABLED` y `DISABLED` → respuesta `200` solo con `ENABLED` (RF-7.1, RF-7.2, CE-6c).
  2. **Caso límite:** Sin tipos habilitados (solo `DISABLED` o vacío) → `200` con `[]` (RF-7.3, CE-6).
  3. **Caso de error:** Excepción genérica del caso de uso → `400` (RF-8.4).

**RF cubiertos:** RF-7.1, RF-7.2, RF-7.3, CE-6, CE-6c.

**Hecho cuando:** los tres tests pasan; el test feliz confirma filtro `ENABLED`; el test de límite confirma `[]` cuando no hay habilitados.

---

## Fase 2 — Registro de endpoints, metadata y cableado de `Program.cs`

### T-09 `MapTypeEndpoints` + `TypeEndpointsMetadataTests` + cablear `Program.cs`

- [x] Crear `sosMVP/Extensions/TypeEndpointsExtensions.cs` con `static IEndpointRouteBuilder MapTypeEndpoints(this IEndpointRouteBuilder endpoints)`.
- [x] Declarar **dos grupos**:
  - `var authEndpoints = endpoints.MapGroup(string.Empty).RequireAuthorization();` → `InsertType`, `GetType`, `GetTypes`, `GetTypesForSelects`.
  - `var adminEndpoints = endpoints.MapGroup(string.Empty).RequireAuthorization(AdminAuthorization.PolicyName);` → `UpdateType`, `UpdateTypeVisibility`.
- [x] Mapear los 6 endpoints con delegados de `TypeHandlers`.
- [x] Encadenar `Produces` y `WithName` **exactos** (tabla 4.3 del plan):
  - `InsertType`: `.Produces(201).Produces(400).Produces(401).Produces(500).WithName("InsertType")`
  - `GetType`: `.Produces<TypeDTO>(200).Produces(400).Produces(401).Produces(404).WithName("GetType")`
  - `GetTypes`: `.Produces<IEnumerable<TypeDTO>>(200).Produces(401).WithName("GetTypes")`
  - `UpdateType`: `.Produces(204).Produces(400).Produces(401).Produces(403).Produces(404).WithName("UpdateType")`
  - `UpdateTypeVisibility`: `.Produces(204).Produces(400).Produces(401).Produces(403).Produces(404).WithName("UpdateTypeVisibility")`
  - `GetTypesForSelects`: `.Produces<IEnumerable<TypeDTO>>(200).Produces(401).WithName("GetTypesForSelects")`
- [x] **No** aplicar `Produces` sobre los grupos; solo `RequireAuthorization`.
- [x] Endpoints sin cuerpo usan sobrecarga de solo código en `Produces`.
- [x] Crear `test/TypeEndpointsMetadataTests.cs` imitando `UserEndpointsMetadataTests`:
  - `ExpectedContract` con las 6 filas de la tabla 4.3.
  - `MaterializeEndpoints()` que construya `WebApplication.CreateBuilder()`, registre `ICommonService<TypeDTO>` e `ISelectService<TypeDTO>` como `null!`, registre `JwtTestTokens.Factory()`.
  - Tests: 6 rutas declaradas con métodos HTTP correctos, cada una con sus códigos exactos, `WithName` exacto, políticas correctas (4 con `[Authorize]`, 2 con `AdminOnly`), parámetros `{id}` como `int` requerido, sin `ProducesProblem`.
  - Test de regresión: `UserEndpointsMetadataTests` sigue en verde.
- [x] En `Program.cs`, tras `builder.Services.AddUserModule();`, añadir `builder.Services.AddTypeModule();`.
- [x] Tras `app.MapUserEndpoints();`, añadir `app.MapTypeEndpoints();`.
- [x] Verificar orden de middlewares: `UseExceptionHandler()`, `UseAuthentication()`, `UseAuthorization()`, endpoints, `Run()`.

**RF cubiertos:** RF-1.1, RF-1.2, RF-1.3, RF-1.4, RF-1.5, RF-1.6, RF-1.7, RF-9.1, RF-9.2, RF-9.3, RNF-3, RNF-8.

**Hecho cuando:**
- `TypeEndpointsMetadataTests` pasa completamente: 6 rutas, `WithName` exactos, `Produces` exactos, políticas correctas, parámetros `int` requeridos, sin `ProducesProblem`; `UserEndpointsMetadataTests` sigue en verde.
- `dotnet build` compila sin errores; `Program.cs` contiene la llamada a `builder.Services.AddTypeModule()` y a `app.MapTypeEndpoints()`, y el orden de middlewares es `UseExceptionHandler()`, `UseAuthentication()`, `UseAuthorization()`, endpoints, `Run()` (verificable por inspección del archivo).

---

## Fase 3 — Cierre y verificación final

### T-10 Verificación final y contraste contra criterios de finalización

- [x] Ejecutar `dotnet build` → 0 errores.
- [x] Ejecutar `dotnet test` → 0 fallos (todas las suites: Usuario + Tipo).
- [x] Ejecutar `dotnet list package` en la solución → solo paquetes de la constitución + `Microsoft.AspNetCore.Authentication.JwtBearer`.
- [x] Recorrer los 15 criterios de finalización de la spec (sección 8) y apuntar cada uno a su prueba o comprobación:
  1. 6 endpoints disponibles → `TypeHandlers*Tests` (feliz) + `TypeEndpointsMetadataTests`.
  2. 6 endpoints rechazan `401` sin sesión → metadata declara `401` en todos; limitación conocida (middleware real no se prueba sin `WebApplicationFactory`).
  3. `UpdateType` y `UpdateTypeVisibility` rechazan `403` sin rol `admin` → metadata declara `403` + política `AdminOnly` en esos 2; `AdminAuthorizationTests` (existente) cubre la lógica.
  4. `InsertType`, `GetType`, `GetTypes`, `GetTypesForSelects` aceptan cualquier rol autenticado → metadata: esos 4 **no** tienen política `AdminOnly`.
  5. `Id` no numérico → `400` → metadata verifica parámetro `int` requerido.
  6. `Id` de ruta prevalece → `TypeHandlersGetTypeTests`, `UpdateTypeTests`, `UpdateTypeVisibilityTests` (CE-1).
  7. `UpdateTypeVisibility` solo propaga `Id` y `Visibility` → `UpdateTypeVisibilityTests` (CE-2).
  8. `GetTypes` devuelve todos (incluye `DISABLED`) → `GetTypesTests` (CE-6b).
  9. `GetTypesForSelects` devuelve solo `ENABLED` → `GetTypesForSelectsTests` (CE-6c).
  10. `GetType` devuelve aunque `DISABLED` → `GetTypeTests` (límite, RF-3.5).
  11. Fallos → `404`/`400`/`500` en español → `ExceptionTranslationTests` (existente) + tests de error de handlers.
  12. Códigos de éxito correctos (`201`/`204`/`200`) → metadata + tests de respuesta.
  13. `WithName` + `Produces` en cada endpoint → `TypeEndpointsMetadataTests`.
  14. Compila y tests pasan → `dotnet build` + `dotnet test`.
  15. Cada endpoint con prueba feliz, límite, error, datos propios → T-03 a T-08 (cada handler trae sus 3 tests).

**RF cubiertos:** Todos los RF de la spec + criterios de finalización.

**Hecho cuando:** `dotnet build` y `dotnet test` en verde, `dotnet list package` limpio, y los 15 criterios tienen respaldo documental en el test correspondiente.

---

## Lista final de tareas (10 tareas)

1. **T-01** Crear fakes de repositorio, mapper y casos de uso para Tipo — RF: RNF-4
2. **T-02** Crear `AddTypeModule()` en `ServiceCollectionExtensions` — RF: RNF-1, RNF-2, RNF-3
3. **T-03** `InsertTypeAsync` handler + tests (feliz + límite + error) — RF: RF-2.1–2.6, RF-8.2, RF-8.5, CE-4, CE-11
4. **T-04** `GetTypeAsync` handler + tests (feliz + límite + error) — RF: RF-3.1–3.5, RF-8.1, RF-8.2, CE-1, CE-3, CE-10, CE-13
5. **T-05** `GetTypesAsync` handler + tests (feliz + límite + error) — RF: RF-4.1–4.3, CE-6, CE-6b
6. **T-06** `UpdateTypeAsync` handler + tests (feliz + límite + error) — RF: RF-5.1–5.6, RF-8.1, RF-8.2, CE-1, CE-5, CE-11
7. **T-07** `UpdateTypeVisibilityAsync` handler + tests (feliz + límite + error) — RF: RF-6.1–6.6, RF-8.1, RF-8.3, CE-1, CE-2, CE-12
8. **T-08** `GetTypesForSelectsAsync` handler + tests (feliz + límite + error) — RF: RF-7.1–7.3, CE-6, CE-6c
9. **T-09** `MapTypeEndpoints` + `TypeEndpointsMetadataTests` + cablear `Program.cs` — RF: RF-1.1–1.7, RF-9.1–9.3, RNF-3, RNF-8
10. **T-10** Verificación final y contraste criterios — RF: todos + criterios finalización

Total: **10 tareas** (T-01 y T-02 son fundacionales; T-03 a T-08 cada una entrega handler + sus 3 tests; T-09 entrega endpoints + metadata test + cableado de `Program.cs`; T-10 cierre). Se mantiene el límite práctico de ~30 min por tarea y el principio "tests primero, código después" en cada una. La fusión de T-09 y T-10 (antes cableado separado) evita una tarea de un minuto: llamar `AddTypeModule()` y `MapTypeEndpoints()` desde `Program.cs` solo tiene sentido cuando el mapeo ya existe, y el test de metadata de T-09 necesita los endpoints mapeados para afirmar ruta, verbo, `WithName`, `Produces` y política.

---

## Archivos de `test/` que quedan en el plan (tras corrección 2)

| Archivo | Acción | Razones |
| --- | --- | --- |
| `test/Fakes/FakeTypeRepository.cs` | **crear** | Necesario: implementa `IRepository<TypeEntity,TypeDTO>` + `ISelectRepository<TypeDTO>`; los fakes existentes son solo para `UserEntity`. |
| `test/Fakes/FakeTypeMapper.cs` | **crear** | Necesario: implementa `IMapper<TypeDTO,TypeEntity>`; `FakeUserMapper` es para `UserDTO`. |
| `test/Fakes/FakeTypeCommonService.cs` | **crear** | Necesario: implementa `ICommonService<TypeDTO>`; `FakeCommonService` es para `UserDTO`. |
| `test/Fakes/FakeTypeSelectService.cs` | **crear** | Necesario: implementa `ISelectService<TypeDTO>`; nombre inequívoco (no `FakeSelectServiceType` que confunde con `FakeUserService`). |
| `test/TypeModuleRegistrationTests.cs` | **crear** | Necesario en T-02: su "Hecho cuando" exige comprobar el cableado DI. Compone el contenedor como `Program.cs` (`AddUserModule()` + `AddTypeModule()`), lo arranca con `ValidateOnBuild`/`ValidateScopes` y afirma que `ICommonService<TypeDTO>` resuelto dos veces en el mismo ámbito es la misma instancia, que `TypeRepository`, `CommonService<TypeEntity,TypeDTO>` y `SelectService<TypeDTO>` se comparten con sus puertos, y que los 8 registros son `scoped` y se recrean en cada ámbito (no singleton). Cubre RNF-1, RNF-2, RNF-3. |
| `test/TypeHandlersInsertTypeTests.cs` | **crear** | Tests integrados en T-03. |
| `test/TypeHandlersGetTypeTests.cs` | **crear** | Tests integrados en T-04. |
| `test/TypeHandlersGetTypesTests.cs` | **crear** | Tests integrados en T-05. |
| `test/TypeHandlersUpdateTypeTests.cs` | **crear** | Tests integrados en T-06. |
| `test/TypeHandlersUpdateTypeVisibilityTests.cs` | **crear** | Tests integrados en T-07. |
| `test/TypeHandlersGetTypesForSelectsTests.cs` | **crear** | Tests integrados en T-08. |
| `test/TypeEndpointsMetadataTests.cs` | **crear** | Test de metadata integrado en T-09. |
| `test/Support/TypeTestData.cs` | **eliminado del plan** | No se crea: cada test construye su `TypeDTO` en línea (3 propiedades); helper no aporta valor. |
| `test/CommonServiceTypeTests.cs` | **eliminado del plan** | No se crea: `CommonService<TEntity,TDTO>` es genérico y ya cubierto por `CommonServiceUserTests.cs`. Validaciones genéricas se afirman en tests de handler vía `ExceptionToThrow` + `AssertTranslationAsync`. |
| `test/SelectServiceTypeTests.cs` | **eliminado del plan** | No se crea: `SelectService<TDTO>` es genérico y delega a `ISelectRepository`; se verifica indirectamente en `TypeHandlersGetTypesForSelectsTests`. |

---

## Mapa RF → implementación → test final (compacto)

| RF | Implementación principal | Test |
| --- | --- | --- |
| RF-1.1–1.7 | `TypeEndpointsExtensions.cs`, `Program.cs` | `TypeEndpointsMetadataTests`, `AdminAuthorizationTests` (existente) |
| RF-2.1–2.6 | `TypeHandlers.cs` (InsertType), `TypeRepository` (existente) | `TypeHandlersInsertTypeTests` (feliz/límite/error) |
| RF-3.1–3.5 | `TypeHandlers.cs` (GetType) | `TypeHandlersGetTypeTests` (feliz/límite/error) |
| RF-4.1–4.3 | `TypeHandlers.cs` (GetTypes) | `TypeHandlersGetTypesTests` (feliz/límite/error) |
| RF-5.1–5.6 | `TypeHandlers.cs` (UpdateType) | `TypeHandlersUpdateTypeTests` (feliz/límite/error) |
| RF-6.1–6.6 | `TypeHandlers.cs` (UpdateTypeVisibility), `CommonService` (existente) | `TypeHandlersUpdateTypeVisibilityTests` (feliz/límite/error) |
| RF-7.1–7.3 | `TypeHandlers.cs` (GetTypesForSelects), `SelectService` (existente) | `TypeHandlersGetTypesForSelectsTests` (feliz/límite/error) |
| RF-8.1–8.6 | `ExceptionHandlerExtensions.cs` (existente) | `ExceptionTranslationTests` (existente) + tests de error handlers |
| RF-9.1–9.3 | `TypeEndpointsExtensions.cs` | `TypeEndpointsMetadataTests` |
| RNF-1, RNF-2 | `ServiceCollectionExtensions.cs` (AddTypeModule) | `TypeModuleRegistrationTests` (DI resuelve por abstracción, instancia única por ámbito) |
| RNF-3 | `ServiceCollectionExtensions.cs`, `Program.cs` | `TypeModuleRegistrationTests` (los 8 registros son `scoped`), `dotnet build` |
| RNF-6 | Código en inglés, excepciones en español | `ExceptionHandlerExtensions` usa `exception.Message` |
| RNF-10 | Sin nuevos paquetes | `dotnet list package` verifica |

---

## Confirmación de correcciones aplicadas

✅ **RNF fuera de rango eliminados:** En todo el `plan.md` y `tasks.md` solo aparecen **RNF-1, RNF-2, RNF-3, RNF-6, RNF-8, RNF-10** (los que existen en la spec 002). Se eliminaron referencias a RNF-4, RNF-5, RNF-7, RNF-9, RNF-13, RNF-14 que no existen en esta spec (provenían de la spec 001).

✅ **RF inventados eliminados:** Todos los RF citados existen en la spec 002 (RF-1 a RF-9). No hay RF-10 ni superiores.

✅ **Tasks reducidas a 10** (antes 11, antes 16), con tests integrados en cada handler (T-03 a T-08) y metadata test + cableado integrados en T-09.

✅ **Fakes corregidos:** Nombres inequívocos (`FakeTypeCommonService`, `FakeTypeSelectService`), estilo copiado de fakes de Usuario existentes, sin helpers innecesarios (`TypeTestData.cs` eliminado).

✅ **Suites de casos de uso genéricos eliminadas:** `CommonServiceTypeTests.cs` y `SelectServiceTypeTests.cs` quitadas con justificación; su cobertura se absorbe en tests de handler + `ExceptionTranslationTests` existente.

✅ **Referencias internas revisadas:** La lista final de tareas cita T-03 a T-08, T-09, T-10 de forma consistente; no quedan referencias a números viejos (p. ej. "ver T-11" o "según T-10" anterior).