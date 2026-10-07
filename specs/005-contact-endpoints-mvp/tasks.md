# Tareas — Spec 005: Caso de uso Contacto

- **Base:** `specs/005-contact-endpoints-mvp/spec.md` + `specs/005-contact-endpoints-mvp/plan.md`.
- **Orden:** de dependencia. Solo se ejecuta una tarea cada vez; se marca `[x]` y se PARA.
- **TDD:** primero los tests de la tarea (en rojo), después el código. Nunca se cierra una tarea con `dotnet test` en rojo.
- **Comprobación de cierre de cada tarea:** `dotnet build` (0 errores) y `dotnet test test/test.csproj` (0 fallos).

---

- [x] **T1. Fakes de contacto: `FakeContactChildrenService` y `FakeContactSelectService`.** RF-2, RF-3, RF-4, RF-5, RF-6, RF-7, RF-10
- Hecho cuando: `dotnet build` compila y los fakes exponen contadores de llamada, últimos DTO recibidos, resultados configurables y `ExceptionToThrow` para `IEnterpriseChildrenService<ContactDTO>` e `ISelectService<ContactDTO>`.

- [x] **T2. Handlers de los 7 endpoints en `sosMVP/Handlers/ContactHandlers.cs`.** RF-2.1 a RF-2.6, RF-3.1 a RF-3.6, RF-4.1 a RF-4.5, RF-5.1 a RF-5.5, RF-6.1 a RF-6.5, RF-7.1 a RF-7.5, RF-10.1 a RF-10.4
- Hecho cuando: cada handler invoca solo su puerto primario, `GetContact` construye `new ContactDTO { Id = id }` sin body, `UpdateContact` sobrescribe `dto.Id` con el de la ruta, `UpdateContactVisibility` crea un DTO nuevo solo con `Id` y `Visibility`, y `InsertContact` retorna 201 sin cuerpo.

- [x] **T3. Registro de endpoints en `sosMVP/Extensions/ContactEndpointsExtensions.cs`.** RF-1, RF-9.1, RF-9.2, RF-9.3
- Hecho cuando: existen los 7 endpoints con las rutas, grupos (4 admin, 3 autenticado), `WithName` exactos y `Produces` con todos los códigos de la tabla RF-9.2.

- [x] **T4. DI y composición: `AddContactModule` en `ServiceCollectionExtensions.cs` y registro en `Program.cs`.** RNF-2, RNF-3
- Hecho cuando: `ContactRepository` se resuelve igual hacia `IByEnterpriseRepository<ContactEntity, ContactDTO>` y `ISelectRepository<ContactDTO>`, `EnterpriseChildrenService<ContactEntity, ContactDTO>` y `SelectService<ContactDTO>` quedan registrados como `scoped`, y `Program.cs` llama `AddContactModule()` y `MapContactEndpoints()`.

- [x] **T5. Host de pruebas `test/Support/ContactEndpointTestHost.cs`.** RF-1
- Hecho cuando: el host publica las 7 rutas con el pipeline real de JwtBearer + política admin, registra ambos fakes, y permite enviar peticiones con/sin token, rol y body devolviendo `TestHttpResponse`.

- [x] **T6. Tests de `InsertContact` y `GetContact`.** RF-2, RF-3, RF-8, CE-10, CE-11, CE-13, CE-16
- Hecho cuando: `dotnet test` pasa con: 201 sin cuerpo ni `Location`, `EntityException`→400, `DbUpdateException`→500, `KeyNotFoundException`→404, `id` no numérico→400, 200 con contacto `DISABLED`, respuesta sin `Visibility` y 401 sin sesión.

- [x] **T7. Tests de `GetContactsByEnterprise` y `GetContactsByEnterpriseForSelect`.** RF-4, RF-7, CE-6, CE-6b, CE-6c, CE-17
- Hecho cuando: `dotnet test` pasa con: listado completo que incluye `DISABLED`, listado de select solo `ENABLED`, colección vacía→200, `enterpriseId` no numérico→400, 403 no admin en `/contactsent/`, 200 con rol no admin en `/contactsentsct/` y 401.

- [x] **T8. Tests de `UpdateContact` y `UpdateContactVisibility`.** RF-5, RF-6, CE-1, CE-2, CE-5, CE-11, CE-12
- Hecho cuando: `dotnet test` pasa con: 204 sin cuerpo, `Id` de ruta prevalece sobre el del cuerpo, solo viajan `Id`+`Visibility` en el endpoint de visibilidad, `KeyNotFoundException`→404, `Visibility` inválido→400, `FullName` inválido→400, 403 no admin y 401.

- [x] **T9. Tests de `GetContactsForSelect`, metadatos y autorización.** RF-1, RF-9, RF-10, CE-7, CE-8, CE-9, CE-14, CE-18
- Hecho cuando: `dotnet test` pasa con: 200 con proyección `Id`+`FullName`, colección vacía→200, 403 no admin y 401 en `/contactsct/`, `WithName`/`Produces` exactos en los 7 endpoints, 401 sin token y con token expirado en los 7, y 403 con rol no admin o `Role` ausente en los 4 endpoints admin sin ejecutar el caso de uso.

- [x] **T10. Tests de registro DI y verificación final.** RNF-3
- Hecho cuando: `ContactModuleRegistrationTests` valida registros `scoped`, ambas interfaces secundarias apuntando a la misma instancia de `ContactRepository`, container con `ValidateOnBuild`, y `dotnet build` + `dotnet test` terminan sin errores (0 fallos).
