# Plan de Implementación — Caso de Uso: Tipo

- **Spec que autoriza:** `specs/002-type-endpoints-mvp/spec.md`
- **Constitución:** `docs/constitution.md`
- **Alcance de este documento:** CÓMO se implementa la spec. Cita los RF que autoriza cada decisión.
- **Estado:** sin bloqueantes.

---

## 0. Inventario del código existente (verificado)

| Elemento                             | Realidad actual                                                                                                                                                                                                                                                                                                                                                                                                                                                                   |
| ------------------------------------ | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `ICommonService<TDTO>`             | `AddAsyncInfo(TDTO, ContactDTO?)`, `GetAsyncInfo(TDTO)`, `GetAsyncAllInfo()`, `UpdateAsyncInfo(TDTO)`, `UpdateAsyncVisibility(TDTO)` — ya usado por `CommonService<UserEntity, UserDTO>`                                                                                                                                                                                                                                                                             |
| `CommonService<TEntity, TDTO>`     | Implementa`ICommonService<TDTO>`. Props privadas: `_repository` (`IRepository<TEntity,TDTO>`), `_toEntityMapper` (`IMapper<TDTO,TEntity>`), `_toContactEntityMapper` (`IMapper<ContactDTO,ContactEntity>`) — **reutilizable para `TypeEntity, TypeDTO`**                                                                                                                                                                                                   |
| `ISelectService<TDTO>`             | `GetAsyncInfoForSelects()` — interfaz ya existente                                                                                                                                                                                                                                                                                                                                                                                                                             |
| `SelectService<TDTO>`              | Implementa`ISelectService<TDTO>`. Prop privada: `_repository` (`ISelectRepository<TDTO>`) — **reutilizable para `TypeDTO`**                                                                                                                                                                                                                                                                                                                                        |
| `TypeRepository`                   | Implementa`IRepository<TypeEntity, TypeDTO>` **e** `ISelectRepository<TypeDTO>`. Métodos: `AddAsyncInfo` (fija `Visibility = "ENABLED"`), `GetAsyncInfo` (lanza `KeyNotFoundException` si no existe), `GetAsyncAllInfo` (no filtra por visibilidad), `UpdateAsyncInfo` (lanza `KeyNotFoundException` si 0 filas), `UpdateAsyncVisibility` (lanza `KeyNotFoundException` si 0 filas), `GetAsyncInfoForSelects` (filtra `Visibility == "ENABLED"`) |
| `TypeEntity`                       | Reglas:`Id` ≥ 1 si no null; `Type` longitud 5–50 si no null. Lanza `EntityException` en español                                                                                                                                                                                                                                                                                                                                                                          |
| `TypeDTO`                          | `Id` (int?), `Type` (string?), `Visibility` (string?)                                                                                                                                                                                                                                                                                                                                                                                                                       |
| `TypeDTOtoEntityMapper`            | Implementa`IMapper<TypeDTO, TypeEntity>`                                                                                                                                                                                                                                                                                                                                                                                                                                        |
| Excepciones de dominio               | `EntityException`, `ApplicationException` (heredan de `Exception`)                                                                                                                                                                                                                                                                                                                                                                                                          |
| Excepciones de repositorio           | `KeyNotFoundException` (no encontrado), `DbUpdateException` (conflicto unicidad por índice `uq_tipo` sobre `Tipo1`)                                                                                                                                                                                                                                                                                                                                                      |
| `AdminAuthorization`               | Política`AdminOnly` con `RequireClaim("Role", "admin")`, métodos `ReadRoleClaim`, `ReadUserIdClaim` — **ya existe y se reutiliza**                                                                                                                                                                                                                                                                                                                               |
| `ExceptionHandlerExtensions`       | Traduce`KeyNotFoundException`→`404`, `EntityException`/`ApplicationException`→`400`, `DbUpdateException`→`500`, `Exception`→`400`. Mensajes en español — **ya existe y se reutiliza**                                                                                                                                                                                                                                                               |
| `JwtTokenFactory` / `JwtOptions` | Emisión y validación de token, claims por lista de cierre —**ya existe; no se toca para Tipo**                                                                                                                                                                                                                                                                                                                                                                           |
| `UserEndpointsExtensions`          | Patrón de referencia:`MapGroup` + `RequireAuthorization(AdminAuthorization.PolicyName)` + `Produces` + `WithName` en cada endpoint — **se imita**                                                                                                                                                                                                                                                                                                                 |
| `ServiceCollectionExtensions`      | `AddUserModule()` registra todo `scoped` — **se amplía con `AddTypeModule()`**                                                                                                                                                                                                                                                                                                                                                                                      |
| Fakes en`test/Fakes/`              | **Concretos de Usuario, no genéricos**: `FakeCommonRepository` (solo `IRepository<UserEntity,UserDTO>`), `FakeCommonService` (solo `ICommonService<UserDTO>`), `FakeUserMapper` (`IMapper<UserDTO,UserEntity>`), `FakeUserRepository`, `FakeUserService`, `FakeContactMapper`. **Para Tipo se necesitan fakes propios** que implementen los mismos contratos pero para `TypeEntity/TypeDTO`.                                                     |
| Helpers en`test/Support/`          | `TestHttp` (`ExecuteAsync`, `TranslateAsync`, `TestHttpResponse`), `HandlerTestSupport` (`AssertTranslationAsync`, `CreateSession`, `AssertEmptyBodyAsync`, `AssertBodyIsInSpanishAsync`, `AssertCreatedWithoutLocationAsync`, `AssertNoContentAsync`, `ReadJsonBody`, `ReadToken`), `JwtTestTokens` — **se reutilizan tal cual**.                                                                                                             |
| Tests en`test/`                    | `UserEndpointsMetadataTests`, suites por handler, `CommonServiceUserTests`, `AdminAuthorizationTests`, `ExceptionTranslationTests` — **se imita estructura para Tipo**.                                                                                                                                                                                                                                                                                            |

---

## 1. Paquetes NuGet

**Ninguno nuevo.** La spec 001 ya autorizó `Microsoft.AspNetCore.Authentication.JwtBearer`. EF Core y xUnit ya están. Principio 1 de la constitución: no se añade ninguna dependencia.

---

## 2. Casos de uso: interfaces, propiedades privadas y métodos a usar

### 2.1 `CommonService<TypeEntity, TypeDTO>` — endpoints `InsertType`, `GetType`, `GetTypes`, `UpdateType`, `UpdateTypeVisibility`

Se registra como `CommonService<TypeEntity, TypeDTO>` implementando `ICommonService<TypeDTO>`.

| Endpoint            | Método del caso de uso                                   | Retorno                        | RF que autoriza        |
| ------------------- | --------------------------------------------------------- | ------------------------------ | ---------------------- |
| `POST /type/`     | `AddAsyncInfo(TypeDTO dto, ContactDTO? contact = null)` | `Task` (void)                | RF-2.1, RF-2.4         |
| `GET /type/{id}`  | `GetAsyncInfo(TypeDTO dto)`                             | `Task<TypeDTO>`              | RF-3.1, RF-3.2, RF-3.5 |
| `GET /type/`      | `GetAsyncAllInfo()`                                     | `Task<IEnumerable<TypeDTO>>` | RF-4.1, RF-4.3         |
| `PUT /type/{id}`  | `UpdateAsyncInfo(TypeDTO dto)`                          | `Task` (void)                | RF-5.2, RF-5.5         |
| `PUT /typev/{id}` | `UpdateAsyncVisibility(TypeDTO dto)`                    | `Task` (void)                | RF-6.2, RF-6.5         |

Propiedades privadas (inyectadas por constructor, `readonly`):

- `_repository` — `IRepository<TypeEntity, TypeDTO>`: satisface `TypeRepository` (RNF-2).
- `_toEntityMapper` — `IMapper<TypeDTO, TypeEntity>`: satisface `TypeDTOtoEntityMapper`.
- `_toContactEntityMapper` — `IMapper<ContactDTO, ContactEntity>`: se mantiene por construcción aunque `AddAsyncInfo` de Tipo no lo use (el caso de uso es genérico).

**No se modifican los casos de uso.** Sus firmas ya satisfacen RF-2.4, RF-5.5 y RF-6.5 (los tres son `void`).

Comportamientos que el caso de uso ya aporta y que la API solo debe traducir:

- `AddAsyncInfo` fija `Visibility = "ENABLED"` en el repositorio (RF-2.2).
- `UpdateAsyncVisibility` valida `Id > 0` y que `Visibility` sea `ENABLED` o `DISABLED`, acumulando mensajes y lanzando `ApplicationException` (cubre RF-6.3, RF-6.6, CE-12).
- `GetAsyncInfo` **no filtra por visibilidad** (coherente con RF-3.5 y decisión del usuario opción A); lanza `KeyNotFoundException` solo si el ID no existe (RF-3.3, RF-8.1).
- `GetAsyncAllInfo` no filtra por visibilidad (RF-4.1, RF-4.2).
- `GetAsyncInfoForSelects` filtra `Visibility == "ENABLED"` (RF-7.1, RF-7.2).

### 2.2 `SelectService<TypeDTO>` — endpoint `GetTypesForSelects`

Implementa `ISelectService<TypeDTO>`. Propiedad privada: `_repository` (`ISelectRepository<TypeDTO>`), satisface `TypeRepository`.

| Endpoint          | Método del caso de uso      | Retorno                        | RF que autoriza |
| ----------------- | ---------------------------- | ------------------------------ | --------------- |
| `GET /typesct/` | `GetAsyncInfoForSelects()` | `Task<IEnumerable<TypeDTO>>` | RF-7.1, RF-7.3  |

---

## 3. Control de acceso (RF-1)

Dos grupos con políticas distintas, usando `RequireAuthorization` / `AdminAuthorization.PolicyName`:

| Grupo                 | Endpoints                                                         | Política                                                    | Qué verifica                                                                                                 |
| --------------------- | ----------------------------------------------------------------- | ------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------- |
| **Autenticado** | `InsertType`, `GetType`, `GetTypesForSelects` | `[Authorize]` (solo autenticación válida, cualquier rol) | Sesión válida →`401` si no hay o vencida (RF-1.1, RF-1.2). **No verifica rol `admin`** (RF-1.6). |
| **Admin**       | `GetTypes`, `UpdateType`, `UpdateTypeVisibility`                          | `RequireAuthorization(AdminAuthorization.PolicyName)`      | Sesión válida**y** claim `Role = "admin"` → `403` si no (RF-1.3, RF-1.4, RF-1.5).                |

**Por qué** (decisión técnica, alternativa descartada): el middleware de ASP.NET Core ya impone el orden "autenticación primero, autorización después" (`UseAuthentication()` antes de `UseAuthorization()`). Un filtro propio (`IEndpointFilter`) obligaría a replicar a mano la distinción `401`/`403` que la spec hace explícita. Se descarta porque añade código sin valor y rompe la coherencia con `UserEndpointsExtensions`.

**Reutilización de `AdminAuthorization`:** la política `AdminOnly` y los métodos `ReadRoleClaim`/`ReadUserIdClaim` ya existen en `sosMVP/Security/AdminAuthorization.cs`. No se duplica código; los handlers de `UpdateType` y `UpdateTypeVisibility` usan la misma política y, si necesitan leer el `Id` del claim (para comparaciones futuras), llaman a `AdminAuthorization.ReadUserIdClaim(principal)`.

---

## 4. Endpoints: método de extensión con `IEndpointRouteBuilder`

**Decisión técnica:** los seis endpoints se registran en una clase de extensión `static` sobre `IEndpointRouteBuilder`, en `sosMVP/Extensions/TypeEndpointsExtensions.cs`, expuesta como `app.MapTypeEndpoints()`. `Program.cs` añade la llamada junto a `app.MapUserEndpoints()`.

**Por qué** (alternativa descartada): idéntico a la spec 001 — el SDK de minimal API ya provee `IEndpointRouteBuilder` con binding de cuerpo JSON, binding de `{id}` y `400` automático ante cuerpo o ruta malformados. Controllers obligan a `AddControllers` y attributes; lambdas en `Program.cs` saturan el archivo.

### 4.1 Estructura de `Program.cs` tras el cambio (solo lo nuevo para Tipo)

```csharp
// ... configuración existente de Usuario ...
builder.Services.AddTypeModule();          // nuevo: registra repositorio, mappers, casos de uso de Tipo
// ... authentication/authorization existente ...

var app = builder.Build();
// ... middleware existente ...
app.MapUserEndpoints();
app.MapTypeEndpoints();                    // nuevo
app.Run();
```

Todos los registros son `scoped`, cumpliendo el principio 3. No hay un solo `new` sobre casos de uso ni DTOs en los endpoints.

### 4.2 Endpoint por endpoint

| Endpoint            | Delegado                                                        | Respuesta                          | RF cubiertos                       |
| ------------------- | --------------------------------------------------------------- | ---------------------------------- | ---------------------------------- |
| `POST /type/`     | `ICommonService<TypeDTO>.AddAsyncInfo(dto)`                   | `201` sin cuerpo ni `Location` | RF-2.1 a RF-2.6, CE-4              |
| `GET /type/{id}`  | `GetAsyncInfo(dto con Id de ruta)`                            | `200` con `TypeDTO`            | RF-3.1 a RF-3.5, CE-1, CE-3, CE-10 |
| `GET /type/`      | `GetAsyncAllInfo()`                                           | `200` con colección             | RF-4.1 a RF-4.4, CE-6, CE-6b       |
| `PUT /type/{id}`  | `UpdateAsyncInfo(dto con Id de ruta)`                         | `204` sin cuerpo                 | RF-5.1 a RF-5.6, CE-1, CE-5        |
| `PUT /typev/{id}` | `UpdateAsyncVisibility(dto con Id de ruta y solo Visibility)` | `204` sin cuerpo                 | RF-6.1 a RF-6.6, CE-1, CE-2        |
| `GET /typesct/`   | `ISelectService<TypeDTO>.GetAsyncInfoForSelects()`            | `200` con colección             | RF-7.1 a RF-7.3, CE-6, CE-6c       |

Reglas transversales dentro de los delegados:

- **Id de ruta gana al cuerpo** (RF-3.1, RF-5.1, RF-6.1, CE-1): el endpoint asigna `dto.Id = id` antes de invocar el caso de uso, sin leer el `Id` del cuerpo.
- **`{id}` declarado como `int`**: una ruta no numérica produce `400` automático del binding, cubriendo RF-3.4, RF-5.6, RF-6.6 y CE-13.
- **`UpdateTypeVisibility` proyecta a un DTO nuevo** con solo `Id` y `Visibility` (RF-6.2, CE-2). No se reutiliza el DTO recibido, para que ninguna otra propiedad llegue al caso de uso.
- **No se devuelven entidades en escrituras:** los tres endpoints de escritura responden sin cuerpo (RF-2.4, RF-2.5, RF-5.5, RF-6.5, CE-2b).
- **Validación de `Type` (longitud 5–50)** ocurre en `TypeEntity` al mapear DTO→entidad, lanza `EntityException` → `400` (RF-2.3, RF-5.4, RF-8.2).
- **Validación de `Visibility`** ocurre en `CommonService.UpdateAsyncVisibility` (lanza `ApplicationException`) → `400` (RF-6.3, RF-8.3).
- **Conflicto de unicidad** por índice `uq_tipo` sobre `Tipo1` → `DbUpdateException` → `ExceptionHandlerExtensions` traduce a `500` (RF-2.6, RF-8.5, CE-4).
- **Traducción de excepciones** centralizada en `ExceptionHandlerExtensions` existente (RF-8.1 a RF-8.6, CE-15, CE-16).

### 4.3 Contrato de respuestas con `Produces` en cada endpoint

**Decisión técnica:** idéntica a la spec 001 — cada `Map*` encadena `Produces` para declarar su contrato. No se aplica sobre el grupo.

| Endpoint                 | `WithName(...)`        | Códigos`Produces(...)`                   |
| ------------------------ | ------------------------ | ------------------------------------------- |
| `InsertType`           | `InsertType`           | `201`, `400`, `401`, `500`          |
| `GetType`              | `GetType`              | `200`, `400`, `401`, `404`          |
| `GetTypes`             | `GetTypes`             | `200`, `401`, `403`                   |
| `UpdateType`           | `UpdateType`           | `204`, `400`, `401`, `403`, `404` |
| `UpdateTypeVisibility` | `UpdateTypeVisibility` | `204`, `400`, `401`, `403`, `404` |
| `GetTypesForSelects`   | `GetTypesForSelects`   | `200`, `401`                            |

Reglas de uso (idénticas a spec 001, secciones 4.6.2 y 4.6.3):

- Se declara en el endpoint, no en el grupo.
- Los tres endpoints sin cuerpo (`InsertType`, `UpdateType`, `UpdateTypeVisibility`) usan la sobrecarga de solo código.
- `Produces` no ejecuta nada; `401` y `403` los produce el middleware; declararlos documenta.
- `login` (de Usuario) es la excepción y no lleva `401`/`403`; aquí **todos** los endpoints de Tipo llevan `401` porque todos exigen sesión.
- `ProducesProblem` queda fuera: los errores se construyen a mano en `ExceptionHandlerExtensions` con mensaje en español.
- OpenAPI no se genera (paquete `Microsoft.AspNetCore.OpenApi` no autorizado), pero la metadata queda en `EndpointDataSource` y es verificable en test.

---

## 5. Estructura de archivos prevista

| Archivo                                                                        | Acción             | Contenido                                                                                                                                                                                                                                                                                                                            | Constitución   |
| ------------------------------------------------------------------------------ | ------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | --------------- |
| `sosMVP/Extensions/TypeEndpointsExtensions.cs`                               | **crear**     | `static IEndpointRouteBuilder MapTypeEndpoints(this IEndpointRouteBuilder)` con 6 `Map*`, cada uno con su `Produces` y `WithName`                                                                                                                                                                                            | Principios 1, 3 |
| `sosMVP/Handlers/TypeHandlers.cs`                                            | **crear**     | 6 métodos públicos`static async Task<IResult>` (o `IResult`), inyectando `ICommonService<TypeDTO>` / `ISelectService<TypeDTO>`, lógica de autorización y proyección de DTOs                                                                                                                                             | Principio 1     |
| `sosMVP/Extensions/ServiceCollectionExtensions.cs`                           | **modificar** | Añadir`AddTypeModule()` con registros `scoped`: `TypeRepository`, `IRepository<TypeEntity,TypeDTO>`, `ISelectRepository<TypeDTO>`, `IMapper<TypeDTO,TypeEntity>`, `CommonService<TypeEntity,TypeDTO>`, `ICommonService<TypeDTO>` (alias a misma instancia), `SelectService<TypeDTO>`, `ISelectService<TypeDTO>` | Principios 1, 3 |
| `sosMVP/Program.cs`                                                          | **modificar** | Llamar a`builder.Services.AddTypeModule()` y `app.MapTypeEndpoints()`                                                                                                                                                                                                                                                            | Principio 3     |
| `test/Fakes/FakeTypeRepository.cs`                                           | **crear**     | Implementa`IRepository<TypeEntity, TypeDTO>` e `ISelectRepository<TypeDTO>` para tests de handlers y casos de uso. Estilo de `FakeCommonRepository`: contadores, `Last*`, `ExceptionToThrow`.                                                                                                                              | Principio 4     |
| `test/Fakes/FakeTypeMapper.cs`                                               | **crear**     | Implementa`IMapper<TypeDTO, TypeEntity>` (estilo `FakeUserMapper`: `MapFunc` inyectable).                                                                                                                                                                                                                                      | Principio 4     |
| `test/Fakes/FakeTypeCommonService.cs`                                        | **crear**     | Implementa`ICommonService<TypeDTO>` (estilo `FakeCommonService`).                                                                                                                                                                                                                                                                | Principio 4     |
| `test/Fakes/FakeTypeSelectService.cs`                                        | **crear**     | Implementa`ISelectService<TypeDTO>` (estilo `FakeCommonService` pero para el contrato `ISelectService`).                                                                                                                                                                                                                       | Principio 4     |
| `test/TypeHandlersInsertTypeTests.cs`                                        | **crear**     | Camino feliz + límite + error de`InsertTypeAsync`                                                                                                                                                                                                                                                                                 | Principio 4     |
| `test/TypeHandlersGetTypeTests.cs`                                           | **crear**     | Camino feliz + límite + error de`GetTypeAsync`                                                                                                                                                                                                                                                                                    | Principio 4     |
| `test/TypeHandlersGetTypesTests.cs`                                          | **crear**     | Camino feliz + límite + error de`GetTypesAsync`                                                                                                                                                                                                                                                                                   | Principio 4     |
| `test/TypeHandlersUpdateTypeTests.cs`                                        | **crear**     | Camino feliz + límite + error de`UpdateTypeAsync`                                                                                                                                                                                                                                                                                 | Principio 4     |
| `test/TypeHandlersUpdateTypeVisibilityTests.cs`                              | **crear**     | Camino feliz + límite + error de`UpdateTypeVisibilityAsync`                                                                                                                                                                                                                                                                       | Principio 4     |
| `test/TypeHandlersGetTypesForSelectsTests.cs`                                | **crear**     | Camino feliz + límite + error de`GetTypesForSelectsAsync`                                                                                                                                                                                                                                                                         | Principio 4     |
| `test/TypeEndpointsMetadataTests.cs`                                         | **crear**     | Verifica ruta, verbo,`WithName`, `Produces`, política de los 6 endpoints (equivalente a `UserEndpointsMetadataTests`).                                                                                                                                                                                                        | Principio 4     |
| `hexArch/repository/**`                                                      | **no tocar**  | Consultas existentes — principio 5                                                                                                                                                                                                                                                                                                  | Principio 5     |
| `hexArch/application/**`, `hexArch/domain/**`, `hexArch/data/mappers/**` | **no tocar**  | Ya satisfacen la spec                                                                                                                                                                                                                                                                                                                | RNF-1           |

**Archivos eliminados del plan respecto a versión anterior (con justificación):**

- `test/Support/TypeTestData.cs` — **no se crea**. Cada test construye su `TypeDTO` en línea (3 propiedades simples); un helper no aporta valor y añade mantenimiento.
- `test/CommonServiceTypeTests.cs` — **no se crea**. `CommonService<TEntity,TDTO>` es genérico y su comportamiento ya está cubierto por `CommonServiceUserTests.cs`. Para Tipo, la validación genérica (`Visibility` en `UpdateAsyncVisibility`, `ENABLED` en `AddAsyncInfo`) se afirma en los tests de handler usando `FakeTypeCommonService.ExceptionToThrow` + `HandlerTestSupport.AssertTranslationAsync` (patrón de `ExceptionTranslationTests.cs`).
- `test/SelectServiceTypeTests.cs` — **no se crea**. `SelectService<TDTO>` es genérico y delega a `ISelectRepository`. La delegación se verifica indirectamente en `TypeHandlersGetTypesForSelectsTests` (el handler invoca `ISelectService` y el fake registra la llamada). Tests dedicados al caso de uso genérico repetirían código sin aportar cobertura específica de Tipo.

---

## 6. Estrategia de tests (principio 4)

**Restricción:** solo xUnit. `Microsoft.AspNetCore.Mvc.Testing` y `Microsoft.AspNetCore.TestHost` prohibidos ⇒ no hay pruebas de integración HTTP reales. Los códigos `401` y `403` se verifican probando la lógica de autorización (política y claims), no el pipeline completo. Limitación conocida, igual que en spec 001.

`test/test.csproj` ya referencia `sosMVP.csproj` (hecho en T-03 de spec 001). Las clases de `sosMVP` son `public`.

### 6.1 Suites (una por handler + metadata)

| Suite                                     | Sujeto                                                                 | Cubre                                                                               |
| ----------------------------------------- | ---------------------------------------------------------------------- | ----------------------------------------------------------------------------------- |
| `TypeHandlersInsertTypeTests`           | `TypeHandlers.InsertTypeAsync` con `ICommonService<TypeDTO>` falso | RF-2.1 a RF-2.6, CE-4, CE-11 (feliz + límite + error)                              |
| `TypeHandlersGetTypeTests`              | `TypeHandlers.GetTypeAsync`                                          | RF-3.1 a RF-3.5, CE-1, CE-3, CE-10, CE-13 (feliz + límite + error)                 |
| `TypeHandlersGetTypesTests`             | `TypeHandlers.GetTypesAsync`                                         | RF-4.1 a RF-4.4, CE-6, CE-6b (feliz + límite + error)                              |
| `TypeHandlersUpdateTypeTests`           | `TypeHandlers.UpdateTypeAsync`                                       | RF-5.1 a RF-5.6, CE-1, CE-5, CE-11 (feliz + límite + error)                        |
| `TypeHandlersUpdateTypeVisibilityTests` | `TypeHandlers.UpdateTypeVisibilityAsync`                             | RF-6.1 a RF-6.6, CE-1, CE-2, CE-12 (feliz + límite + error)                        |
| `TypeHandlersGetTypesForSelectsTests`   | `TypeHandlers.GetTypesForSelectsAsync`                               | RF-7.1 a RF-7.3, CE-6, CE-6c (feliz + límite + error)                              |
| `TypeEndpointsMetadataTests`            | `EndpointDataSource` con `MapTypeEndpoints`                        | RF-9.1, RF-9.2, RF-9.3 (verifica ruta, verbo,`WithName`, `Produces`, política) |

### 6.2 Requisitos por endpoint (constitución, principio 4)

Cada endpoint necesita **mínimo tres pruebas**: camino feliz, caso límite, caso de error. Ejemplos:

- `InsertTypeAsync`:

  - **Feliz:** DTO con `Type` válido → `201`, caso de uso recibe DTO con `Visibility = ENABLED` ignorando la del cuerpo.
  - **Límite:** `Type` duplicado (índice `uq_tipo`) → `500`, caso de uso lanza `DbUpdateException`.
  - **Error:** `Type` < 5 o > 50 caracteres → `400` (`EntityException`).
- `GetTypeAsync`:

  - **Feliz:** ID existente → `200` con `TypeDTO` completo.
  - **Límite:** ID existente con `Visibility = DISABLED` → `200` (no `404`; RF-3.5, decisión usuario opción A).
  - **Error:** ID inexistente → `404` (`KeyNotFoundException`); ID no numérico → `400` (binding).
- `GetTypesAsync`:

  - **Feliz:** Tipos registrados → `200` con colección que incluye `DISABLED`.
  - **Límite:** Sin tipos → `200` con `[]`.
  - **Límite:** Usuario no admin → `403` (RF-4.4).
  - **Error:** (ninguno de negocio propio; `401` por sesión se prueba en metadata).
- `UpdateTypeAsync`:

  - **Feliz:** ID existente, `Type` válido → `204`, caso de uso recibe `Id` de ruta.
  - **Límite:** `Id` ruta ≠ `Id` cuerpo → caso de uso recibe `Id` de ruta (CE-1).
  - **Error:** ID inexistente → `404`; `Type` inválido → `400`.
- `UpdateTypeVisibilityAsync`:

  - **Feliz:** ID existente, `Visibility = DISABLED` → `204`, caso de uso recibe solo `Id` y `Visibility`.
  - **Límite:** DTO con propiedades extra (`Type`) → caso de uso solo ve `Id` y `Visibility` (CE-2).
  - **Error:** ID inexistente → `404`; `Visibility` inválida → `400` (`ApplicationException`).
- `GetTypesForSelectsAsync`:

  - **Feliz:** Tipos habilitados → `200` con colección solo `ENABLED`.
  - **Límite:** Tipos con `DISABLED` → no aparecen en respuesta (CE-6c).
  - **Error:** Sin tipos habilitados → `200` con `[]`.

### 6.3 Test de metadatos (`TypeEndpointsMetadataTests`)

Equivalente a `UserEndpointsMetadataTests`:

- Recorre `EndpointDataSource` de `MapTypeEndpoints()`.
- Verifica 6 rutas con métodos HTTP correctos.
- Verifica `WithName` exactos (`InsertType`, `GetType`, `GetTypes`, `UpdateType`, `UpdateTypeVisibility`, `GetTypesForSelects`).
- Verifica `Produces` exactos de la tabla 4.3.
- Verifica política: 3 endpoints con `[Authorize]` (solo autenticación), 3 endpoints con `AdminAuthorization.PolicyName`.
- Verifica que `login` de Usuario sigue sin autorización (regresión).
- Verifica parámetros `{id}` como `int` requerido.
- Verifica que no hay `ProducesProblem`.

### 6.4 Doubles de prueba

`test/Fakes/` con implementaciones de:

- `IRepository<TypeEntity, TypeDTO>` + `ISelectRepository<TypeDTO>` → `FakeTypeRepository`
- `IMapper<TypeDTO, TypeEntity>` → `FakeTypeMapper`
- `ICommonService<TypeDTO>` → `FakeTypeCommonService`
- `ISelectService<TypeDTO>` → `FakeTypeSelectService`

Se prefieren a `InMemory` de EF Core porque `UpdateAsyncInfo` y `UpdateAsyncVisibility` usan `ExecuteUpdateAsync` (no soportado en memoria). Los fakes no tocan BD real.

Cada test crea sus propios datos (criterio 15 de finalización), sin sembrado compartido.

---

## 7. Decisiones con su alternativa descartada

| Decisión                                                                                                                     | Alternativa descartada                                                 | Por qué                                                                                                                                                                                |
| ----------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Autorización en dos grupos** (autenticado vs admin) con `RequireAuthorization` y `AdminAuthorization.PolicyName` | Un solo grupo`AdminOnly` para los 6, o un filtro manual por endpoint | La spec exige distinción: 4 endpoints no requieren`admin` (RF-1.6). Dos grupos reutilizan el middleware nativo y la política existente.                                             |
| **Reutilizar `AdminAuthorization`**                                                                                   | Duplicar`RequireClaim("Role", "admin")` en cada handler              | Ya existe, probado en spec 001 (`AdminAuthorizationTests`), evita divergencias.                                                                                                       |
| **`SelectService` para `GetTypesForSelects`**                                                                       | Usar`CommonService.GetAsyncAllInfo()` y filtrar en handler           | `SelectService` ya existe, delega a `ISelectRepository.GetAsyncInfoForSelects()` que ya filtra `ENABLED` en BD. Separación de responsabilidades (RNF-2).                         |
| **DTO nuevo en `UpdateTypeVisibility`** (solo `Id` + `Visibility`)                                                | Mutar el DTO recibido poniendo a null el resto                         | Evita que propiedades no deseadas lleguen al caso de uso; intención explícita y testeable (CE-2).                                                                                     |
| **`AddTypeModule()` separado**                                                                                        | Añadir registros de Tipo dentro de`AddUserModule()`                 | Separación de módulos: cada caso de uso tiene su método de registro. Facilita tests y evita acoplar`UserModule` con `TypeModule`. Si mañana se quita Usuario, Tipo no se rompe. |

---

## 8. Mapa RF → implementación → test

| RF                             | Implementación principal                                                                                                         | Test que lo demuestra                                                                                                                              |
| ------------------------------ | --------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------- |
| RF-1.1, RF-1.2                 | `TypeEndpointsExtensions`: grupo autenticado + grupo admin; `UseAuthentication/UseAuthorization` en `Program.cs`            | `TypeEndpointsMetadataTests` (política declarada), `TypeHandlers*Tests` (fakes no ejecutan caso de uso si falla auth — limitación conocida) |
| RF-1.3, RF-1.4, RF-1.5         | `AdminAuthorization.PolicyName` en grupo admin                                                                                  | `TypeEndpointsMetadataTests` (política presente), `AdminAuthorizationTests` (ya existe, cubre rol)                                            |
| RF-1.6                         | Grupo autenticado**sin** política `AdminOnly`                                                                            | `TypeEndpointsMetadataTests` (ausencia de `AdminOnly` en 4 endpoints)                                                                          |
| RF-1.7                         | Todos los 6 endpoints dentro de grupos que exigen autorización                                                                   | `TypeEndpointsMetadataTests` (todos declaran `401`)                                                                                            |
| RF-2.1, RF-2.4                 | `TypeHandlers.InsertTypeAsync` → `ICommonService<TypeDTO>.AddAsyncInfo`                                                      | `TypeHandlersInsertTypeTests` (feliz)                                                                                                            |
| RF-2.2                         | `TypeRepository.AddAsyncInfo` fija `Visibility = "ENABLED"`                                                                   | `TypeHandlersInsertTypeTests` (fake registra DTO que llega al caso de uso)                                                                       |
| RF-2.3, RF-2.6, RF-8.2, RF-8.5 | `TypeEntity` valida longitud → `EntityException`→`400`; índice `uq_tipo` → `DbUpdateException`→`500`             | `TypeHandlersInsertTypeTests` (límite: duplicado→`500`; error: longitud→`400`)                                                            |
| RF-2.5                         | `Results.StatusCode(201)` sin cuerpo ni `Location`                                                                            | `TypeHandlersInsertTypeTests` (feliz: cuerpo vacío, sin `Location`)                                                                           |
| RF-3.1, RF-3.2, RF-3.5         | `TypeHandlers.GetTypeAsync`: `dto.Id = id` → `GetAsyncInfo`                                                                | `TypeHandlersGetTypeTests` (feliz, límite: `DISABLED`→`200`, CE-1)                                                                         |
| RF-3.3, RF-8.1                 | `KeyNotFoundException` del repo → `404`                                                                                      | `TypeHandlersGetTypeTests` (error)                                                                                                               |
| RF-3.4                         | Binding`int id` → `400` automático                                                                                          | `TypeEndpointsMetadataTests` (parámetro `int` requerido)                                                                                      |
| RF-4.1, RF-4.2, RF-4.3, RF-4.4 | `TypeHandlers.GetTypesAsync` → `GetAsyncAllInfo()`                                                                           | `TypeHandlersGetTypesTests` (feliz, límite: vacío, CE-6b, usuario no admin → 403)                                                              |
| RF-5.1, RF-5.2, RF-5.5         | `TypeHandlers.UpdateTypeAsync`: `dto.Id = id` → `UpdateAsyncInfo`                                                          | `TypeHandlersUpdateTypeTests` (feliz, CE-1)                                                                                                      |
| RF-5.3, RF-8.1                 | `KeyNotFoundException` → `404`                                                                                               | `TypeHandlersUpdateTypeTests` (error)                                                                                                            |
| RF-5.4, RF-8.2                 | `EntityException` (longitud `Type`) → `400`                                                                                | `TypeHandlersUpdateTypeTests` (error)                                                                                                            |
| RF-5.6                         | Binding`int id` → `400`                                                                                                      | `TypeEndpointsMetadataTests`                                                                                                                     |
| RF-6.1, RF-6.2, RF-6.5         | `TypeHandlers.UpdateTypeVisibilityAsync`: DTO nuevo `{Id=id, Visibility=dto.Visibility}` → `UpdateAsyncVisibility`         | `TypeHandlersUpdateTypeVisibilityTests` (feliz, CE-1, CE-2)                                                                                      |
| RF-6.3, RF-6.6, RF-8.3         | `CommonService.UpdateAsyncVisibility` valida `Id>0` y `Visibility∈{ENABLED,DISABLED}` → `ApplicationException`→`400` | `TypeHandlersUpdateTypeVisibilityTests` (error: `Visibility` inválida, `Id` ≤ 0)                                                           |
| RF-6.4, RF-8.1                 | `KeyNotFoundException` → `404`                                                                                               | `TypeHandlersUpdateTypeVisibilityTests` (error)                                                                                                  |
| RF-7.1, RF-7.2, RF-7.3         | `TypeHandlers.GetTypesForSelectsAsync` → `ISelectService<TypeDTO>.GetAsyncInfoForSelects()`                                  | `TypeHandlersGetTypesForSelectsTests` (feliz, límite: `DISABLED` ausente, error: vacío)                                                      |
| RF-8.4, RF-8.6                 | `ExceptionHandlerExtensions` centralizado                                                                                       | `ExceptionTranslationTests` (ya existe, cubre todas las excepciones)                                                                             |
| RF-9.1, RF-9.2, RF-9.3         | `Produces` + `WithName` en cada `Map*`                                                                                      | `TypeEndpointsMetadataTests` (verifica metadata completa)                                                                                        |
| RNF-1, RNF-2                   | Inyección de`ICommonService`, `ISelectService`, `IRepository`, `ISelectRepository`, `IMapper`                          | `ServiceCollectionExtensions` registra todo por abstracción                                                                                     |
| RNF-3                          | Todos los registros`AddScoped`                                                                                                  | `ServiceCollectionExtensions` usa `AddScoped`                                                                                                  |
| RNF-6                          | Identificadores/comentarios en inglés; mensajes en español (excepciones ya en español)                                         | Código en inglés,`ExceptionHandlerExtensions` usa `exception.Message`                                                                        |
| RNF-10                         | Sin nuevos paquetes                                                                                                               | `dotnet list package` verifica                                                                                                                   |

---

## 9. Verificación final

```bash
dotnet build
dotnet test
```

Ambos deben terminar en verde (`0` errores, `0` fallos). `dotnet list package` debe mostrar solo los paquetes de la constitución + `Microsoft.AspNetCore.Authentication.JwtBearer` (ya verificado en spec 001).
