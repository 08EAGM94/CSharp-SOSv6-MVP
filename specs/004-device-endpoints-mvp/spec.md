# Especificación — Caso de Uso: Equipo

- **ID de spec:** `004-device-endpoints-mvp`
- **Estado:** borrador
- **Constitución aplicable:** `docs/constitution.md`
- **Alcance de este documento:** QUÉ se construye y POR QUÉ. Las decisiones de implementación (mecanismo concreto de sesión, estructura del manejo de errores, estrategia de pruebas) corresponden al plan, no a esta spec.

---

## 1. Contexto y objetivo

El sistema SOS v6 administra **bitácoras** (órdenes de servicio registradas en campo). Cada **bitácora** referencia opcionalmente un **equipo** (`EquipoId` es opcional: una bitácora puede registrarse sin equipo) y un equipo tiene muchas bitácoras. Cada **equipo** pertenece obligatoriamente a un **tipo** (`TipoId` es obligatorio) y a una **empresa** (`EmpresaId` es obligatoria). Esa cadena —**Tipo → Equipo → Bitácora** y **Empresa → Equipo → Bitácora**— hace que el catálogo de equipos sea la entidad intermedia que vincula la clasificación (tipo) y el cliente (empresa) con las órdenes de servicio. Un equipo mal clasificado o deshabilitado indebidamente afecta a las bitácoras que lo usan y, a través de ellas, a la trazabilidad del servicio.

Esta spec define el **caso de uso Equipo**: la capacidad de registrar, consultar, listar (por empresa), actualizar y cambiar la visibilidad de los equipos.

El objetivo es que **cualquier usuario autenticado** pueda consultar el catálogo de equipos (incluido el listado reducido para *selects* de la interfaz al asignar un equipo a una bitácora) y **crear un equipo**, y que **únicamente un administrador autenticado** pueda **gobernar** dicho catálogo (modificar y habilitar/deshabilitar equipos mediante `UpdateDevices` y `UpdateDevicesVisibility`).

**Por qué este caso de uso va después de Tipo (002) y Empresa (003):** el control de acceso a los endpoints de Equipo depende de la sesión y el rol establecidos por el caso de uso Usuario (spec 001), y los equipos clasifican obligatoriamente a través de Tipo y pertenecen obligatoriamente a una Empresa.

---

## 2. Usuarios

| Actor | Descripción | Puede hacer |
|-------|-------------|-------------|
| **Administrador** | Usuario con rol `admin`. Único autorizado a gobernar el catálogo de equipos. | Iniciar sesión (vía caso de uso Usuario), crear, consultar, listar, actualizar y cambiar visibilidad de equipos. |
| **Usuario operativo** | Usuario con un rol distinto de `admin`. Existe, puede autenticarse y consultar el catálogo. | Iniciar sesión, consultar un equipo, listar equipos por empresa, listar equipos por empresa para selects. |
| **Consumidor de la API** | Cliente (aplicación o herramienta) que invoca los endpoints de esta spec. | Invocar los seis endpoints respetando el contrato de cada uno. |

---

## 3. Historias de usuario

### HU-1 — Gobernar el catálogo de equipos

**Como** administrador del sistema, **quiero** registrar, consultar, listar por empresa, actualizar y cambiar la visibilidad de los equipos, **para** mantener al día la información de los dispositivos que pueden ser el motivo de una bitácora (orden de servicio); un equipo mal clasificado o deshabilitado indebidamente impacta en las bitácoras vinculadas a través de la cadena Tipo → Equipo → Bitácora y Empresa → Equipo → Bitácora.

### HU-2 — Consultar el catálogo de equipos por empresa

**Como** usuario operativo del sistema, **quiero** consultar un equipo por su identificador, listar todos los equipos de una empresa y obtener la lista reducida para selects, **para** poder seleccionar correctamente el equipo al registrar una bitácora (orden de servicio), ya que la bitácora puede referenciar opcionalmente un equipo que pertenece a una empresa y el select de la interfaz se alimenta del endpoint reducido.

### HU-3 — Proteger el catálogo de equipos

**Como** administrador del sistema, **quiero** que las operaciones de escritura sobre el catálogo (**actualizar y cambiar visibilidad**) estén restringidas a usuarios con rol `admin`, **para** que ningún otro usuario pueda alterar la información de los equipos, lo cual repercute en las bitácoras vinculadas a esos equipos. La creación de equipos (`InsertDevice`) es accesible para cualquier usuario autenticado.

### HU-4 — Relación equipo-bitácora

**Como** usuario operativo, **quiero** que un equipo pueda ser el motivo por el cual se genera una bitácora (orden de servicio), **para** trazabilidad del servicio realizado sobre ese equipo; la otra opción es un servicio que no tiene nada que ver con un equipo descrito en una cadena de texto. Esta relación 1:n (Equipo → Bitácora) es parte del dominio y se refleja en `BinnacleEntity.DeviceId` (opcional).

---

## 4. Requisitos funcionales

Los criterios de aceptación usan notación EARS en español:

- **Evento:** "Cuando \<evento\>, el sistema \<respuesta\>."
- **Comportamiento no deseado:** "Si \<condición\>, entonces el sistema \<respuesta\>."
- **Estado:** "Mientras \<estado\>, el sistema \<respuesta\>."
- **Característica opcional:** "Donde \<característica\>, el sistema \<respuesta\>."

### Resumen de endpoints

| Método | Ruta | Nombre del endpoint (WithName) | Caso de uso (puerto primario) | Puerto secundario (repositorio) | DTO y propiedades usadas |
|--------|------|-------------------------------|------------------------------|--------------------------------|---------------------------|
| POST | `/device/` | `InsertDevice` | `IEnterpriseChildrenService<DeviceDTO>` | `IByEnterpriseRepository<DeviceEntity, DeviceDTO>` | `DeviceDTO` completo (`EnterpriseId`, `TypeId`, `Brand`, `Model`, `SerialNumber`, `InventoryNumber` obligatorios; `Visibility` se fija a ENABLED) |
| GET | `/device/{id}` | `GetDevice` | `IEnterpriseChildrenService<DeviceDTO>` | `IByEnterpriseRepository<DeviceEntity, DeviceDTO>` | DTO de entrada: solo `Id` (tomado del parámetro de ruta); **respuesta: `DeviceDTO` completo** |
| GET | `/devicesent/{enterpriseId}` | `GetDevicesByEnterprise` | `IEnterpriseChildrenService<DeviceDTO>` | `IByEnterpriseRepository<DeviceEntity, DeviceDTO>` | DTO de entrada: solo `EnterpriseId` (tomado del parámetro de ruta, valor int); **respuesta: `IEnumerable<DeviceDTO>` con todas las propiedades incluidas `Visibility`** |
| PUT | `/device/{id}` | `UpdateDevices` | `IEnterpriseChildrenService<DeviceDTO>` | `IByEnterpriseRepository<DeviceEntity, DeviceDTO>` | `DeviceDTO` con `Id` tomado del parámetro de ruta y resto de propiedades (`EnterpriseId`, `TypeId`, `Brand`, `Model`, `SerialNumber`, `InventoryNumber`; `Visibility` se gestiona en endpoint separado) |
| PUT | `/devicev/{id}` | `UpdateDevicesVisibility` | `IEnterpriseChildrenService<DeviceDTO>` | `IByEnterpriseRepository<DeviceEntity, DeviceDTO>` | `DeviceDTO` con `Id` de la ruta y `Visibility` |
| GET | `/devicesentsct/{enterpriseId}` | `GetDevicesByEnterpriseForSelect` | `IEnterpriseChildrenService<DeviceDTO>` | `IByEnterpriseRepository<DeviceEntity, DeviceDTO>` | DTO de entrada: solo `EnterpriseId` (tomado del parámetro de ruta, valor int); **respuesta: `IEnumerable<DeviceDTO>` con `Id`, `Brand`, `SerialNumber`; filtro por `Visibility = ENABLED` pero `Visibility` no viaja en la respuesta** |

### RF-1 — Control de acceso previo a toda operación protegida

Todos los endpoints de esta spec **exigen sesión válida**.

| # | Criterio de aceptación |
|---|------------------------|
| RF-1.1 | Cuando una petición se dirige a cualquiera de los seis endpoints, el sistema verificará primero que la petición porta una sesión válida. |
| RF-1.2 | Si una petición no porta sesión válida o su sesión ha expirado, entonces el sistema responderá con el código `401` y no ejecutará ninguna función del endpoint. |
| RF-1.3 | Donde exista una sesión válida, el sistema comprobará **antes de ejecutar cualquier otra acción** que el rol de esa sesión sea `admin` **solo para los endpoints `UpdateDevices` y `UpdateDevicesVisibility`**. Ese rol es el leído de los `claims` emitidos en el login (spec 001) y es la única fuente que decide si el flujo del endpoint continúa. |
| RF-1.4 | Si el rol de la sesión no es `admin` **y el endpoint es `UpdateDevices` o `UpdateDevicesVisibility`**, entonces el sistema responderá con el código `403` y no ejecutará ninguna función vital del endpoint. |
| RF-1.5 | Si la comparación del rol no puede realizarse por una sesión corrupta o manipulada, entonces el sistema responderá con el código `403` y no ejecutará ninguna función vital del endpoint. |
| RF-1.6 | Para los endpoints `InsertDevice`, `GetDevice` y `GetDevicesByEnterpriseForSelect`, **basta con una sesión válida (usuario autenticado)**; no se verifica el rol `admin`. |
| RF-1.7 | El endpoint `GetDevicesByEnterprise` exige rol `admin`; los endpoints `UpdateDevices` y `UpdateDevicesVisibility` también exigen rol `admin`. |
| RF-1.8 | Ningún endpoint de esta spec es público: todos exigen sesión válida. No existe equivalente a `login` en este caso de uso. |

### RF-2 — `InsertDevice` (POST `/device/`)

Registra un equipo nuevo. El caso de uso fija `Visibility = "ENABLED"` al crear.

| # | Criterio de aceptación |
|---|------------------------|
| RF-2.1 | Cuando un usuario autenticado invoque `POST /device/`, el sistema registrará un equipo a partir del DTO de equipo recibido. |
| RF-2.2 | El caso de uso (`EnterpriseChildrenService`) asigna `Visibility = "ENABLED"` al nuevo registro; el valor de `Visibility` enviado en el cuerpo (si lo hay) es ignorado. |
| RF-2.3 | Si el DTO no cumple las reglas de negocio del equipo (`EnterpriseId` > 0, `TypeId` > 0, `Brand` 2-50 chars, `Model` 5-100 chars, `SerialNumber` máx. 150 chars, `InventoryNumber` > 0), entonces el sistema responderá con el código `400` (`EntityException` → RF-8.2) y no creará el registro. |
| RF-2.4 | Cuando el registro se complete, el sistema responderá con el código `201` y sin cuerpo; el caso de uso `AddAsyncInfo` es `void` y no produce ningún objeto de retorno. |
| RF-2.5 | La creación no expone en la respuesta ninguna referencia al recurso creado: la respuesta se limita al código `201` y no incluye cuerpo ni cabecera `Location`. |
| RF-2.6 | Si el motor de persistencia genera un conflicto de unicidad, el sistema responderá con el código `500` y no creará un registro duplicado. |

### RF-3 — `GetDevice` (GET `/device/{id}`)

Obtiene un registro de equipo. **No filtra por visibilidad**: devuelve `200` con el registro aunque su `Visibility` sea `DISABLED` (opción A, coherente con specs 001, 002, 003). El `404` queda reservado exclusivamente a identificadores inexistentes.

| # | Criterio de aceptación |
|---|------------------------|
| RF-3.1 | Cuando un usuario autenticado invoque `GET /device/{id}`, el sistema sustituye el `Id` del `DeviceDTO` por el valor del parámetro de ruta. |
| RF-3.2 | Cuando el DTO tenga asignado el `Id` del parámetro de ruta, el sistema devolverá el registro de equipo correspondiente a ese identificador (`DeviceDTO` completo con todas sus propiedades). |
| RF-3.3 | Si el identificador no corresponde a ningún equipo, entonces el sistema responderá con el código `404`. |
| RF-3.4 | Si el `Id` de la ruta no es un valor numérico válido, entonces el sistema responderá con el código `400` y no consultará ningún registro. |
| RF-3.5 | Si el equipo existe pero tiene `Visibility = DISABLED`, el sistema devolverá `200` con el `DeviceDTO` completo; no responderá `404`. |

### RF-4 — `GetDevicesByEnterprise` (GET `/devicesent/{enterpriseId}`)

Obtiene **todos** los registros de equipos de una empresa, **incluyendo los deshabilitados**. Esta decisión es coherente con la consulta de listado del catálogo por empresa, que no aplica filtro de visibilidad: el listado completo sirve para gobernar el catálogo de dispositivos, de ahí que exista un endpoint dedicado (`GetDevicesByEnterpriseForSelect`) con solo las habilitadas para los selects. **Solo accesible para administrador.**

| # | Criterio de aceptación |
|---|------------------------|
| RF-4.1 | Cuando un administrador autenticado invoque `GET /devicesent/{enterpriseId}`, el sistema sustituye el `EnterpriseId` del `DeviceDTO` por el valor del parámetro de ruta (valor int). |
| RF-4.2 | Cuando el DTO tenga asignado el `EnterpriseId` del parámetro de ruta, el sistema devolverá el conjunto de **todos** los registros de equipos de esa empresa, independientemente de su `Visibility`. |
| RF-4.3 | El conjunto devuelto **incluirá** registros con `Visibility = DISABLED`, con independencia de su estado de visibilidad en el almacenamiento. |
| RF-4.4 | Si no existe ningún equipo registrado para esa empresa, entonces el sistema devolverá un conjunto vacío con el código `200`. |
| RF-4.5 | La respuesta contiene todas las propiedades del `DeviceDTO` incluyendo `Visibility`. |
| RF-4.6 | Si el `enterpriseId` de la ruta no es un valor numérico válido, entonces el sistema responderá con el código `400` y no consultará ningún registro. |
| RF-4.7 | Si la sesión no tiene rol `admin`, entonces el sistema responderá con el código `403` y no ejecutará ninguna función del endpoint. |

### RF-5 — `UpdateDevices` (PUT `/device/{id}`)

Actualiza un equipo. Solo accesible para administrador.

| # | Criterio de aceptación |
|---|------------------------|
| RF-5.1 | Cuando un administrador autenticado invoque `PUT /device/{id}`, el sistema sustituye el `Id` del `DeviceDTO` por el valor del parámetro de ruta, con independencia del `Id` que venga en el cuerpo de la petición. |
| RF-5.2 | Cuando el DTO tenga asignado el `Id` del parámetro de ruta, el sistema actualizará el equipo correspondiente a ese identificador con los datos del DTO (propiedades `EnterpriseId`, `TypeId`, `Brand`, `Model`, `SerialNumber`, `InventoryNumber`; `Visibility` se gestiona en endpoint separado). |
| RF-5.3 | Si el identificador no corresponde a ningún equipo, entonces el sistema responderá con el código `404` y no realizará ninguna modificación. |
| RF-5.4 | Si el DTO no cumple las reglas de negocio del equipo (`EnterpriseId` > 0, `TypeId` > 0, `Brand` 2-50 chars, `Model` 5-100 chars, `SerialNumber` máx. 150 chars, `InventoryNumber` > 0), entonces el sistema responderá con el código `400` (`EntityException` → RF-8.2) y no realizará ninguna modificación. |
| RF-5.5 | Cuando la actualización se complete, el sistema responderá con el código `204` y sin cuerpo: la operación de actualización (vía `EnterpriseChildrenService.UpdateAsyncChild`) no produce ningún objeto de retorno. |
| RF-5.6 | Si el `Id` de la ruta no es un valor numérico válido, entonces el sistema responderá con el código `400` y no realizará ninguna modificación. |

### RF-6 — `UpdateDevicesVisibility` (PUT `/devicev/{id}`)

Actualiza **únicamente** el campo `Visibility` de un equipo. Solo accesible para administrador.

| # | Criterio de aceptación |
|---|------------------------|
| RF-6.1 | Cuando un administrador autenticado invoque `PUT /devicev/{id}`, el sistema sustituye el `Id` del `DeviceDTO` por el valor del parámetro de ruta. |
| RF-6.2 | Cuando el endpoint actualice el campo `Visibility`, el sistema tomará del DTO **únicamente** las propiedades `Id` y `Visibility`; cualquier otra propiedad del DTO será ignorada. |
| RF-6.3 | Si el valor recibido en `Visibility` no es uno de los admitidos por las reglas de negocio (`ENABLED` o `DISABLED`), entonces el sistema responderá con el código `400` y no realizará ninguna modificación. |
| RF-6.4 | Si el identificador no corresponde a ningún equipo, entonces el sistema responderá con el código `404` y no realizará ninguna modificación. |
| RF-6.5 | Cuando la modificación se complete, el sistema responderá con el código `204` y sin cuerpo: la operación de actualización de visibilidad (vía `EnterpriseChildrenService.UpdateAsyncVisibility`) no produce ningún objeto de retorno. |
| RF-6.6 | Si el `Id` de la ruta no es un valor numérico válido, entonces el sistema responderá con el código `400` y no realizará ninguna modificación. |

### RF-7 — `GetDevicesByEnterpriseForSelect` (GET `/devicesentsct/{enterpriseId}`)

Obtiene los registros de equipos **habilitados únicamente** (`Visibility = ENABLED`) de una empresa, filtra por `Visibility = "ENABLED"` y devuelve `Id`, `Brand`, `SerialNumber`.

| # | Criterio de aceptación |
|---|------------------------|
| RF-7.1 | Cuando un usuario autenticado invoque `GET /devicesentsct/{enterpriseId}`, el sistema sustituye el `EnterpriseId` del `DeviceDTO` por el valor del parámetro de ruta (valor int). |
| RF-7.2 | Cuando el DTO tenga asignado el `EnterpriseId` del parámetro de ruta, el sistema devolverá el conjunto de registros de equipos de esa empresa con `Visibility = ENABLED`. |
| RF-7.3 | El conjunto devuelto **no incluirá** ningún registro con `Visibility = DISABLED`, con independencia de su existencia en el almacenamiento. |
| RF-7.4 | Si no existe ningún equipo habilitado registrado para esa empresa, entonces el sistema devolverá un conjunto vacío con el código `200`. |
| RF-7.5 | La respuesta contiene únicamente `Id`, `Brand` y `SerialNumber` por cada equipo. El filtro `Visibility = ENABLED` se aplica en la consulta, pero el campo `Visibility` **no** se incluye en la respuesta. Las propiedades no incluidas en este listado llegarán como `null` en el DTO y el consumidor no debe asumir que están presentes. |
| RF-7.6 | Si el `enterpriseId` de la ruta no es un valor numérico válido, entonces el sistema responderá con el código `400` y no consultará ningún registro. |

### RF-8 — Contrato de errores

Todo fallo de negocio producido por un caso de uso se traduce a un código HTTP y a un mensaje al usuario final.

| # | Criterio de aceptación |
|---|------------------------|
| RF-8.1 | Si un caso de uso falla por registro no encontrado (`KeyNotFoundException` del repositorio), entonces el sistema responderá con el código `404`. |
| RF-8.2 | Si un caso de uso falla por una violación de reglas de negocio de la entidad (`EntityException`), entonces el sistema responderá con el código `400`. |
| RF-8.3 | Si un caso de uso falla por una violación de reglas de negocio de la aplicación (`ApplicationException`, p. ej. `Visibility` inválido o `Id` < 1), entonces el sistema responderá con el código `400`. |
| RF-8.4 | Si un caso de uso falla por cualquier otra causa de negocio, entonces el sistema responderá con el código `400`. |
| RF-8.5 | Si un caso de uso falla por un conflicto de unicidad emitido por el motor de persistencia, entonces el sistema responderá con el código `500`. |
| RF-8.6 | Donde la API produzca un mensaje de error, el mensaje será redactado en español. |

### RF-9 — Documentación del contrato (OpenAPI / Swagger)

Los seis endpoints deben exponer su contrato de respuestas documentado y su nombre público.

| # | Criterio de aceptación |
|---|------------------------|
| RF-9.1 | Cada endpoint usará `WithName(...)` con **exactamente** el nombre indicado en la tabla de la sección "Resumen de endpoints" (`InsertDevice`, `GetDevice`, `GetDevicesByEnterprise`, `UpdateDevices`, `UpdateDevicesVisibility`, `GetDevicesByEnterpriseForSelect`). |
| RF-9.2 | Cada endpoint usará `Produces(...)` para documentar **cada** código de respuesta que pueda emitir según sus RF. La tabla siguiente lista los códigos **exactos** que cada endpoint debe documentar, deducidos de RF-1 a RF-8. |
| RF-9.3 | Los nombres y códigos documentados coincidirán con los definidos en los RF-2 a RF-7 y RF-1. |

#### Tabla de códigos `Produces(...)` por endpoint (verificable)

| Endpoint | WithName(...) | Códigos Produces(...) |
|----------|---------------|----------------------|
| `InsertDevice` | `InsertDevice` | `201`, `400`, `401`, `500` |
| `GetDevice` | `GetDevice` | `200`, `400`, `401`, `404` |
| `GetDevicesByEnterprise` | `GetDevicesByEnterprise` | `200`, `400`, `401`, `403` |
| `UpdateDevices` | `UpdateDevices` | `204`, `400`, `401`, `403`, `404` |
| `UpdateDevicesVisibility` | `UpdateDevicesVisibility` | `204`, `400`, `401`, `403`, `404` |
| `GetDevicesByEnterpriseForSelect` | `GetDevicesByEnterpriseForSelect` | `200`, `400`, `401` |

**Derivación:**
- `201/204/200` son los códigos de éxito de RF-2.4, RF-5.5, RF-6.5, RF-3.2, RF-4.2, RF-7.2.
- `401` aplica a **todos** por RF-1.2.
- `403` aplica a `GetDevicesByEnterprise`, `UpdateDevices` y `UpdateDevicesVisibility` por RF-1.7 y RF-4.7.
- `400` aplica a todos los que validan `Id`/`enterpriseId` numérico (RF-3.4, RF-4.6, RF-5.6, RF-6.6, RF-7.6), reglas de negocio de entidad (RF-2.3, RF-5.4), `Visibility` (RF-6.3) o DTO inválido (RF-8.2/8.3).
- `404` aplica a los que buscan por `Id` (RF-3.3, RF-5.3, RF-6.4).
- `500` aplica solo a `InsertDevice` por conflicto de unicidad (RF-2.6).

---

## 5. Requisitos no funcionales

| # | Requisito | Criterio de aceptación |
|---|-----------|------------------------|
| RNF-1 | Esquema por capas | La especificación respeta la separación entre dominio, aplicación, datos y repositorio establecida en `docs/constitution.md`. |
| RNF-2 | Puertos y adaptadores | Los casos de uso se comunican con la persistencia únicamente a través de los puertos de la capa de aplicación; ningún caso de uso accede directamente al mecanismo de almacenamiento. |
| RNF-3 | Inyección de dependencias | Los componentes se registran con ámbito de vida por petición (`scoped`). |
| RNF-4 | Plataforma | El sistema opera sobre .NET 10.0 usando únicamente biblioteca estándar. |
| RNF-5 | Persistencia | La persistencia de equipos se realiza mediante Entity Framework Core. |
| RNF-6 | Idioma | Los identificadores y los comentarios del código están en inglés; los mensajes destinados al usuario final están en español. |
| RNF-7 | Consistencia de datos | Cuando una operación de escritura se complete, el estado almacenado debe corresponder a los datos enviados por el consumidor de la API. |
| RNF-8 | Ausencia de filtración de secretos | Ninguna respuesta de la API debe exponer datos sensibles. |
| RNF-9 | Compatibilidad | El contrato de los seis endpoints no cambia de forma incompatible dentro de este caso de uso. |
| RNF-10 | Sin dependencias nuevas | No se añade ningún paquete NuGet fuera de los ya autorizados en la spec 001 (solo `Microsoft.AspNetCore.Authentication.JwtBearer` y EF Core). |

---

## 6. Casos límite

| # | Situación | Comportamiento esperado |
|---|-----------|------------------------|
| CE-1 | El `Id` del cuerpo de `PUT /device/{id}` o `PUT /devicev/{id}` difiere del `Id` de la ruta. | Prevalece siempre el `Id` de la ruta. |
| CE-2 | `PUT /devicev/{id}` incluye propiedades ajenas a `Id` y `Visibility` (p. ej. `Brand`, `Model`). | Esas propiedades se ignoran y no producen efecto sobre el equipo. |
| CE-3 | Se solicita un equipo inexistente (`GetDevice`, `UpdateDevices`, `UpdateDevicesVisibility`). | `404`. |
| CE-4 | Se intenta crear un equipo cuyo índice único provoque conflicto en el motor de persistencia. | `500` y no se duplica el registro. |
| CE-5 | Se intenta actualizar un equipo inexistente. | `404` y no se realiza ninguna modificación. |
| CE-6 | Se solicitan equipos de una empresa y no hay ninguno registrado. | `200` con conjunto vacío. |
| CE-6b | Existe al menos un equipo deshabilitado y se invoca `GET /devicesent/{enterpriseId}`. | El conjunto devuelto **incluye** el registro con `Visibility = DISABLED` (RF-4.3). |
| CE-6c | Existe al menos un equipo deshabilitado y se invoca `GET /devicesentsct/{enterpriseId}`. | El conjunto devuelto **no incluye** el registro con `Visibility = DISABLED` (RF-7.3). |
| CE-7 | Se invocan los seis endpoints sin sesión o con sesión expirada. | `401` y no se ejecuta ninguna función del endpoint. |
| CE-8 | Se invocan `GetDevicesByEnterprise`, `UpdateDevices` o `UpdateDevicesVisibility` con una sesión de rol distinto de `admin`. | `403` y no se ejecuta ninguna función del endpoint. |
| CE-9 | Se invocan `GetDevicesByEnterprise`, `UpdateDevices` o `UpdateDevicesVisibility` con rol `admin`, pero la sesión está manipulada. | `403`. |
| CE-10 | `GetDevice` devuelve un equipo con `Visibility = DISABLED`. | `200` con el equipo (no se filtra por visibilidad en consulta individual; decisión del usuario, opción A). |
| CE-11 | El DTO de `InsertDevice` o `UpdateDevices` viola una regla de negocio (longitudes de campos, IDs > 0). | `400` y no se escribe ningún dato. |
| CE-12 | El DTO de `UpdateDevicesVisibility` envía `Visibility` distinto de `ENABLED` o `DISABLED`. | `400` y no se realiza ninguna modificación. |
| CE-13 | Cualquiera de las rutas recibe un `Id` o `enterpriseId` no numérico. | `400` y no se ejecuta ninguna función del endpoint. |
| CE-14 | La sesión del consumidor ha vencido su vigencia (30 min desde emisión, sin renovación). | `401` en los seis endpoints. |
| CE-15 | Se emite un error en cualquiera de los seis endpoints. | El mensaje al usuario se entrega en español. |

---

## 7. Fuera de alcance

- Persistencia de las órdenes de servicio (bitácoras) y de cualquier entidad distinta del equipo.
- Edición del contenido de la orden de servicio.
- Asignación de órdenes de servicio a equipos (la bitácora referencia opcionalmente un equipo, pero esa lógica corresponde al caso de uso Bitácora).
- Recuperación, restablecimiento o envío de contraseñas por correo.
- Esquema de autorización distinto del binario administrador / no administrador.
- Autenticación por proveedores externos, correo, SMS o segundo factor.
- Auditoría de los cambios realizados sobre los equipos.
- Versionado y publicación del contrato de la API más allá de RNF-9.
- Pruebas de rendimiento, de carga y de penetración.
- Modificación de `hexArch/repository` (prohibido por el principio 5 de la Constitución).

---

## 8. Criterios de finalización

El caso de uso Equipo se considera concluido cuando:

1. Los seis endpoints de la sección 4 están disponibles y responden conforme a su criterio de aceptación.
2. Los seis endpoints rechazan con `401` las peticiones sin sesión válida o con sesión vencida.
3. `GetDevicesByEnterprise`, `UpdateDevices` y `UpdateDevicesVisibility` rechazan con `403` las peticiones cuyo `Role` de los `claims` no sea `admin`, sin ejecutar ninguna función del endpoint y sin consultar `Visibility` ni el almacén.
4. `InsertDevice`, `GetDevice` y `GetDevicesByEnterpriseForSelect` aceptan usuario autenticado (no exigen `admin`).
5. Un `Id` o `enterpriseId` de ruta no numérico se rechaza con `400` en todos los endpoints que lo reciben.
6. El `Id` del parámetro de ruta prevalece sobre el `Id` del cuerpo en `GetDevice`, `UpdateDevices` y `UpdateDevicesVisibility`.
7. `UpdateDevicesVisibility` aplica únicamente el campo `Visibility` e ignora cualquier otra propiedad del DTO recibido.
8. `GetDevicesByEnterprise` devuelve **todos** los registros (incluye `Visibility = DISABLED`).
9. `GetDevicesByEnterpriseForSelect` devuelve **solo** registros con `Visibility = ENABLED`.
10. `GetDevice` devuelve el registro aunque tenga `Visibility = DISABLED` (no filtra en consulta individual; `404` solo para ID inexistente; decisión del usuario, opción A).
11. Los orígenes de fallo de la sección 4 se traducen a `404`, `400` o `500` según corresponda, con mensajes en español.
12. `InsertDevice` responde `201` sin cuerpo ni cabecera `Location`; `UpdateDevices` y `UpdateDevicesVisibility` responden `204` sin cuerpo; `GetDevice`, `GetDevicesByEnterprise` y `GetDevicesByEnterpriseForSelect` responden `200` con el DTO o colección correspondiente.
13. Cada endpoint cuenta con `WithName(...)` usando exactamente los nombres de la tabla de resumen y `Produces(...)` para cada código de respuesta posible.
14. El proyecto compila sin errores ni advertencias de código o de analizadores, y todas las pruebas pasan (`dotnet test` verde).
15. Cada endpoint cuenta con su prueba automatizada y con la prueba de su caso límite y su caso de error, según `docs/constitution.md`; cada prueba crea sus propios datos.

### Deuda técnica conocida

- La compilación reporta avisos `NU1903` de auditoría NuGet sobre `System.Security.Cryptography.Xml` 9.0.0, dependencia transitiva de `Microsoft.EntityFrameworkCore.SqlServer` → `Microsoft.Data.SqlClient` → `System.Security.Cryptography.ProtectedData`. Son preexistentes a este caso de uso y ajenos a RF-1 a RF-9, por lo que no forman parte del criterio 14. Su eliminación exige actualizar el paquete (prohibido por RNF-10 sin spec previa) o desactivar la auditoría NuGet, y corresponde a una spec propia.

---

## 9. Dudas abiertas

- Ninguna.