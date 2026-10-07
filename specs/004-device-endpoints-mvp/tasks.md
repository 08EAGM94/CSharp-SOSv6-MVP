# Tareas — Spec 004: Caso de uso Equipo

- **Base:** `specs/004-device-endpoints-mvp/spec.md` + `specs/004-device-endpoints-mvp/plan.md`.
- **Orden:** de dependencia. Solo se ejecuta una tarea cada vez; se marca `[x]` y se PARA.
- **TDD:** primero los tests de la tarea (en rojo), después el código. Nunca se cierra una tarea con `dotnet test` en rojo.
- **Comprobación de cierre de cada tarea:** `dotnet build` (0 errores) y `dotnet test test/test.csproj` (0 fallos).

---

- [x] **T1. Crear DeviceHandlers.cs.** RF-2 a RF-7
  - Hecho cuando: `dotnet build` compila; 6 handlers estáticos async retornando `IResult`; binding correcto de `id`/`enterpriseId`; invocación a `IEnterpriseChildrenService<DeviceDTO>` según pseudocódigo del plan; `Results.Ok`/`NoContent`/`StatusCode(201)`.

- [x] **T2. Crear DeviceEndpointsExtensions.cs.** RF-1, RF-2 a RF-7, RF-9
  - Hecho cuando: `dotnet build` compila; archivo existe en `sosMVP/Extensions/` con 6 endpoints registrados, grupos `authenticated` (3) y `admin` (3), `WithName` exactos, `Produces` según tabla del plan.

- [x] **T3. Registrar módulo Device (DI + Program.cs).** RNF-3, RF-1, RF-9
  - Hecho cuando: `dotnet build` compila; `AddDeviceModule` en `ServiceCollectionExtensions.cs` registra repo, mappers, `EnterpriseChildrenService<DeviceEntity, DeviceDTO>` e `IEnterpriseChildrenService<DeviceDTO>` (`scoped`); `Program.cs` tiene `builder.Services.AddDeviceModule()` y `app.MapDeviceEndpoints()`.

- [x] **T4. Tests InsertDevice y GetDevice.** RF-2, RF-3, RF-8, CE-10
  - Hecho cuando: `dotnet test --filter "InsertDevice|GetDevice"` pasa; cubre: 201/200 éxito, 400 DTO/id inválido, 404 no existe, 500 unicidad (Insert), 200 con Visibility=DISABLED (Get), 401 sin token.

- [x] **T5. Tests GetDevicesByEnterprise.** RF-4, RF-1.7, CE-6b
  - Hecho cuando: `dotnet test --filter "GetDevicesByEnterprise"` pasa; cubre: 200 lista completa (incluye DISABLED), 403 no admin, 400 enterpriseId inválido, 401 sin token.

- [x] **T6. Tests UpdateDevices.** RF-5, CE-1
  - Hecho cuando: `dotnet test --filter "UpdateDevices"` pasa; cubre: 204 éxito, 404 no existe, 400 DTO inválido, 403 no admin, 401 sin token, CE-1 (Id body ≠ route).

- [x] **T7. Tests UpdateDevicesVisibility.** RF-6, CE-2
  - Hecho cuando: `dotnet test --filter "UpdateDevicesVisibility"` pasa; cubre: 204 éxito, 404 no existe, 400 Visibility inválido, 403 no admin, 401 sin token, CE-2 (propiedades extra ignoradas).

- [x] **T8. Tests GetDevicesByEnterpriseForSelect.** RF-7, CE-6c
  - Hecho cuando: `dotnet test --filter "GetDevicesByEnterpriseForSelect"` pasa; cubre: 200 solo ENABLED, 400 enterpriseId inválido, 401 sin token, CE-6c (no incluye DISABLED).

- [x] **T9. Tests metadatos y autorización (cross-cutting).** RF-1, RF-9
  - Hecho cuando: `dotnet test --filter "DeviceEndpointsMetadata|DeviceAuthorization"` pasa; verifica `WithName` exactos y `Produces` códigos en 6 endpoints; 401 sin token en todos; 403 no admin en 3 endpoints admin.

- [x] **T10. Verificación completa.** Todos RF
  - Hecho cuando: `dotnet build` y `dotnet test` (sin filtro) pasan en verde; 0 fallos; 0 advertencias de código y de analizadores (los avisos `NU1903` de auditoría NuGet son deuda técnica preexistente declarada en la spec).