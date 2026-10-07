# Tareas — Spec 003: Caso de uso Empresa

- **Base:** `specs/003-enterprise-endpoints-mvp/spec.md` + `specs/003-enterprise-endpoints-mvp/plan.md`.
- **Orden:** de dependencia. Solo se ejecuta una tarea cada vez; se marca `[x]` y se PARA.
- **TDD:** primero los tests de la tarea (en rojo), después el código. Nunca se cierra una tarea con `dotnet test` en rojo.
- **Comprobación de cierre de cada tarea:** `dotnet build` (0 errores) y `dotnet test test/test.csproj` (0 fallos).

---

- [X] **T1. Añadir `AddEnterpriseModule()` en `ServiceCollectionExtensions.cs`.** RF-2, RF-3, RF-4, RF-5, RF-6, RF-7, RNF-3

- Hecho cuando: `dotnet build` compila; el método registra `EnterpriseRepository`, `IRepository<EnterpriseEntity,EnterpriseDTO>`, `ISelectRepository<EnterpriseDTO>`, `EnterpriseDTOtoEntityMapper`, `CommonService<EnterpriseEntity,EnterpriseDTO>`, `ICommonService<EnterpriseDTO>`, `SelectService<EnterpriseDTO>`, `ISelectService<EnterpriseDTO>` con vida `scoped`.

- [X] **T2. Crear `EnterpriseHandlers.cs` con 6 handlers estáticos.** RF-2, RF-3, RF-4, RF-5, RF-6, RF-7

- Hecho cuando: `dotnet build` compila; los 6 métodos existen con firmas correctas y delegan a `ICommonService<EnterpriseDTO>` / `ISelectService<EnterpriseDTO>`; `InsertEnterpriseAsync` ignora `ContactDTO` sin `FullName` (RF-2.9); `UpdateEnterpriseAsync` y `UpdateEnterpriseVisibilityAsync` fuerzan `id` de ruta; `GetEnterprisesForSelectsAsync` usa `ISelectService`.

- [X] **T3. Crear `EnterpriseEndpointsExtensions.cs` con `MapEnterpriseEndpoints()`.** RF-1, RF-9

- Hecho cuando: `dotnet build` compila; 6 endpoints registrados con `MapGroup` correcto (`authenticatedEndpoints` para 3: InsertEnterprise, GetEnterprise, GetEnterprisesForSelects; `adminEndpoints` para 3: GetEnterprises, UpdateEnterprise, UpdateEnterpriseVisibility), `Produces` exactos de la tabla de la spec, `WithName` exactos.

- [X] **T4. Registrar módulo Enterprise en `Program.cs`.** RF-1, RF-9

- Hecho cuando: `dotnet build` compila; `builder.Services.AddEnterpriseModule()` y `app.MapEnterpriseEndpoints()` invocados; la app arranca sin errores.

- [X] **T5. Crear fakes de test: `FakeEnterpriseCommonService.cs` y `FakeEnterpriseSelectService.cs`.** RF-2, RF-3, RF-4, RF-5, RF-6, RF-7

- Hecho cuando: `dotnet build` compila; ambos fakes implementan `ICommonService<EnterpriseDTO>` / `ISelectService<EnterpriseDTO>` con contadores de llamadas, propiedades `LastAddedDto`, `LastAddedContact`, `ExceptionToThrow`, y resultados configurables (`InfoResult`, `AllResult`, `SelectResult`).

- [X] **T6. Tests `EnterpriseHandlersInsertEnterpriseTests.cs` (happy path + límites + errores).** RF-2.1–2.9, RF-8.2, RF-8.5, CE-4, CE-11, CE-12, CE-17, CE-18

- Hecho cuando: `dotnet test --filter "EnterpriseHandlersInsertEnterpriseTests"` pasa en verde; cubre: 201 sin cuerpo ni Location, delegación única al caso de uso, contacto con FullName se crea, contacto sin FullName se ignora (RF-2.9), Visibility del cuerpo no se valida, conflicto índice único → 500, longitudes empresa → 400, longitud contacto → 400, **fallo del caso de uso al crear el contacto → 500 y llamada única sin reintento (CE-17)**, mensaje en español.

- [X] **T7. Tests `EnterpriseHandlersGetEnterpriseTests.cs`.** RF-3.1–3.5, RF-8.1, RF-8.4, CE-3, CE-10, CE-14

- Hecho cuando: `dotnet test --filter "EnterpriseHandlersGetEnterpriseTests"` pasa en verde; cubre: 200 con DTO completo, empresa DISABLED → 200 no 404, id de ruta prevalece, inexistente → 404, id no numérico → 400, fallo negocio → 400, mensaje en español.

- [X] **T8. Tests `EnterpriseHandlersGetEnterprisesTests.cs`.** RF-4.1–4.5, RF-8.4, CE-6, CE-6b

- Hecho cuando: `dotnet test --filter "EnterpriseHandlersGetEnterprisesTests"` pasa en verde; cubre: 200 con colección, incluye DISABLED, NO incluye Visibility en respuesta (llega null), vacío → 200 con [], **usuario no admin → 403 (RF-4.5)**, fallo negocio → 400, mensaje en español.

- [X] **T9. Tests `EnterpriseHandlersUpdateEnterpriseTests.cs`.** RF-5.1–5.6, RF-8.1, RF-8.2, CE-1, CE-3, CE-5, CE-11, CE-14

- Hecho cuando: `dotnet test --filter "EnterpriseHandlersUpdateEnterpriseTests"` pasa en verde; cubre: 204 sin cuerpo, id ruta prevalece, inexistente → 404, id no numérico → 400, longitudes → 400, fallo negocio → 400, mensaje en español.

- [X] **T10. Tests `EnterpriseHandlersUpdateEnterpriseVisibilityTests.cs`.** RF-6.1–6.6, RF-8.1, RF-8.3, CE-1, CE-2, CE-3, CE-13, CE-14

- Hecho cuando: `dotnet test --filter "EnterpriseHandlersUpdateEnterpriseVisibilityTests"` pasa en verde; cubre: 204 sin cuerpo, id ruta prevalece, otras propiedades ignoradas, Visibility inválido → 400, inexistente → 404, id no numérico → 400, fallo negocio → 400, mensaje en español.

- [X] **T11. Tests `EnterpriseHandlersGetEnterprisesForSelectsTests.cs`.** RF-7.1–7.4, RF-8.3, RF-8.4, RF-8.6, CE-6, CE-6c

- Hecho cuando: `dotnet test --filter "EnterpriseHandlersGetEnterprisesForSelectsTests"` pasa en verde; cubre: 200 con colección ENABLED, usa `ISelectService` no `ICommonService`, colección llega intacta (sin filtrar ni añadir), filtro ENABLED vive en repo (handler no filtra), todo DISABLED → [], vacío → [], fallo caso de uso → 400, fallo genérico → 400, mensaje en español.

- [X] **T12. Corregir cuerpo de `POST /enterprise/` y firma de `InsertEnterpriseAsync` (record envoltorio + handler con un único body).** RF-2.1, RF-2.2, RF-2.9

- Hecho cuando: `dotnet build` compila; **la tabla de rutas se materializa sin error** (verificado enumerando `EndpointDataSource.Endpoints` o con una petición real que no responda 500); la suite completa `dotnet test` sigue en verde, **incluidos los tests de T6 a T11** (los puntos de llamada de `EnterpriseHandlersInsertEnterpriseTests.cs` que invocaban la firma antigua `commonService, dto, contact` se actualizan en esta misma tarea para que la suite no se rompa; **justificación**: el cambio de firma es atómico y rompería la compilación si no se actualizan los tests a la vez — hacerlo en la misma tarea evita un estado intermedio de "build roto" y mantiene la atomicidad de la tarea).

- [X] **T13. Tests `EnterpriseAuthorizationTests.cs` (401/403).** RF-1.1–1.7, CE-7, CE-8, CE-9, CE-15

- Hecho cuando: `dotnet test --filter "EnterpriseAuthorizationTests"` pasa en verde; cubre: 6 endpoints → 401 sin token / token expirado; `GetEnterprises`, `UpdateEnterprise`, `UpdateEnterpriseVisibility` → 403 con rol `user`; 403 con sesión manipulada; `InsertEnterprise`, `GetEnterprise`, `GetEnterprisesForSelects` → 200/201 con rol `user` (no 403).

- [X] **T14. Tests `EnterpriseEndpointsMetadataTests.cs` (WithName + Produces).** RF-9.1, RF-9.2, RF-9.3

- Hecho cuando: `dotnet test --filter "EnterpriseEndpointsMetadataTests"` pasa en verde; verifica que cada endpoint tiene `WithName` exacto y `Produces` con códigos exactos de la tabla de la spec (**incluye 403 en GetEnterprises**).

- [X] **T15. Ejecutar suite completa y confirmar `dotnet build` + `dotnet test` en verde.** Todos los RF

- Hecho cuando: `dotnet build` compila sin warnings; `dotnet test` ejecuta **todos** los tests (incluidos los de specs 001 y 002) y pasa en verde; ¡Listo precioso!
