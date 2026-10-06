# Tareas — Spec 006: Caso de uso Bitácora

- **Base:** `specs/006-binnacle-endpoints-mvp/spec.md` + `specs/006-binnacle-endpoints-mvp/plan.md`.
- **Orden:** de dependencia. Solo se ejecuta una tarea cada vez; se marca `[x]` y se PARA.
- **TDD:** primero los tests de la tarea (en rojo), después el código. Nunca se cierra una tarea con `dotnet test` en rojo.
- **Comprobación de cierre de cada tarea:** `dotnet build` (0 errores) y `dotnet test test/test.csproj` (0 fallos).

---

- [x] **T1. Caso de uso: normalización del alta y validaciones.** RF-2.2, RF-4.3, RF-5.2, RF-6.3, RF-7.3, RF-12.3, RF-12.6
  - Archivos: `test/Fakes/FakeBinnacleRepository.cs` (nuevo), `test/Fakes/FakeBinnacleService.cs` (nuevo), `test/BinnacleServiceTests.cs` (nuevo), `hexArch/application/useCases/BinnacleService.cs` (modificar).
  - Tests en rojo → código: `NormalizeForInsert` vacía `Status`, `ActivitiesDone`, `Hints` y `CancelDesc` en `AddAsyncInfo`; `IsValidPagination` lanza `ApplicationException` cuando `page`/`elemsKey` no son ≥ 1 en `GetAsyncAllInfo`; `IsValidStatus`/`IsValidVisibility` quedan como helpers puros usados por `UpdateAsyncInfo` y `UpdateAsyncVisibility`.
  - Hecho cuando: `dotnet test` ejecuta `BinnacleServiceTests.cs` en verde — el DTO de alta llega al repositorio con los cuatro campos vacíos y con el `UserId` indicado; `page=0`, `elemsKey=0` o nulos producen `ApplicationException` con mensaje en español; `Status` nulo/desconocido y `Visibility` fuera de `ENABLED`/`DISABLED` producen `ApplicationException`; los tests previos siguen en verde.

- [x] **T2. Reglas de dominio de la bitácora.** RF-2.3, RF-3.5, RF-8.5, RF-10.5, RF-11.5
  - Archivo: `test/BinnacleEntityTests.cs` (nuevo). Sin cambios de código (`BinnacleEntity` ya existe).
  - Hecho cuando: los tests cubren `ContactId` ≤ 0, `Service` < 15, `Amount` ≤ 0 y `CustomerSignature` > 255 → `EntityException`; `ActivitiesDone`/`Hints` con valor y < 15 e `Id`/`UserId` ≤ 0 → `EntityException`; la nulidad de texto no lanza; y todos pasan con `dotnet test` en verde.

- [x] **T3. Handlers de alta y consulta.** RF-1.7, RF-2.1, RF-2.5, RF-2.6, RF-3.1, RF-3.2, RF-3.3, RF-12.1, RF-12.2, RF-12.5, RF-12.6
  - Archivos: `test/BinnacleHandlersInsertBinnacleTests.cs`, `test/BinnacleHandlersGetBinnacleTests.cs` (nuevos), `sosMVP/Handlers/BinnacleHandlers.cs` (nuevo, con `InsertBinnacleAsync` y `GetBinnacleAsync`).
  - Tests en rojo → código: `InsertBinnacleAsync` devuelve `201` sin cuerpo ni `Location`, fija el `UserId` de la sesión e ignora el del cuerpo, y responde `401` sin llegar al caso de uso cuando el `claim` `Id` no es legible; `GetBinnacleAsync` genera el DTO con `Id` de ruta y `UserId` de sesión y devuelve `200`.
  - Hecho cuando: los dos archivos de tests pasan en verde cubriendo `KeyNotFoundException`→404, `EntityException`→400, `DbUpdateException`→500, mensajes en español y `401` sin `claim` `Id`; `dotnet build` sin errores.

- [x] **T4. Handlers de listado (seguimiento y reporte).** RF-1.7, RF-4.1, RF-4.2, RF-4.4, RF-5.1, RF-5.3
  - Archivos: `test/BinnacleHandlersFollowupListTests.cs`, `test/BinnacleHandlersBinnaclesReportTests.cs` (nuevos), `sosMVP/Handlers/BinnacleHandlers.cs` (añadir `FollowupListAsync`, `BinnaclesReportAsync` y el record `BinnaclesReportRequest`).
  - Tests en rojo → código: `FollowupListAsync` construye el DTO con **solo** el `UserId` de la sesión y delega `page`, `elemsKey`, la constante `"FollowupList"` y `binnFilter` nulo, devolviendo `200` con `PaginationResult`; `BinnaclesReportAsync` delega `page`, `elemsKey`, `binnFilter` y la constante `"BinnaclesReport"` con DTO nulo, devolviendo `200` con `PaginationResult`.
  - Hecho cuando: ambos archivos en verde cubriendo `401` sin `claim` `Id` y `ApplicationException`→400 con `page`/`elemsKey` ≤ 0; los tests anteriores siguen en verde.

- [x] **T5. Handlers de gobierno (actualización y visibilidad).** RF-6.1, RF-6.6, RF-7.1, RF-7.2, RF-7.6, CE-1, CE-2, CE-12, CE-13
  - Archivos: `test/BinnacleHandlersUpdateBinnacleTests.cs`, `test/BinnacleHandlersUpdateVisibilityTests.cs` (nuevos), `sosMVP/Handlers/BinnacleHandlers.cs` (añadir `UpdateBinnacleAsync`, `UpdateBinnacleVisibilityAsync`).
  - Tests en rojo → código: `UpdateBinnacleAsync` sustituye el `Id` del cuerpo por el de la ruta y devuelve `204` sin cuerpo; `UpdateBinnacleVisibilityAsync` envía al caso de uso **únicamente** `Id` y `Visibility`.
  - Hecho cuando: ambos archivos en verde cubriendo `ApplicationException` (Status desconocido, Visibility fuera de `ENABLED`/`DISABLED`)→400, `KeyNotFoundException`→404, `id` de ruta ≤ 0 →400 y delegación exactamente una vez; `dotnet build` sin errores.

- [x] **T6. Handlers de seguimiento parcial y reinicio de actividades.** RF-1.7, RF-8.1, RF-8.6, RF-9.1, RF-9.6, RF-12.4, CE-3, CE-4, CE-14, CE-15, CE-23
  - Archivos: `test/BinnacleHandlersFollowupPartialTests.cs`, `test/BinnacleHandlersResetActivitiesTests.cs` (nuevos), `sosMVP/Handlers/BinnacleHandlers.cs` (añadir `FollowupPartialAsync`, `ResetActivitiesAsync`).
  - Tests en rojo → código: ambos handlers fijan `Id` de ruta y `UserId` de sesión; `ResetActivitiesAsync` no lee cuerpo (genera el DTO); `204` sin cuerpo.
  - Hecho cuando: ambos archivos en verde cubriendo `KeyNotFoundException`→404, `EntityException`→400, `401` sin `claim` `Id`, y que la excepción de estatus prohibido que lanza el repositorio (`"El acceso a esta bitácora está prohibido."` en RF-8.7 y la de RF-9.4) se traduce a `400` con mensaje en español.

- [x] **T7. Handlers de cancelación y finalización.** RF-1.7, RF-10.1, RF-10.6, RF-11.1, RF-11.6, RF-12.4, CE-1, CE-4, CE-15, CE-26, CE-27
  - Archivos: `test/BinnacleHandlersCancelBinnacleTests.cs`, `test/BinnacleHandlersFinishBinnacleTests.cs` (nuevos), `sosMVP/Handlers/BinnacleHandlers.cs` (añadir `CancelBinnacleAsync`, `FinishBinnacleAsync`).
  - Tests en rojo → código: ambos handlers fijan `Id` de ruta y `UserId` de sesión, delegan el cuerpo del DTO y devuelven `204` sin cuerpo.
  - Hecho cuando: ambos archivos en verde cubriendo `KeyNotFoundException`→404, `EntityException`→400 (`CancelDesc` corto y firma > 255), `401` sin `claim` `Id`, y la traducción a `400` de las excepciones de estatus de RF-10.7 y RF-11.7; `dotnet build` sin errores.

- [x] **T8. Registro del módulo de bitácora.** RNF-2, RNF-3
  - Archivos: `test/BinnacleModuleRegistrationTests.cs` (nuevo), `sosMVP/Extensions/ServiceCollectionExtensions.cs` y `sosMVP/Program.cs` (modificar).
  - Tests en rojo → código: `AddBinnacleModule()` registra `BinnacleRepository`/`IBinnacleRepository`, `IMapper<BinnacleDTO, BinnacleEntity>` e `IBinnacleService` con vida `scoped`, y `Program.cs` lo llama.
  - Hecho cuando: `BinnacleModuleRegistrationTests.cs` en verde comprueba los cuatro registros con vida `scoped` y su resolución, y `dotnet build` sin errores.

- [x] **T9. Rutas, contrato documentado y mapeo.** RF-13.1, RF-13.2, RF-13.3, RF-1.3, RF-1.6, CE-7
  - Archivos: `test/BinnacleEndpointsMetadataTests.cs` (nuevo), `sosMVP/Extensions/BinnacleEndpointsExtensions.cs` (nuevo), `sosMVP/Program.cs` (añadir `MapBinnacleEndpoints()`).
  - Tests en rojo → código: los diez endpoints con sus rutas exactas, sus `WithName(...)` de la tabla y sus `Produces(...)` exactos; los siete no administradores sin la política `AdminOnly` y los tres (`BinnaclesReport`, `UpdateBinnacle`, `UpdateBinnacleVisibility`) con ella; ningún endpoint anónimo; `{id}`, `{page}` y `{elemsKey}` como `int` obligatorios.
  - Hecho cuando: `BinnacleEndpointsMetadataTests.cs` en verde (diez rutas localizadas, códigos y nombres exactos, grupos correctos) y `dotnet build` sin errores.

- [x] **T10. Sesión y autorización de los diez endpoints.** RF-1.1, RF-1.2, RF-1.3, RF-1.4, RF-1.5, RF-1.6, RF-1.7, RF-1.8, CE-6, CE-8, CE-9, CE-10, CE-11, CE-18
  - Archivos: `test/Support/BinnacleEndpointTestHost.cs` (nuevo), `test/BinnacleAuthorizationTests.cs` (nuevo).
  - Tests en rojo → código: el host compone JWT real, la política `AdminOnly` y el despacho por ruta; si hace falta, se ajusta el grupo de rutas para que coincida con lo que los tests exigen.
  - Hecho cuando: `BinnacleAuthorizationTests.cs` en verde comprueba `401` sin token, con token caducado, con token de firma alterada (RF-1.5.c vía RF-1.2, diez endpoints) y con token sin `claim` `Id`; `403` en los tres endpoints administradores con rol `user`, rol ausente **o rol con valor fuera de `admin`/`user` (RF-1.5.b)**; los otros siete aceptan rol `user` sin `403`; y en todos los rechazos el fake del caso de uso registra **cero** llamadas; `dotnet build` y `dotnet test` completos en verde.

---

**Nota (D1 del plan):** las reglas de estatus de RF-8.7, RF-9.4, RF-10.7 y RF-11.7 viven en `BinnacleRepository` y no se ejecutan en `dotnet test`; las tareas T6 y T7 cubren su efecto observable en el endpoint (traducción a `400` con mensaje en español).
