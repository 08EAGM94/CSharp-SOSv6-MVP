# Plan — Caso de Uso: Contacto (005-contact-endpoints-mvp)

## 1. Archivos y responsabilidades

| Archivo | Responsabilidad | RF cubiertos |
|---------|----------------|--------------|
| `sosMVP/Extensions/ContactEndpointsExtensions.cs` | Registro de los 7 endpoints en dos grupos (autenticado y admin), con `Produces` y `WithName` exactos | RF-1, RF-9 |
| `sosMVP/Handlers/ContactHandlers.cs` | Handlers de los 7 endpoints: binding de parámetros de ruta, invocación de puertos primarios, mapeo a `IResult` | RF-2 a RF-7, RF-10 |
| `sosMVP/Extensions/ServiceCollectionExtensions.cs` | Nuevo `AddContactModule`: registro DI de `ContactRepository` (ambos puertos secundarios) y `EnterpriseChildrenService<ContactEntity, ContactDTO>` | RNF-2, RNF-3 |
| `sosMVP/Program.cs` | Composición: `builder.Services.AddContactModule();` y `app.MapContactEndpoints();` | RNF-3 |
| `hexArch/application/abstractions/primaryPorts/IEnterpriseChildrenService.cs` | Ya existe — puerto primario de los 6 endpoints CRUD/visibilidad | RF-2 a RF-7 |
| `hexArch/application/abstractions/primaryPorts/ISelectService.cs` | Ya existe — puerto primario de `GetContactsForSelect` | RF-10 |
| `hexArch/application/useCases/EnterpriseChildrenService.cs` | Ya existe — orquesta validaciones (`Visibility`, `Id`) y delega al repo | RF-2.2, RF-6.3, RF-8 |
| `hexArch/application/useCases/SelectService.cs` | Ya existe — delega en `ISelectRepository` | RF-10 |
| `hexArch/repository/ContactRepository.cs` | Ya existe — **intocable** (principio 5): `IByEnterpriseRepository<ContactEntity, ContactDTO>` + `ISelectRepository<ContactDTO>` | RF-3 a RF-7, RF-10 |
| `hexArch/data/mappers/dtoToEntity/ContactDTOtoEntityMapper.cs` | Ya existe — mapeo DTO → Entity (dispara `EntityException` por longitud de `FullName`/`Id`) | RF-2.3, RF-5.4, RF-8.2 |
| `hexArch/domain/entities/ContactEntity.cs` | Ya existe — reglas de negocio (`Id`/`EnterpriseId` ≥ 1, `FullName` 10–150) | RF-2.3, RF-3.4, RF-5.4, RF-8.2 |
| `sosMVP/Extensions/ExceptionHandlerExtensions.cs` | Ya existe — traducción `KeyNotFoundException`→404, `EntityException`/`ApplicationException`→400, `DbUpdateException`→500, mensajes en español | RF-8 |
| `test/Fakes/FakeContactChildrenService.cs` | Nuevo fake de `IEnterpriseChildrenService<ContactDTO>` con contadores, últimos DTOs y excepción a lanzar | RF-2 a RF-7 (tests) |
| `test/Fakes/FakeContactSelectService.cs` | Nuevo fake de `ISelectService<ContactDTO>` | RF-10 (tests) |
| `test/Support/ContactEndpointTestHost.cs` | Host de pruebas: pipeline real (JwtBearer + política admin + binding) sobre las 7 rutas, con ambos fakes registrados | RF-1 (tests) |
| `test/ContactHandlersInsertContactTests.cs` | Tests `InsertContact` | RF-2, RF-8 |
| `test/ContactHandlersGetContactTests.cs` | Tests `GetContact` | RF-3, CE-10 |
| `test/ContactHandlersGetContactsByEnterpriseTests.cs` | Tests `GetContactsByEnterprise` | RF-4, CE-6b |
| `test/ContactHandlersUpdateContactTests.cs` | Tests `UpdateContact` | RF-5, CE-1 |
| `test/ContactHandlersUpdateContactVisibilityTests.cs` | Tests `UpdateContactVisibility` | RF-6, CE-2, CE-12 |
| `test/ContactHandlersGetContactsByEnterpriseForSelectTests.cs` | Tests `GetContactsByEnterpriseForSelect` | RF-7, CE-6c |
| `test/ContactHandlersGetContactsForSelectTests.cs` | Tests `GetContactsForSelect` | RF-10, CE-18 |
| `test/ContactEndpointsMetadataTests.cs` | Verifica `WithName` y `Produces` de los 7 endpoints | RF-9 |
| `test/ContactAuthorizationTests.cs` | 401 sin sesión (7 endpoints), 403 no admin (4 endpoints admin), 200/operación con rol no admin en los 3 abiertos (CE-17) | RF-1, CE-7, CE-8, CE-9, CE-17 |
| `test/ContactModuleRegistrationTests.cs` | Composición DI: puertos, servicios y `scoped` | RNF-3 |

## 2. Funciones puras (handlers, sin efectos laterales propios)

| Función | Descripción | Parámetros | Retorno | RF |
|---------|-------------|------------|---------|-----|
| `ContactHandlers.InsertContactAsync` | Invoca `AddAsyncChild`, retorna 201 sin cuerpo | `IEnterpriseChildrenService<ContactDTO>, ContactDTO` | `Task<IResult>` | RF-2 |
| `ContactHandlers.GetContactAsync` | Crea DTO con `Id` de ruta, invoca `GetAsyncChild`, retorna 200/404 | `IEnterpriseChildrenService<ContactDTO>, int id` | `Task<IResult>` | RF-3 |
| `ContactHandlers.GetContactsByEnterpriseAsync` | Crea DTO con `EnterpriseId` de ruta, invoca `GetAsyncChildrenByEnterprise`, retorna 200 | `IEnterpriseChildrenService<ContactDTO>, int enterpriseId` | `Task<IResult>` | RF-4 |
| `ContactHandlers.UpdateContactAsync` | Sobrescribe `dto.Id` con el de ruta, invoca `UpdateAsyncChild`, retorna 204/404 | `IEnterpriseChildrenService<ContactDTO>, int id, ContactDTO dto` | `Task<IResult>` | RF-5 |
| `ContactHandlers.UpdateContactVisibilityAsync` | Crea DTO nuevo solo con `Id` de ruta y `Visibility`, invoca `UpdateAsyncVisibility`, retorna 204/404 | `IEnterpriseChildrenService<ContactDTO>, int id, ContactDTO dto` | `Task<IResult>` | RF-6 |
| `ContactHandlers.GetContactsByEnterpriseForSelectAsync` | Crea DTO con `EnterpriseId` de ruta, invoca `GetAsyncChildrenByEnterForSelect`, retorna 200 | `IEnterpriseChildrenService<ContactDTO>, int enterpriseId` | `Task<IResult>` | RF-7 |
| `ContactHandlers.GetContactsForSelectAsync` | Invoca `GetAsyncInfoForSelects` sin parámetros, retorna 200 | `ISelectService<ContactDTO>` | `Task<IResult>` | RF-10 |

No hay funciones con dependencia temporal: el caso de uso no trabaja con fechas, por lo que ningún parámetro «hoy» es necesario.

## 3. Algoritmo en pseudocódigo

### InsertContact (POST /contact/)
```
FUNCIÓN InsertContactAsync(service, dto):
    // RF-1.1/RF-1.2: sesión exigida por RequireAuthorization del grupo
    // RF-2.2: EnterpriseChildrenService/ContactRepository fija Visibility = ENABLED (ignora el del body)
    // RF-2.3: mapper → ContactEntity lanza EntityException (→ 400); RF-2.4: DbUpdateException (→ 500)
    ESPERAR service.AddAsyncChild(dto)
    RETORNAR Results.StatusCode(201)   // RF-2.5, RF-2.6: sin cuerpo ni Location
```

### GetContact (GET /contact/{id})
```
FUNCIÓN GetContactAsync(service, id):
    // RF-3.1: solo se asigna Id desde la ruta (la lectura no tiene cuerpo)
    dto = NUEVO ContactDTO { Id = id }
    // RF-3.2/RF-3.3: KeyNotFoundException → 404; RF-3.5: no filtra Visibility
    // RF-3.6: el repo no proyecta Visibility ni el handler lo añade
    contacto = ESPERAR service.GetAsyncChild(dto)
    RETORNAR Results.Ok(contacto)
```

### GetContactsByEnterprise (GET /contactsent/{enterpriseId})
```
FUNCIÓN GetContactsByEnterpriseAsync(service, enterpriseId):
    // RF-1.3/RF-1.7/RF-4.7: grupo admin (RequireAuthorization + AdminPolicy)
    dto = NUEVO ContactDTO { EnterpriseId = enterpriseId }   // RF-4.1
    // RF-4.2/RF-4.3: repo sin filtro Visibility; RF-4.4: lista vacía → 200
    contactos = ESPERAR service.GetAsyncChildrenByEnterprise(dto)
    RETORNAR Results.Ok(contactos)   // RF-4.5: Id, EnterpriseId, FullName, Visibility
```

### UpdateContact (PUT /contact/{id})
```
FUNCIÓN UpdateContactAsync(service, id, dto):
    // RF-1.3/RF-1.4: grupo admin
    dto.Id = id                        // RF-5.1, CE-1: prevalece el Id de la ruta
    // RF-5.2: solo FullName es editable (repo actualiza únicamente NombreCompleto)
    // RF-5.3: KeyNotFoundException → 404; RF-5.4: EntityException → 400
    ESPERAR service.UpdateAsyncChild(dto)
    RETORNAR Results.NoContent()       // RF-5.5
```

### UpdateContactVisibility (PUT /contactv/{id})
```
FUNCIÓN UpdateContactVisibilityAsync(service, id, dto):
    // RF-1.3/RF-1.4: grupo admin
    visibilityDto = NUEVO ContactDTO { Id = id, Visibility = dto.Visibility }  // RF-6.1, RF-6.2, CE-2
    // RF-6.3: UpdateAsyncVisibility valida ENABLED/DISABLED (→ ApplicationException → 400)
    // RF-6.4: KeyNotFoundException → 404
    ESPERAR service.UpdateAsyncVisibility(visibilityDto)
    RETORNAR Results.NoContent()       // RF-6.5
```

### GetContactsByEnterpriseForSelect (GET /contactsentsct/{enterpriseId})
```
FUNCIÓN GetContactsByEnterpriseForSelectAsync(service, enterpriseId):
    // RF-1.6: grupo autenticado (sin policy admin)
    dto = NUEVO ContactDTO { EnterpriseId = enterpriseId }   // RF-7.1
    // RF-7.2/RF-7.3: repo filtra Visibility = ENABLED; RF-7.4: vacío → 200
    contactos = ESPERAR service.GetAsyncChildrenByEnterForSelect(dto)
    RETORNAR Results.Ok(contactos)     // RF-7.5
```

### GetContactsForSelect (GET /contactsct/)
```
FUNCIÓN GetContactsForSelectAsync(selectService):
    // RF-1.3/RF-1.7/RF-10.5: grupo admin
    // RF-10.1/RF-10.2: repo filtra ENABLED; RF-10.3: proyección Id + FullName
    contactos = ESPERAR selectService.GetAsyncInfoForSelects()
    RETORNAR Results.Ok(contactos)     // RF-10.4: vacío → 200
```

Los `400` por `Id`/`enterpriseId` no numérico (RF-3.4, RF-4.6, RF-5.6, RF-6.6, RF-7.6) los produce el binding automático de minimal API al convertir el parámetro de ruta a `int`; los `400` por valor numérico ≤ 0 los produce `ContactEntity` vía mapper (`EntityException` → `ApiExceptionHandler` → 400). `401`/`403` los produce el pipeline de autenticación/autorización antes de ejecutar el handler.

## 4. Interfaz (endpoints)

| Endpoint | Método | Ruta | Grupo auth | WithName | Produces |
|----------|--------|------|------------|----------|----------|
| InsertContact | POST | `/contact/` | autenticado | `InsertContact` | 201, 400, 401, 500 |
| GetContact | GET | `/contact/{id}` | autenticado | `GetContact` | 200, 400, 401, 404 |
| GetContactsByEnterprise | GET | `/contactsent/{enterpriseId}` | admin | `GetContactsByEnterprise` | 200, 400, 401, 403 |
| UpdateContact | PUT | `/contact/{id}` | admin | `UpdateContact` | 204, 400, 401, 403, 404 |
| UpdateContactVisibility | PUT | `/contactv/{id}` | admin | `UpdateContactVisibility` | 204, 400, 401, 403, 404 |
| GetContactsByEnterpriseForSelect | GET | `/contactsentsct/{enterpriseId}` | autenticado | `GetContactsByEnterpriseForSelect` | 200, 400, 401 |
| GetContactsForSelect | GET | `/contactsct/` | admin | `GetContactsForSelect` | 200, 401, 403 |

Grupos, igual que en Device: `endpoints.MapGroup(string.Empty).RequireAuthorization()` y `endpoints.MapGroup(string.Empty).RequireAuthorization(AdminAuthorization.PolicyName)`.

## 5. Decisiones justificadas

| Decisión | Alternativa descartada | Justificación |
|----------|------------------------|---------------|
| Los 7 handlers usan `IEnterpriseChildrenService<ContactDTO>` / `ISelectService<ContactDTO>` (puertos primarios) | Invocar `IByEnterpriseRepository`/`ISelectRepository` desde los handlers | Arquitectura hexagonal: los adaptadores primarios solo hablan con puertos primarios (RF-2 de la constitución y RNF-2). Los servicios ya existen y encapsulan validación de `Visibility`/`Id` y el mapeo DTO↔Entity. |
| `UpdateContactVisibility` construye un DTO nuevo `{ Id, Visibility }` | Reutilizar el DTO del cuerpo y limpiar propiedades | Garantiza RF-6.2 y CE-2 por construcción (el resto de propiedades ni llega al caso de uso). Mismo patrón que Device (CE-2 de la spec 004). |
| `GetContact` construye `new ContactDTO { Id = id }` sin leer cuerpo | Enlazar un body en GET | RF-3.1 y criterio de finalización 6: la lectura no tiene cuerpo; el handler solo asigna el accesor `Id`. |
| `InsertContact` delega por completo el `Visibility = ENABLED` al caso de uso/repositorio | Validar o reescribir `Visibility` en el handler | RF-2.2: el valor del body se ignora; `ContactRepository.AddAsyncChild` ya fija `ENABLED` y el handler no debe tener lógica de negocio. |
| `IMapper<ContactDTO, ContactEntity>` **no** se registra en `AddContactModule` | (a) Registrarlo también en `AddContactModule` (descriptor duplicado); (b) Moverlo desde `AddUserModule` | Ya está registrado en `AddUserModule` (`ServiceCollectionExtensions.cs:25`) con la misma implementación y `Program.cs` compone ese módulo primero. (a) duplica el descriptor y rompe pruebas `Assert.Single` tipo las de `TypeModuleRegistrationTests`; (b) toca el módulo de otra spec (001/003) sin autorización. |
| `AddContactModule` registra `ContactRepository` hacia `IByEnterpriseRepository<ContactEntity, ContactDTO>` **e** `ISelectRepository<ContactDTO>` | Dos registros separados de la instancia o un repositorio nuevo | Un solo adaptador implementa ambos puertos secundarios (patrón Type/Enterprise); ambas resoluciones deben apuntar a la misma instancia en el ámbito. |
| Pruebas basadas en fakes + `ContactEndpointTestHost` (pipeline real de auth/binding) | Pruebas de integración contra SQL Server | No se pueden añadir paquetes (RNF-10/principio 1) y no hay servidor de BD garantizado en CI; el patrón ya validó 401/403/400 en specs 002–004. La proyección de `ContactRepository` es intocable (principio 5) y se verifica por inspección, no por test de BD. |
| `400` por ruta no numérica delegado en el binding de minimal API | Validación manual `int.TryParse` en cada handler | Mismo mecanismo ya probado en specs 002–004 (`idValue: "abc"` → 400); evita duplicar lógica en 5 endpoints (DRY). |

## 6. Estrategia de tests (`dotnet test`)

### Tests por endpoint (1 por endpoint + casos límite + error)

| Archivo de test | Qué prueba | RF |
|-----------------|------------|----|
| `ContactHandlersInsertContactTests.cs` | 201 sin cuerpo ni `Location`; DTO llega intacto al caso de uso; `Visibility` del body no lo reescribe el handler; `EntityException`→400; `ApplicationException`→400; `DbUpdateException`→500 (CE-16); mensaje en español; body malformado→400; 401 sin token | RF-2, RF-8, CE-11, CE-16 |
| `ContactHandlersGetContactTests.cs` | 200 con DTO devuelto por el servicio; `Id` de ruta sustituido; `KeyNotFoundException`→404; `id` no numérico→400 (binding); 200 cuando el servicio devuelve un contacto con `Visibility = DISABLED` (CE-10); respuesta sin campo `Visibility` (RF-3.6: el DTO de respuesta solo trae lo que proyecta el servicio); 401 | RF-3, CE-10, CE-13 |
| `ContactHandlersGetContactsByEnterpriseTests.cs` | 200 con la colección completa (incluye DISABLED, CE-6b); `EnterpriseId` de ruta asignado; colección vacía→200 (CE-6); `enterpriseId` no numérico→400; 403 no admin; 401 | RF-4, CE-6, CE-6b |
| `ContactHandlersUpdateContactTests.cs` | 204 sin cuerpo; `Id` de ruta prevalece sobre el del cuerpo (CE-1); `KeyNotFoundException`→404 (CE-5); `EntityException`→400 (CE-11); `id` no numérico→400; 403 no admin; 401 | RF-5, CE-1, CE-5, CE-11 |
| `ContactHandlersUpdateContactVisibilityTests.cs` | 204; solo viajan `Id`+`Visibility` (CE-2); `Visibility` inválido→400 vía `ApplicationException` (CE-12); `KeyNotFoundException`→404; `id` no numérico→400; 403 no admin; 401 | RF-6, CE-2, CE-12 |
| `ContactHandlersGetContactsByEnterpriseForSelectTests.cs` | 200 solo ENABLED (CE-6c); colección vacía→200; `enterpriseId` no numérico→400; 200 con rol no admin (CE-17); 401 | RF-7, CE-6c, CE-17 |
| `ContactHandlersGetContactsForSelectTests.cs` | 200 con proyección `Id`+`FullName` (sin `EnterpriseId`/`Visibility`, RF-10.3); colección vacía→200 (CE-18); 403 no admin; 401 (CE-14) | RF-10, CE-14, CE-18 |
| `ContactEndpointsMetadataTests.cs` | Los 7 endpoints tienen `WithName` exacto y `Produces` con todos los códigos de la tabla RF-9.2 | RF-9 |
| `ContactAuthorizationTests.cs` | 401 sin token y con token expirado en los 7; 403 con rol no admin y con `Role` ausente en los 4 admin (CE-9); 403 no ejecuta el caso de uso (`TotalCalls == 0`) | RF-1, CE-7, CE-8, CE-9 |
| `ContactModuleRegistrationTests.cs` | Todos los registros `scoped`, ambas interfaces secundarias → misma instancia `ContactRepository`, `IEnterpriseChildrenService<ContactDTO>` → `EnterpriseChildrenService<ContactEntity, ContactDTO>`, `ISelectService<ContactDTO>` → `SelectService<ContactDTO>`, container valida en build | RNF-3 |

### Convenciones
- Cada test crea sus propios datos/fakes; nada de estado compartido entre tests.
- Los handlers de error se prueban dos veces: lanzando la excepción desde el fake y verificando el código vía `HandlerTestSupport`/`TestHttp`, y a través del `ContactEndpointTestHost` para los casos que ocurren antes del handler (401, 403, binding 400).
- Criterio 16: cada endpoint tiene su prueba de éxito, su caso límite y su caso de error.
- Verificación final: `dotnet build` sin errores ni avisos nuevos y `dotnet test` verde (los avisos `NU1903` preexistentes de EF Core no bloquean).

## 7. Registro DI (`AddContactModule`)

```csharp
public static IServiceCollection AddContactModule(this IServiceCollection services)
{
    services.AddScoped<ContactRepository>();
    services.AddScoped<IByEnterpriseRepository<ContactEntity, ContactDTO>>(
        provider => provider.GetRequiredService<ContactRepository>());
    services.AddScoped<ISelectRepository<ContactDTO>>(
        provider => provider.GetRequiredService<ContactRepository>());

    services.AddScoped<EnterpriseChildrenService<ContactEntity, ContactDTO>>();
    services.AddScoped<IEnterpriseChildrenService<ContactDTO>>(
        provider => provider.GetRequiredService<EnterpriseChildrenService<ContactEntity, ContactDTO>>());

    services.AddScoped<SelectService<ContactDTO>>();
    services.AddScoped<ISelectService<ContactDTO>>(
        provider => provider.GetRequiredService<SelectService<ContactDTO>>());

    return services;
}
```

Y en `Program.cs`: `builder.Services.AddContactModule();` + `app.MapContactEndpoints();` (el mapper `ContactDTOtoEntityMapper` ya está registrado por `AddUserModule`).

## 8. Orden de implementación (tareas)

1. **Fakes de contacto** — `FakeContactChildrenService` y `FakeContactSelectService` (soporte de todos los tests).
2. **ContactHandlers.cs** — 7 handlers.
3. **ContactEndpointsExtensions.cs** — registro con grupos auth, `Produces` y `WithName`.
4. **ServiceCollectionExtensions.cs + Program.cs** — `AddContactModule` y mapeo.
5. **ContactEndpointTestHost.cs** — host con pipeline real y ambos fakes.
6. **Tests de handlers** — 7 archivos (éxito, límite y error por endpoint).
7. **Tests de metadatos y autorización** — RF-9 y RF-1.
8. **Tests de registro DI** — RNF-3.
9. **Verificación** — `dotnet build` + `dotnet test`.
