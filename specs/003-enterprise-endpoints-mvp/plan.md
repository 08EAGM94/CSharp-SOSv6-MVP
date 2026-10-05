# Plan de Implementación — Caso de Uso Empresa (Spec 003)

## Arquitectura de la solución

| Archivo (nuevo o modificado) | Capa | Responsabilidad | RF cubiertos |
|-------------------------------|------|-----------------|--------------|
| `sosMVP/Handlers/EnterpriseHandlers.cs` | API / Handlers | 6 handlers estáticos para los endpoints (Insert, Get, GetAll, Update, UpdateVisibility, GetForSelects) **y el record envoltorio `InsertEnterpriseRequest` del cuerpo de `POST /enterprise/`** | RF-2, RF-3, RF-4, RF-5, RF-6, RF-7 |
| `sosMVP/Extensions/EnterpriseEndpointsExtensions.cs` | API / Config | Registro de los 6 endpoints con `MapGroup`, `RequireAuthorization`, `Produces`, `WithName` | RF-1, RF-9 |
| `sosMVP/Extensions/ServiceCollectionExtensions.cs` (modif.) | API / DI | Añade `AddEnterpriseModule()` con registros scoped | RNF-3 |
| `sosMVP/Program.cs` (modif.) | API / Bootstrap | Invoca `AddEnterpriseModule()` y `MapEnterpriseEndpoints()` | — |
| `hexArch/application/useCases/CommonService.cs` (existente) | Application | Caso de uso genérico `ICommonService<EnterpriseDTO>` (ya implementado) | RF-2, RF-3, RF-4, RF-5, RF-6 |
| `hexArch/application/useCases/SelectService.cs` (existente) | Application | Caso de uso `ISelectService<EnterpriseDTO>` (ya implementado) | RF-7 |
| `hexArch/data/mappers/dtoToEntity/EnterpriseDTOtoEntityMapper.cs` (existente) | Infra / Mapper | Mapea `EnterpriseDTO → EnterpriseEntity` (ya implementado) | RF-2.4, RF-5.4 |
| `hexArch/data/mappers/dtoToEntity/ContactDTOtoEntityMapper.cs` (existente) | Infra / Mapper | Mapea `ContactDTO → ContactEntity` (ya implementado) | RF-2.5 |
| `hexArch/repository/EnterpriseRepository.cs` (existente, **INTOCABLE**) | Infra / Repository | Implementa `IRepository<EnterpriseEntity,EnterpriseDTO>` e `ISelectRepository<EnterpriseDTO>` (ya implementado, Constitución p5) | RF-2, RF-3, RF-4, RF-5, RF-6, RF-7 |
| `test/Fakes/FakeEnterpriseCommonService.cs` (nuevo) | Test / Fake | Fake de `ICommonService<EnterpriseDTO>` para tests de handlers | Todos |
| `test/Fakes/FakeEnterpriseSelectService.cs` (nuevo) | Test / Fake | Fake de `ISelectService<EnterpriseDTO>` para tests de handlers | RF-7 |
| `test/EnterpriseHandlersInsertEnterpriseTests.cs` (nuevo) | Test | Tests de `InsertEnterprise` (happy path, límite, error) | RF-2, CE-12, CE-17, CE-18 |
| `test/EnterpriseHandlersGetEnterpriseTests.cs` (nuevo) | Test | Tests de `GetEnterprise` | RF-3, CE-3, CE-10 |
| `test/EnterpriseHandlersGetEnterprisesTests.cs` (nuevo) | Test | Tests de `GetEnterprises` | RF-4, CE-6, CE-6b |
| `test/EnterpriseHandlersUpdateEnterpriseTests.cs` (nuevo) | Test | Tests de `UpdateEnterprise` | RF-5, CE-3, CE-5, CE-11 |
| `test/EnterpriseHandlersUpdateEnterpriseVisibilityTests.cs` (nuevo) | Test | Tests de `UpdateEnterpriseVisibility` | RF-6, CE-3, CE-13 |
| `test/EnterpriseHandlersGetEnterprisesForSelectsTests.cs` (nuevo) | Test | Tests de `GetEnterprisesForSelects` | RF-7, CE-6c |
| `test/EnterpriseEndpointsMetadataTests.cs` (nuevo) | Test | Verifica `WithName` y `Produces` de los 6 endpoints | RF-9 |
| `test/EnterpriseAuthorizationTests.cs` (nuevo) | Test | Tests de 401/403 con tokens (expirado, rol no admin, manipulado) | RF-1, CE-7, CE-8, CE-9, CE-15 |

**Archivos ya existentes que se REUTILIZAN (no se modifican):**
- `EnterpriseRepository.cs` — Constitución principio 5: repositorio intocable.
- `CommonService<T>`, `SelectService<T>` — Casos de uso genéricos ya implementados.
- `EnterpriseDTO`, `ContactDTO` — DTOs ya definidos.
- `EnterpriseEntity`, `ContactEntity` — Entidades de dominio con validaciones (longitudes, etc.).
- Mappers `EnterpriseDTOtoEntityMapper`, `ContactDTOtoEntityMapper`.
- `ApiExceptionHandler` — Manejo centralizado de excepciones → códigos HTTP (ya cubre `KeyNotFoundException→404`, `EntityException→400`, `ApplicationException→400`, `DbUpdateException→500`).
- `AdminAuthorization` — Política `AdminOnly` y helpers de claims.
- `JwtTokenFactory` — Emisión/validación de tokens JWT.
- Infraestructura de tests: `HandlerTestSupport`, `TestHttp`, `JwtTestTokens`, `CreateSession`.

---

## Piezas nuevas vs. reutilizadas

**Reutilizadas (casi todo el "cómo" ya existe):**
- Entidades de dominio (`EnterpriseEntity`, `ContactEntity`) con reglas de longitud → validan RF-2.4, RF-2.5, RF-5.4.
- DTOs (`EnterpriseDTO`, `ContactDTO`).
- Puertos `ICommonService<T>`, `ISelectService<T>` e implementaciones `CommonService<T>`, `SelectService<T>`.
- Repositorio `EnterpriseRepository` — **INTOCABLE** (Constitución p5). Ya implementa:
  - `AddAsyncInfo(EnterpriseEntity, ContactEntity?)` — transacción atómica empresa+contacto, fija `Visibility=ENABLED`.
  - `GetAsyncInfo(EnterpriseEntity)` — devuelve DTO completo, **no filtra por visibilidad**, lanza `KeyNotFoundException` si no existe.
  - `GetAsyncAllInfo()` — devuelve `Id, CommercialName, TradeName` de **todas** las empresas (incluye `DISABLED`), **no incluye `Visibility`**.
  - `UpdateAsyncInfo(EnterpriseEntity)` — actualiza todos los campos **excepto `Visibility`**, lanza `KeyNotFoundException` si no existe.
  - `UpdateAsyncVisibility(EnterpriseDTO)` — actualiza solo `Visibility`, valida `ENABLED/DISABLED`, lanza `KeyNotFoundException` si no existe.
  - `GetAsyncInfoForSelects()` — devuelve `Id, CommercialName, TradeName` solo de `Visibility=ENABLED`, **no incluye `Visibility`**.
- Mappers DTO→Entity.
- Manejo global de errores (`ApiExceptionHandler`) — ya traduce excepciones a códigos HTTP y mensajes en español.
- Autorización: política `AdminOnly` + `RequireAuthorization()` para autenticados.
- Patrón de endpoints: grupo `authenticatedEndpoints` (requiere sesión) + grupo `adminEndpoints` (requiere `AdminOnly`). **`GetEnterprises` ahora usa `adminEndpoints` (3 endpoints admin, 3 autenticados).**
- Patrón de tests: fakes de `ICommonService<T>` e `ISelectService<T>`, `HandlerTestSupport`, `TestHttp`.

**Nuevas (lo que hay que crear de verdad):**
1. `EnterpriseHandlers.cs` — 6 handlers estáticos (patrón idéntico a `TypeHandlers.cs`) **y el record `InsertEnterpriseRequest`**.
2. `EnterpriseEndpointsExtensions.cs` — registro de 6 endpoints (patrón idéntico a `TypeEndpointsExtensions.cs`).
3. `AddEnterpriseModule()` en `ServiceCollectionExtensions.cs` — registra repo, mappers, `CommonService<EnterpriseEntity,EnterpriseDTO>`, `SelectService<EnterpriseDTO>`.
4. 6 archivos de tests de handlers (happy path + caso límite + caso error cada uno).
5. 1 test de metadata de endpoints (`WithName`/`Produces`).
6. 1 test de autorización (401/403).

---

## Interfaz de los endpoints

| Endpoint | Método | Ruta | WithName | Produces (códigos exactos) | Handler | Caso de uso inyectado |
|----------|--------|------|----------|----------------------------|---------|----------------------|
| InsertEnterprise | POST | `/enterprise/` | `InsertEnterprise` | `201, 400, 401, 500` | `EnterpriseHandlers.InsertEnterpriseAsync` | `ICommonService<EnterpriseDTO>` |
| GetEnterprise | GET | `/enterprise/{id}` | `GetEnterprise` | `200, 400, 401, 404` | `EnterpriseHandlers.GetEnterpriseAsync` | `ICommonService<EnterpriseDTO>` |
| GetEnterprises | GET | `/enterprises/` | `GetEnterprises` | `200, 401, 403` | `EnterpriseHandlers.GetEnterprisesAsync` | `ICommonService<EnterpriseDTO>` |
| UpdateEnterprise | PUT | `/enterprise/{id}` | `UpdateEnterprise` | `204, 400, 401, 403, 404` | `EnterpriseHandlers.UpdateEnterpriseAsync` | `ICommonService<EnterpriseDTO>` |
| UpdateEnterpriseVisibility | PUT | `/enterprisev/{id}` | `UpdateEnterpriseVisibility` | `204, 400, 401, 403, 404` | `EnterpriseHandlers.UpdateEnterpriseVisibilityAsync` | `ICommonService<EnterpriseDTO>` |
| GetEnterprisesForSelects | GET | `/enterprisesct/` | `GetEnterprisesForSelects` | `200, 401` | `EnterpriseHandlers.GetEnterprisesForSelectsAsync` | `ISelectService<EnterpriseDTO>` |

**Regla común:** el `id` de ruta **siempre prevalece** sobre el `Id` del cuerpo (RF-5.1, RF-6.1, CE-1).

**Nota sobre `InsertEnterprise`:** el cuerpo de la petición es **un único objeto** (`InsertEnterpriseRequest`) que contiene la empresa (`EnterpriseDTO`) y, opcionalmente, el contacto (`ContactDTO`). El handler recibe ese único parámetro.

---

## Algoritmos en pseudocódigo

### Orden de comprobaciones (authn → authz → validación ruta → caso de uso)
```
Para TODOS los endpoints:
  1. Middleware JWT valida token → si inválido/expirado → 401 (RF-1.2, RF-1.7)
  2. Para GetEnterprises, UpdateEnterprise y UpdateEnterpriseVisibility:
       Política AdminOnly evalúa claim Role == "admin" → si no → 403 (RF-1.3, RF-1.4, RF-1.5)
  3. Para endpoints con {id}:
       Model binding intenta parsear int → si falla → 400 (RF-3.4, RF-5.6, RF-6.6, CE-14)
  4. Se invoca el handler correspondiente.
```

### InsertEnterprise (POST `/enterprise/`)
```
INPUT: InsertEnterpriseRequest request
  // request.Enterprise: EnterpriseDTO (obligatorio)
  // request.Contact: ContactDTO? (opcional)
1. Si request.Contact ≠ null Y (request.Contact.FullName es null O whitespace):
       request.Contact = null  // RF-2.9, CE-18: se ignora el contacto
2. El caso de uso (CommonService.AddAsyncInfo) fija Visibility = "ENABLED" en empresa y contacto (RF-2.3).
3. CommonService mapea request.Enterprise → EnterpriseEntity (valida longitudes en setters → EntityException → 400).
4. Si request.Contact ≠ null: mapea request.Contact → ContactEntity (valida FullName 10-150 → EntityException → 400).
5. Repository.AddAsyncInfo(enterpriseEntity, contactEntity?) en transacción:
      - Inserta Empresa con Visibilidad = "ENABLED".
      - Si contactEntity ≠ null: inserta Contacto con EmpresaId y Visibilidad = "ENABLED".
      - Si falla en cualquier punto: rollback completo (RF-2.8, CE-17, RNF-11).
6. Responde 201 sin cuerpo ni Location (RF-2.6, RF-2.7).
```

### GetEnterprise (GET `/enterprise/{id}`)
```
INPUT: int id (de ruta)
1. Crea EnterpriseDTO { Id = id }.
2. CommonService.GetAsyncInfo(dto) → Repository.GetAsyncInfo(EnterpriseEntity):
      - Proyecta TODOS los campos del DTO (incluye Visibility).
      - NO filtra por Visibility (devuelve aunque sea DISABLED) (RF-3.5, CE-10).
      - Si no existe → KeyNotFoundException → 400 (RF-3.3).
3. Responde 200 con EnterpriseDTO completo.
```

### GetEnterprises (GET `/enterprises/`)
```
1. Política AdminOnly evalúa claim Role == "admin" → si no → 403 (RF-1.3, RF-1.4, RF-4.5).
2. CommonService.GetAsyncAllInfo() → Repository.GetAsyncAllInfo():
      - Proyecta SOLO Id, CommercialName, TradeName.
      - NO filtra por Visibility (incluye DISABLED) (RF-4.1, RF-4.2, CE-6b).
      - NO incluye Visibility en la respuesta (RF-4.4).
3. Responde 200 con IEnumerable<EnterpriseDTO> (propiedades no incluidas llegan null).
```

### UpdateEnterprise (PUT `/enterprise/{id}`)
```
INPUT: int id (ruta), EnterpriseDTO dto (cuerpo)
1. dto.Id = id  // ruta prevalece (RF-5.1, CE-1).
2. CommonService.UpdateAsyncInfo(dto) → Repository.UpdateAsyncInfo(EnterpriseEntity):
      - Actualiza todos los campos EXCEPTO Visibility (RF-5.2).
      - Valida longitudes en setters de EnterpriseEntity → EntityException → 400 (RF-5.4).
      - Si no existe → KeyNotFoundException → 404 (RF-5.3).
3. Responde 204 sin cuerpo (RF-5.5).
```

### UpdateEnterpriseVisibility (PUT `/enterprisev/{id}`)
```
INPUT: int id (ruta), EnterpriseDTO dto (cuerpo)
1. Crea visibilityDto = new EnterpriseDTO { Id = id, Visibility = dto.Visibility }.
   // SOLO Id y Visibility; el resto se ignora (RF-6.2, CE-2).
2. CommonService.UpdateAsyncVisibility(visibilityDto):
      - Valida Id > 0 y Visibility ∈ {ENABLED, DISABLED} → ApplicationException → 400 (RF-6.3, RF-8.3).
      - Repository.UpdateAsyncVisibility → si no existe → KeyNotFoundException → 404 (RF-6.4).
3. Responde 204 sin cuerpo (RF-6.5).
```

### GetEnterprisesForSelects (GET `/enterprisesct/`)
```
1. SelectService.GetAsyncInfoForSelects() → Repository.GetAsyncInfoForSelects():
      - Filtra WHERE Visibilidad = "ENABLED" (RF-7.1).
      - Proyecta SOLO Id, CommercialName, TradeName (NO incluye Visibility) (RF-7.4).
      - DISABLED ya excluidos en BD (RF-7.2, CE-6c).
2. Responde 200 con IEnumerable<EnterpriseDTO> (propiedades no incluidas llegan null).
```

---

## Manejo de errores

**Reutiliza `ApiExceptionHandler` existente (registrado en `Program.cs`):**

| Excepción | Código HTTP | Origen | RF |
|-----------|-------------|--------|----|
| `KeyNotFoundException` | 404 | Repositorio (GetAsyncInfo, UpdateAsyncInfo, UpdateAsyncVisibility) | RF-8.1 |
| `EntityException` | 400 | Entidades de dominio (setters validan longitudes) | RF-8.2 |
| `ApplicationException` | 400 | `CommonService.UpdateAsyncVisibility` valida Id>0 y Visibility ∈ {ENABLED,DISABLED} | RF-8.3 |
| `DbUpdateException` (SQL 2601/2627) | 500 | Conflicto índice único en persistencia | RF-8.5, RF-2.8 |
| Cualquier otra | 400 | Fallback seguro | RF-8.4 |
| Mensajes | Español | `exception.Message` escrito tal cual | RF-8.6, CE-16 |

**No se añade ninguna vía de errores nueva.** Los handlers no capturan excepciones; dejan que burbujeen al middleware global.

---

## Inyección de dependencias

En `ServiceCollectionExtensions.cs` se añade:

```csharp
public static IServiceCollection AddEnterpriseModule(this IServiceCollection services)
{
    // Repository (implementa IRepository<EnterpriseEntity,EnterpriseDTO> e ISelectRepository<EnterpriseDTO>)
    services.AddScoped<EnterpriseRepository>();
    services.AddScoped<IRepository<EnterpriseEntity, EnterpriseDTO>>(
        sp => sp.GetRequiredService<EnterpriseRepository>());
    services.AddScoped<ISelectRepository<EnterpriseDTO>>(
        sp => sp.GetRequiredService<EnterpriseRepository>());

    // Mappers DTO → Entity
    services.AddScoped<IMapper<EnterpriseDTO, EnterpriseEntity>, EnterpriseDTOtoEntityMapper>();
    // ContactDTO → ContactEntity YA registrado en AddUserModule (se reutiliza)

    // Casos de uso
    services.AddScoped<CommonService<EnterpriseEntity, EnterpriseDTO>>();
    services.AddScoped<ICommonService<EnterpriseDTO>>(
        sp => sp.GetRequiredService<CommonService<EnterpriseEntity, EnterpriseDTO>>());

    services.AddScoped<SelectService<EnterpriseDTO>>();
    services.AddScoped<ISelectService<EnterpriseDTO>>(
        sp => sp.GetRequiredService<SelectService<EnterpriseDTO>>());

    return services;
}
```

- Vida **scoped** (Constitución RNF-3).
- `ContactDTOtoEntityMapper` ya registrado en `AddUserModule` → se reutiliza.
- `EnterpriseRepository` registrado una sola vez y expuesto como dos interfaces.

En `Program.cs`:
```csharp
builder.Services.AddEnterpriseModule();
// ...
app.MapEnterpriseEndpoints();
```

---

## Estrategia de tests

**Principio:** Test First — cada tarea escribe su test en rojo antes del código.

**Patrón:** Idéntico a specs 001 y 002.
- Un test por endpoint (happy path) + caso límite + caso de error.
- Cada test crea sus propios datos (fakes in-memory, sin BD real).
- Fakes de `ICommonService<EnterpriseDTO>` e `ISelectService<EnterpriseDTO>` (patrón `FakeTypeCommonService`, `FakeTypeSelectService`).
- Verificación de códigos HTTP vía `TestHttp.ExecuteAsync` y `HandlerTestSupport.AssertTranslationAsync`.
- Verificación de mensajes en español (`AssertBodyIsInSpanishAsync`).
- Verificación de 201 sin cuerpo ni Location (`AssertCreatedWithoutLocationAsync`).
- Verificación de 204 sin cuerpo (`AssertNoContentAsync`).
- Atomicidad (CE-17): fake lanza excepción en `AddAsyncInfo` → se verifica que el fallo se propaga y se traduce a `500` y que `AddAsyncInfo` se llama una sola vez (sin reintento). **El test NO verifica el rollback**; el rollback es una garantía de la capa de persistencia (repositorio intocable, principio 5) y la estrategia de tests del proyecto es con fakes, no contra SQL Server real.

### Tests de autorización (401/403) — Mecanismo sin host HTTP

La @implementer resolvió que los tests de 401/403 de `EnterpriseAuthorizationTests` **no necesitan host HTTP ni paquetes nuevos**. El mecanismo es:

1. Se usa el `IPolicyEvaluator` de producción sobre la política que cada ruta declara (combinado con `AuthorizationPolicy.CombineAsync` y el `IAuthorizationPolicyProvider` real).
2. El evaluador distingue tres resultados: `Challenged` (→ 401), `Forbidden` (→ 403), `Succeeded` (→ continúa).
3. Tokens reales se generan con `JwtTokenFactory` para:
   - Expiración de 30 min (CE-15): token emitido con `exp` en el pasado.
   - Sesión manipulada (CE-9): token firmado con clave distinta o claims alterados.
4. Se invoca `policyEvaluator.AuthenticateAsync` y `policyEvaluator.AuthorizeAsync` directamente en el test, pasando el `HttpContext` construido a mano con el token en el header `Authorization`.
5. Esto evita levantar `WebApplicationFactory` / `TestServer` y mantiene los tests rápidos, deterministas y sin dependencias externas.

### Lista explícita de tests

| Archivo de test | Tests (tentativos) | RF/CE cubiertos |
|-----------------|-------------------|-----------------|
| `EnterpriseHandlersInsertEnterpriseTests.cs` | 1. `ValidEnterprise_IsAnsweredWithCreatedWithoutBodyAndWithoutLocation`<br>2. `ValidEnterprise_IsDelegatedOnceToUseCaseWithContactWhenFullNameProvided`<br>3. `ValidEnterprise_IsDelegatedOnceToUseCaseWithoutContactWhenFullNameMissing`<br>4. `ContactDtoWithEmptyFullName_IsIgnored_OnlyEnterpriseCreated`<br>5. `VisibilitySentInBody_IsNotValidatedNorRewrittenByHandler`<br>6. `DuplicateEnterpriseOnUniqueIndex_IsTranslatedToInternalServerError`<br>7. `EnterpriseOutsideAllowedLengths_IsTranslatedToBadRequest`<br>8. `ContactFullNameOutsideAllowedLength_IsTranslatedToBadRequest`<br>9. `EntityRuleViolation_DeliversMessageInSpanish` | RF-2.1–2.9, RF-8.2, RF-8.5, CE-4, CE-11, CE-12, CE-17, CE-18 |
| `EnterpriseHandlersGetEnterpriseTests.cs` | 1. `ExistingEnterprise_IsAnsweredWithOkAndFullDto`<br>2. `ExistingEnterpriseWithDisabledVisibility_IsAnsweredWithOkNot404`<br>3. `RouteIdPrevailsOverBodyId`<br>4. `NonExistentEnterprise_IsTranslatedToNotFound`<br>5. `NonNumericRouteId_IsTranslatedToBadRequest`<br>6. `BusinessFailure_IsTranslatedToBadRequest`<br>7. `BusinessFailure_DeliversMessageInSpanish` | RF-3.1–3.5, RF-8.1, RF-8.4, CE-3, CE-10, CE-14 |
| `EnterpriseHandlersGetEnterprisesTests.cs` | 1. `Enterprises_AreAnsweredWithOkAndCollectionFromUseCase`<br>2. `Listing_IncludesDisabledEnterprises`<br>3. `Listing_DoesNotIncludeVisibilityInResponse`<br>4. `EmptyStore_IsAnsweredWithOkAndEmptyCollection`<br>5. `BusinessFailure_IsTranslatedToBadRequest`<br>6. `BusinessFailure_DeliversMessageInSpanish`<br>7. `NonAdminUser_IsTranslatedToForbidden` | RF-4.1–4.5, RF-8.4, CE-6, CE-6b |
| `EnterpriseHandlersUpdateEnterpriseTests.cs` | 1. `ValidUpdate_IsAnsweredWithNoContent`<br>2. `RouteIdPrevailsOverBodyId`<br>3. `NonExistentEnterprise_IsTranslatedToNotFound`<br>4. `NonNumericRouteId_IsTranslatedToBadRequest`<br>5. `EnterpriseOutsideAllowedLengths_IsTranslatedToBadRequest`<br>6. `BusinessFailure_IsTranslatedToBadRequest`<br>7. `BusinessFailure_DeliversMessageInSpanish` | RF-5.1–5.6, RF-8.1, RF-8.2, CE-1, CE-3, CE-5, CE-11, CE-14 |
| `EnterpriseHandlersUpdateEnterpriseVisibilityTests.cs` | 1. `ValidVisibilityUpdate_IsAnsweredWithNoContent`<br>2. `RouteIdPrevailsOverBodyId`<br>3. `OtherPropertiesInBody_AreIgnored`<br>4. `InvalidVisibilityValue_IsTranslatedToBadRequest`<br>5. `NonExistentEnterprise_IsTranslatedToNotFound`<br>6. `NonNumericRouteId_IsTranslatedToBadRequest`<br>7. `BusinessFailure_IsTranslatedToBadRequest`<br>8. `BusinessFailure_DeliversMessageInSpanish` | RF-6.1–6.6, RF-8.1, RF-8.3, CE-1, CE-2, CE-3, CE-13, CE-14 |
| `EnterpriseHandlersGetEnterprisesForSelectsTests.cs` | 1. `EnabledEnterprises_AreAnsweredWithOkAndCollectionFromSelectService`<br>2. `Listing_AsksSelectServiceOnce_LeavesCommonServiceUntouched`<br>3. `EnabledCollection_ReachesResponseIntactWithoutFilteringNorAdding`<br>4. `VisibilityFilterLivesInSelectRepositoryNotInHandler`<br>5. `AllDisabledStore_ProducesEmptyCollection`<br>6. `NoRegisteredEnterprises_IsAnsweredWithOkAndEmptyCollection`<br>7. `BusinessFailure_IsTranslatedToBadRequest`<br>8. `GenericUseCaseFailure_IsTranslatedToBadRequest`<br>9. `BusinessFailure_DeliversMessageInSpanish` | RF-7.1–7.4, RF-8.3, RF-8.4, RF-8.6, CE-6, CE-6c |
| `EnterpriseEndpointsMetadataTests.cs` | 1. `InsertEnterprise_HasCorrectWithNameAndProduces`<br>2. `GetEnterprise_HasCorrectWithNameAndProduces`<br>3. `GetEnterprises_HasCorrectWithNameAndProduces`<br>4. `UpdateEnterprise_HasCorrectWithNameAndProduces`<br>5. `UpdateEnterpriseVisibility_HasCorrectWithNameAndProduces`<br>6. `GetEnterprisesForSelects_HasCorrectWithNameAndProduces` | RF-9.1, RF-9.2, RF-9.3 |
| `EnterpriseAuthorizationTests.cs` | 1. `AllEndpoints_RequireValidSession_401WhenMissing`<br>2. `AllEndpoints_RequireValidSession_401WhenExpired`<br>3. `UpdateAndListEndpoints_RequireAdminRole_403WhenUser`<br>4. `UpdateAndListEndpoints_RequireAdminRole_403WhenCorruptedSession`<br>5. `ReadEndpoints_AcceptAnyAuthenticatedRole_Not403` | RF-1.1–1.7, CE-7, CE-8, CE-9, CE-15 |

**Comando de verificación:** `dotnet build` y `dotnet test` (deben pasar en verde).

---

## Decisiones justificadas

| Decisión | Por qué | Alternativa descartada |
|----------|---------|------------------------|
| Reutilizar patrón de specs 001/002 (handlers estáticos, grupos de autorización, fakes) | Consistencia arquitectónica; reduce superficie de bugs; Constitución RNF-1 (esquema por capas). | Inventar nuevo patrón (p. ej. controladores, Minimal APIs con lambdas inline) — rompería coherencia y requeriría nuevos tests de infraestructura. |
| Listado completo (`GetEnterprises`) **no filtra** por visibilidad | Decisión de producto: el listado completo sirve para **gobernar** el catálogo (admin ve todo); existe endpoint dedicado `GetEnterprisesForSelects` para selects de UI (solo `ENABLED`). Coherente con specs 001 y 002. | Filtrar por `ENABLED` en ambos — impediría al admin ver/gestionar empresas deshabilitadas. |
| `UpdateEnterprise` **no toca `Visibility`** | Separación de responsabilidades: visibilidad es un estado de ciclo de vida, no un dato de ficha. Endpoint dedicado `UpdateEnterpriseVisibility` con política `AdminOnly`. | Permitir `Visibility` en `UpdateEnterprise` — acoplaría dos intenciones distintas y complicaría autorización (¿quién puede cambiar visibilidad?). |
| **No añadir paquetes NuGet** (RNF-10) | Solo `Microsoft.AspNetCore.Authentication.JwtBearer` y EF Core (ya en spec 001). El resto es BCL. | Añadir FluentValidation, MediatR, AutoMapper, etc. — violaría Constitución y espec. |
| `ContactDTO` sin `FullName` o en blanco → **se ignora** (RF-2.9, CE-18) | Decisión del usuario (P1 → Opción A): permite crear empresa sin contacto inicial obligatorio; el contacto es opcional de verdad. | Rechazar con 400 — obligaría a enviar un contacto válido siempre, rompiendo la opción A. |
| `Visibility` se fija a `ENABLED` en creación (repo) | Repositorio es la única fuente de verdad para el valor por defecto; handler no valida ni reescribe. | Validar/reescribir en handler — duplicaría lógica y acoplaría a valor hardcodeado. |
| `GetEnterprisesForSelects` usa `ISelectService` (no `ICommonService`) | Puerto separado garantiza que el filtro `ENABLED` vive en `EnterpriseRepository.GetAsyncInfoForSelects` (intocable). | Usar `GetAsyncAllInfo` y filtrar en handler — violaría Constitución p5 (repo intocable) y movería lógica de filtro a capa incorrecta. |
| CE-17 se verifica como **propagación de fallo → 500 + llamada única sin reintento** (no rollback) | La suite de tests es 100% con fakes (patrón specs 001/002); el único proveedor EF disponible es SqlServer; RNF-10 prohíbe añadir paquetes (InMemory/Sqlite) para test de integración. Un test contra SQL Server real rompería el patrón y haría depender `dotnet test` del entorno. | Test de integración contra SQL Server real — descartado por los motivos arriba. |
| **Cuerpo único `InsertEnterpriseRequest` para `POST /enterprise/`** (Opción A) | Un record `sealed` en el mismo archivo del handler (`EnterpriseHandlers.cs`) sigue el precedente de `TokenResponse` y `AdminConfirmation` en `UserHandlers.cs`. Minimal API solo admite **un** cuerpo complejo por endpoint; el record envoltorio hace el contrato explícito en OpenAPI y evita binding ambiguo. | **B**: meter `ContactDTO? Contact` dentro de `EnterpriseDTO` → contaminaría la salida de los GET (RF-4.4, RF-7.4).<br>**C**: registrar `ContactDTO` en DI para que se infiera como servicio → un DTO de petición no es un servicio.<br>**D**: delegado local que recibe `HttpContext` y deserializa a mano → oculta el contrato de OpenAPI y se aparta del estilo del proyecto. |

---

## Riesgos y puntos de atención

1. **Atomicidad de empresa+contacto NO cubierta por la suite de tests**: el fake no puede simular rollback de transacción real. Una regresión en esa transacción no sería detectada por `dotnet test`; quedaría cubierta por el criterio de finalización 16, que la atribuye a la capa de persistencia (repositorio intocable, principio 5 de la Constitución).
2. **ContactDTO sin FullName**: El handler debe poner `contact = null` **antes** de llamar al caso de uso. Si se pasa `ContactDTO` con `FullName = ""` al caso de uso, el mapper creará `ContactEntity` con `FullName = ""` → `EntityException` en setter → 400 incorrecto. → Test CE-18 verifica que NO se llama `AddAsyncInfo` con contact.
3. **Visibilidad en respuestas de listado**: `GetAsyncAllInfo` y `GetAsyncInfoForSelects` **no proyectan `Visibility`**. Los DTOs devueltos tendrán `Visibility = null`. El consumidor no debe asumir presencia (RF-4.4, RF-7.4). → Tests verifican que `Visibility` no está en JSON o es null.
4. **Autorización**: `GetEnterprises`, `UpdateEnterprise` y `UpdateEnterpriseVisibility` usan grupo `adminEndpoints` (política `AdminOnly`). Los otros 3 (`InsertEnterprise`, `GetEnterprise`, `GetEnterprisesForSelects`) usan grupo `authenticatedEndpoints` (solo `RequireAuthorization()`). → Tests de autorización deben usar `CreateSession(role: "user")` y `CreateSession(role: "admin")` y verificar 403 vs 200/204.
5. **Id de ruta vs cuerpo**: En `UpdateEnterprise` y `UpdateEnterpriseVisibility`, el handler **sobrescribe** `dto.Id = id` (ruta). En `GetEnterprise`, crea DTO nuevo con `Id = id`. → Tests CE-1 verifican que el Id del cuerpo se ignora.
6. **Mensajes en español**: `ApiExceptionHandler` escribe `exception.Message` tal cual. Las excepciones de dominio (`EntityException`, `ApplicationException`) ya lanzan mensajes en español. → Tests `*_DeliversMessageInSpanish` verifican cuerpo de respuesta.
7. **Minimal API solo admite un cuerpo por endpoint**: un handler con dos parámetros complejos (`EnterpriseDTO dto, ContactDTO? contact`) rompe el binding. El record envoltorio `InsertEnterpriseRequest` resuelve esto; si se eliminara o cambiara la firma, el binding fallaría silenciosamente en tiempo de ejecución (el segundo parámetro llega null).
8. **Arrancar la app no materializa la tabla de rutas**: "la app arranca sin errores" es una comprobación insuficiente. Cualquier enumeración de endpoints (`app.Services.GetRequiredService<EndpointDataSource>().Endpoints`) o petición real detecta fallos de binding/registro que el startup no revela. → T12 exige verificar que la tabla de rutas se materializa sin error.