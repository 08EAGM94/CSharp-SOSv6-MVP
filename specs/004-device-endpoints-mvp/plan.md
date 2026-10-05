# Plan — Caso de Uso: Equipo (004-device-endpoints-mvp)

## 1. Archivos y responsabilidades

| Archivo | Responsabilidad | RF cubiertos |
|---------|----------------|--------------|
| `sosMVP/Extensions/DeviceEndpointsExtensions.cs` | Registro de los 6 endpoints con autorización, `WithName`, `Produces` | RF-1, RF-2 a RF-7, RF-9 |
| `sosMVP/Handlers/DeviceHandlers.cs` | Handlers de los 6 endpoints: binding de parámetros, invocación a servicios, mapeo de resultados a `IResult` | RF-2 a RF-7 |
| `sosMVP/Extensions/ServiceCollectionExtensions.cs` | Registro DI del módulo Device: repositorio, mappers, servicios | RNF-3 |
| `hexArch/application/abstractions/primaryPorts/IEnterpriseChildrenService.cs` | Ya existe - interfaz genérica para CRUD por empresa + visibilidad | RF-2 a RF-7 |
| `hexArch/application/abstractions/secondaryPorts/IByEnterpriseRepository.cs` | Ya existe - puerto para operaciones por empresa | RF-2 a RF-7 |
| `hexArch/application/useCases/EnterpriseChildrenService.cs` | Ya existe - implementación `IEnterpriseChildrenService` | RF-2 a RF-7 |
| `hexArch/data/mappers/dtoToEntity/DeviceDTOtoEntityMapper.cs` | Ya existe - mapeo DTO → Entity | RF-2, RF-5 |
| `hexArch/repository/DeviceRepository.cs` | Ya existe - implementación `IByEnterpriseRepository<DeviceEntity, DeviceDTO>` | RF-2 a RF-7 |
| `hexArch/domain/Entities/DeviceEntity.cs` | Ya existe - reglas de negocio (validaciones) | RF-2.3, RF-5.4, RF-8.2 |
| `test/DeviceHandlersInsertDeviceTests.cs` | Tests `InsertDevice` | RF-2 |
| `test/DeviceHandlersGetDeviceTests.cs` | Tests `GetDevice` | RF-3 |
| `test/DeviceHandlersGetDevicesByEnterpriseTests.cs` | Tests `GetDevicesByEnterprise` | RF-4 |
| `test/DeviceHandlersUpdateDevicesTests.cs` | Tests `UpdateDevices` | RF-5 |
| `test/DeviceHandlersUpdateDevicesVisibilityTests.cs` | Tests `UpdateDevicesVisibility` | RF-6 |
| `test/DeviceHandlersGetDevicesByEnterpriseForSelectTests.cs` | Tests `GetDevicesByEnterpriseForSelect` | RF-7 |
| `test/DeviceEndpointsMetadataTests.cs` | Tests metadatos `WithName`/`Produces` | RF-9 |
| `test/DeviceAuthorizationTests.cs` | Tests autorización (401, 403) | RF-1 |

## 2. Funciones puras (lógica sin efectos laterales)

| Función | Descripción | Parámetros | Retorno | RF |
|---------|-------------|------------|---------|-----|
| `DeviceHandlers.InsertDeviceAsync` | Valida sesión, invoca `AddAsyncChild`, retorna 201 | `IEnterpriseChildrenService<DeviceDTO>, DeviceDTO` | `Task<IResult>` | RF-2 |
| `DeviceHandlers.GetDeviceAsync` | Asigna `Id` de ruta, invoca `GetAsyncChild`, retorna 200/404 | `IEnterpriseChildrenService<DeviceDTO>, int id` | `Task<IResult>` | RF-3 |
| `DeviceHandlers.GetDevicesByEnterpriseAsync` | Asigna `EnterpriseId` de ruta, invoca `GetAsyncChildrenByEnterprise`, retorna 200 | `IEnterpriseChildrenService<DeviceDTO>, int enterpriseId` | `Task<IResult>` | RF-4 |
| `DeviceHandlers.UpdateDevicesAsync` | Asigna `Id` de ruta, invoca `UpdateAsyncChild`, retorna 204/404 | `IEnterpriseChildrenService<DeviceDTO>, int id, DeviceDTO dto` | `Task<IResult>` | RF-5 |
| `DeviceHandlers.UpdateDevicesVisibilityAsync` | Crea DTO con `Id` y `Visibility`, invoca `UpdateAsyncVisibility`, retorna 204/404 | `IEnterpriseChildrenService<DeviceDTO>, int id, DeviceDTO dto` | `Task<IResult>` | RF-6 |
| `DeviceHandlers.GetDevicesByEnterpriseForSelectAsync` | Asigna `EnterpriseId` de ruta, invoca `GetAsyncChildrenByEnterForSelect`, retorna 200 | `IEnterpriseChildrenService<DeviceDTO>, int enterpriseId` | `Task<IResult>` | RF-7 |

## 3. Algoritmo en pseudocódigo

### InsertDevice (POST /device/)
```
FUNCIÓN InsertDeviceAsync(service, dto):
    // RF-1.1, RF-1.2: Autorización ya validada por middleware (RequireAuthorization)
    // RF-2.2: Visibility se fija a ENABLED en el caso de uso (EnterpriseChildrenService)
    ESPERAR service.AddAsyncChild(dto)
    RETORNAR Results.StatusCode(201)
```

### GetDevice (GET /device/{id})
```
FUNCIÓN GetDeviceAsync(service, id):
    // RF-3.1: Id de ruta prevalece
    dto = NUEVO DeviceDTO { Id = id }
    // RF-3.2, RF-3.3, RF-3.5: GetAsyncChild lanza KeyNotFoundException si no existe (→ 404)
    // No filtra por Visibility (opción A)
    device = ESPERAR service.GetAsyncChild(dto)
    RETORNAR Results.Ok(device)
```

### GetDevicesByEnterprise (GET /devicesent/{enterpriseId})
```
FUNCIÓN GetDevicesByEnterpriseAsync(service, enterpriseId):
    // RF-1.3, RF-1.7, RF-4.7: Autorización admin validada por middleware (RequireAuthorization + AdminPolicy)
    // RF-4.1: EnterpriseId de ruta asignado a DTO
    dto = NUEVO DeviceDTO { EnterpriseId = enterpriseId }
    // RF-4.2, RF-4.3: Repo no filtra por Visibility (incluye DISABLED)
    // RF-4.4: Lista vacía si no hay registros
    devices = ESPERAR service.GetAsyncChildrenByEnterprise(dto)
    RETORNAR Results.Ok(devices)
```

### UpdateDevices (PUT /device/{id})
```
FUNCIÓN UpdateDevicesAsync(service, id, dto):
    // RF-1.3, RF-1.4: Autorización admin validada por middleware
    // RF-5.1: Id de ruta prevalece sobre body
    dto.Id = id
    // RF-5.2: Actualiza propiedades (sin Visibility)
    // RF-5.3: Lanza KeyNotFoundException si no existe (→ 404)
    // RF-5.4: Valida reglas negocio en Entity (→ EntityException → 400)
    ESPERAR service.UpdateAsyncChild(dto)
    RETORNAR Results.NoContent()
```

### UpdateDevicesVisibility (PUT /devicev/{id})
```
FUNCIÓN UpdateDevicesVisibilityAsync(service, id, dto):
    // RF-1.3, RF-1.4: Autorización admin validada por middleware
    // RF-6.1: Id de ruta prevalece
    // RF-6.2: Solo Id y Visibility viajan al caso de uso
    visibilityDto = NUEVO DeviceDTO { Id = id, Visibility = dto.Visibility }
    // RF-6.3: EnterpriseChildrenService.UpdateAsyncVisibility valida Visibility (→ ApplicationException → 400)
    // RF-6.4: Lanza KeyNotFoundException si no existe (→ 404)
    ESPERAR service.UpdateAsyncVisibility(visibilityDto)
    RETORNAR Results.NoContent()
```

### GetDevicesByEnterpriseForSelect (GET /devicesentsct/{enterpriseId})
```
FUNCIÓN GetDevicesByEnterpriseForSelectAsync(service, enterpriseId):
    // RF-1.6: Solo sesión válida (RequireAuthorization sin AdminPolicy)
    // RF-7.1: EnterpriseId de ruta asignado a DTO
    dto = NUEVO DeviceDTO { EnterpriseId = enterpriseId }
    // RF-7.2, RF-7.3: Repo filtra por Visibility = ENABLED
    // RF-7.4: Lista vacía si no hay habilitados
    // RF-7.5: Retorna solo Id, Brand, SerialNumber (Visibility no viaja)
    devices = ESPERAR service.GetAsyncChildrenByEnterForSelect(dto)
    RETORNAR Results.Ok(devices)
```

## 4. Interfaz (endpoints)

| Endpoint | Método | Ruta | Grupo auth | WithName | Produces |
|----------|--------|------|------------|----------|----------|
| InsertDevice | POST | `/device/` | authenticated | `InsertDevice` | 201, 400, 401, 500 |
| GetDevice | GET | `/device/{id}` | authenticated | `GetDevice` | 200, 400, 401, 404 |
| GetDevicesByEnterprise | GET | `/devicesent/{enterpriseId}` | admin | `GetDevicesByEnterprise` | 200, 400, 401, 403 |
| UpdateDevices | PUT | `/device/{id}` | admin | `UpdateDevices` | 204, 400, 401, 403, 404 |
| UpdateDevicesVisibility | PUT | `/devicev/{id}` | admin | `UpdateDevicesVisibility` | 204, 400, 401, 403, 404 |
| GetDevicesByEnterpriseForSelect | GET | `/devicesentsct/{enterpriseId}` | authenticated | `GetDevicesByEnterpriseForSelect` | 200, 400, 401 |

## 5. Decisiones justificadas

| Decisión | Alternativa descartada | Justificación |
|----------|------------------------|---------------|
| Usar `IEnterpriseChildrenService` (implementado por `EnterpriseChildrenService`) para todos los endpoints | Usar `ICommonService` + `IByEnterpriseRepository` directamente en handlers | `EnterpriseChildrenService` ya implementa los 6 métodos exactos necesarios (`AddAsyncChild`, `GetAsyncChild`, `GetAsyncChildrenByEnterprise`, `GetAsyncChildrenByEnterForSelect`, `UpdateAsyncChild`, `UpdateAsyncVisibility`); encapsula la lógica de validación de `Visibility` y mapeo DTO↔Entity. Evita duplicar lógica en handlers (DRY). |
| `GetDevicesByEnterprise` en grupo admin | Ponerlo en grupo authenticated como `GetEnterprises` | Requisito explícito del usuario: "GetDevicesByEnterprise a ser accedido con rol admin". Coherente con ser listado completo para gobernar catálogo. |
| `EnterpriseId` como route parameter `{enterpriseId}` | Query string `?enterpriseId=1` | Especificación explícita del usuario: "agregales el route parameter, el parámetro debe ser valor int". Más RESTful para recurso hijo de empresa. |
| Handlers usan `IEnterpriseChildrenService` (puerto primario) | Usar `IByEnterpriseRepository` (puerto secundario) directamente | Respeto a la arquitectura hexagonal: los handlers (adaptadores primarios) invocan puertos primarios (casos de uso), no puertos secundarios. `EnterpriseChildrenService` orquesta validaciones y delega al repo. |
| Validación `enterpriseId` numérico en route (400 automático) | Validación manual en handler | Minimal API valida automáticamente route parameters `{int:id}` → 400 si no es int. Si se usa `{enterpriseId:int}` en la ruta. |
| `UpdateDevicesVisibility` crea nuevo DTO solo con Id/Visibility | Reusar DTO recibido y limpiar propiedades | Inmutabilidad y claridad: evita que propiedades extra viajen al caso de uso (RF-6.2, CE-2). Patrón usado en Type/Enterprise. |

## 6. Estrategia de tests (`dotnet test`)

### Tests por endpoint (1 por endpoint + casos límite + error)
| Test file | Qué prueba | RF |
|-----------|------------|----|
| `DeviceHandlersInsertDeviceTests.cs` | 201 éxito, 400 DTO inválido, 500 unicidad, 401 sin token | RF-2, RF-8 |
| `DeviceHandlersGetDeviceTests.cs` | 200 éxito, 404 no existe, 400 id inválido, 200 con Visibility=DISABLED, 401 | RF-3, CE-10 |
| `DeviceHandlersGetDevicesByEnterpriseTests.cs` | 200 lista completa (incluye DISABLED), 403 no admin, 400 enterpriseId inválido, 401 | RF-4, RF-1.7, CE-6b |
| `DeviceHandlersUpdateDevicesTests.cs` | 204 éxito, 404 no existe, 400 DTO inválido, 403 no admin, 401, CE-1 | RF-5 |
| `DeviceHandlersUpdateDevicesVisibilityTests.cs` | 204 éxito, 404 no existe, 400 Visibility inválido, 403 no admin, 401, CE-2 | RF-6 |
| `DeviceHandlersGetDevicesByEnterpriseForSelectTests.cs` | 200 solo ENABLED, 400 enterpriseId inválido, 401, CE-6c | RF-7 |
| `DeviceEndpointsMetadataTests.cs` | Verifica `WithName` y `Produces` en los 6 endpoints | RF-9 |
| `DeviceAuthorizationTests.cs` | 401 sin token en todos, 403 no admin en 3 endpoints admin | RF-1 |

### Convenciones de tests
- Cada test crea sus propios datos (BaseTestClass o similar)
- Usan `TestHttp` y `JwtTestTokens` existentes en `test/Support/`
- `WebApplicationFactory<Program>` para integration tests
- `dotnet test` debe pasar sin fallos
- `dotnet build` no debe reportar advertencias de código ni de analizadores (criterio 14 de la spec). Los avisos `NU1903` de auditoría NuGet sobre `System.Security.Cryptography.Xml` son deuda técnica preexistente y transitiva de EF Core SqlServer: quedan declarados en la spec y no bloquean esta tarea.

## 7. Registro DI (ServiceCollectionExtensions.cs)

```csharp
public static IServiceCollection AddDeviceModule(this IServiceCollection services)
{
    services.AddScoped<DeviceRepository>();
    services.AddScoped<IByEnterpriseRepository<DeviceEntity, DeviceDTO>>(
        provider => provider.GetRequiredService<DeviceRepository>());

    services.AddScoped<IMapper<DeviceDTO, DeviceEntity>, DeviceDTOtoEntityMapper>();

    services.AddScoped<EnterpriseChildrenService<DeviceEntity, DeviceDTO>>();
    services.AddScoped<IEnterpriseChildrenService<DeviceDTO>>(
        provider => provider.GetRequiredService<EnterpriseChildrenService<DeviceEntity, DeviceDTO>>());

    return services;
}
```
Y en `Program.cs`: `builder.Services.AddDeviceModule();` + `app.MapDeviceEndpoints();`

## 8. Orden de implementación (tareas)

1. **DeviceEndpointsExtensions.cs** - Registro endpoints con auth, WithName, Produces
2. **DeviceHandlers.cs** - 6 handlers
3. **ServiceCollectionExtensions.cs** - AddDeviceModule
4. **Program.cs** - Registrar módulo y mapear endpoints
5. **Tests** - 8 archivos de test (orden según dependencias)
6. **Verificación** - `dotnet build` + `dotnet test`