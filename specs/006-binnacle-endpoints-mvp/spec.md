# Especificación — Caso de Uso: Bitácora

- **ID de spec:** `006-binnacle-endpoints-mvp`
- **Estado:** implementada
- **Constitución aplicable:** `docs/constitution.md`
- **Alcance de este documento:** QUÉ se construye y POR QUÉ. Las decisiones de implementación (mecanismo concreto de sesión, estructura del manejo de errores, estrategia de pruebas) corresponden al plan, no a esta spec.

---

## 1. Contexto y objetivo

El sistema SOS v6 administra **bitácoras**, que corresponden al concepto de **orden de servicio**: una bitácora es una especie de **detalle-maestro** donde se muestra la información de una orden solicitada por un cliente. Cada bitácora tiene una relación **n:1 con Usuario** (muchas órdenes pertenecen a un usuario operativo que las atiende), una relación **n:1 con Contacto** (la persona de referencia del cliente, a través de la cual se encadena con su empresa) y una relación **n:1 con Equipo**, la cual es **opcional** (una orden puede originarse por un equipo determinado o por un servicio que no tiene que ver con un equipo, descrito en una cadena de texto).

Una bitácora tiene **4 estatus**: **en proceso**, **falta confirmar**, **cancelado** y **finalizado**. Dependiendo del estatus, la orden se muestra de forma **parcial o total** (por ejemplo, una orden en proceso no exponen los mismos datos que una finalizada); la forma concreta de mostrar esa información se trabajará en capas más externas del MVP y queda **fuera del alcance** de esta spec (ver §7). El estatus, sin embargo, gobierna qué operaciones son posibles: el seguimiento parcial pasa la orden a *falta confirmar*, el reinicio de actividades la devuelve a *en proceso*, la cancelación la lleva a *cancelado* y el cierre la lleva a *finalizado*.

Esta spec define el **caso de uso Bitácora** (`BinnacleService`): los diez endpoints para registrar, consultar, listar (seguimiento y reporte), actualizar, cambiar la visibilidad, dar seguimiento parcial, reiniciar actividades, cancelar y finalizar bitácoras.

El objetivo es que **cualquier usuario autenticado** pueda registrar órdenes (`InsertBinnacle`), consultar sus órdenes (`GetBinnacle`), obtener su lista de seguimiento (`FollowupList`) y operar el ciclo de vida de sus órdenes (`FollowupPartial`, `ResetActivities`, `CancelBinnacle`, `FinishBinnacle`), y que **únicamente un administrador autenticado** pueda **gobernar** las órdenes del sistema (`UpdateBinnacle`, `UpdateBinnacleVisibility`, `BinnaclesReport`).

**Por qué este caso de uso va después de las specs 001–005:** el control de acceso depende de la sesión y el rol establecidos por el caso de uso Usuario (spec 001); toda bitácora referencia un usuario (spec 001), un contacto obligatorio de una empresa (specs 003 y 005) y, de forma opcional, un equipo de una empresa (specs 002 y 004); la bitácora es la orden de servicio que completa la cadena Empresa → Contacto → Bitácora descrita en esas specs.

---

## 2. Usuarios

| Actor | Descripción | Puede hacer |
|-------|-------------|-------------|
| **Administrador** | Usuario con rol `admin`. Único autorizado a gobernar las órdenes del sistema. | Iniciar sesión (vía caso de uso Usuario), actualizar bitácoras, cambiar su visibilidad y obtener reportes de bitácoras; además, todas las acciones del usuario operativo. |
| **Usuario operativo** | Usuario con un rol distinto de `admin`. Ejecuta las órdenes de servicio en campo. | Iniciar sesión, registrar una bitácora, consultarla, obtener su lista de seguimiento, registrar el seguimiento parcial, reiniciar actividades, cancelar y finalizar órdenes. |
| **Consumidor de la API** | Cliente (aplicación o herramienta) que invoca los endpoints de esta spec. | Invocar los diez endpoints respetando el contrato de cada uno. |

---

## 3. Historias de usuario

### HU-1 — Registrar y atender órdenes de servicio en campo

**Como** usuario operativo del sistema, **quiero** registrar una bitácora (orden de servicio) asociada a un contacto cliente —y, opcionalmente, a un equipo—, consultarla, obtener mi lista de seguimiento y operar su ciclo de vida (seguimiento parcial, reinicio de actividades, cancelación y finalización), **para** mantener al día el detalle-maestro de las órdenes de mis clientes: cada orden nace *en proceso*, avanza a *falta confirmar* cuando registro el seguimiento, puede volver a *en proceso* al reiniciar sus actividades, y termina *cancelada* o *finalizada*; una orden mal registrada o sin cerrar impacta directamente en la información del cliente (Empresa → Contacto → Bitácora).

### HU-2 — Gobernar las órdenes de servicio del sistema

**Como** administrador del sistema, **quiero** actualizar una bitácora, cambiar su visibilidad y obtener un reporte paginado de bitácoras filtrado, **para** corregir, ocultar o auditar las órdenes de servicio de todo el sistema; una bitácora mal informada o deshabilitada indebidamente afecta la visión de la orden de un cliente, cuya información depende de la cadena Empresa → Contacto → Bitácora y del usuario asignado.

### HU-3 — Proteger las órdenes de servicio

**Como** administrador del sistema, **quiero** que las operaciones de **actualización, cambio de visibilidad y reporte** de bitácoras estén restringidas a usuarios con rol `admin`, **para** que ningún otro usuario pueda alterar u obtener el reporte global de las órdenes del sistema. El registro, la consulta, el listado de seguimiento y las operaciones de ciclo de vida son accesibles para cualquier usuario autenticado, pues responden al trabajo diario del usuario operativo con sus propias órdenes.

### HU-4 — Consultar una orden según su estatus

**Como** usuario del sistema, **quiero** que, al consultar una bitácora, el sistema exponga la información de la orden (datos del cliente a través del contacto y su empresa, usuario asignado, servicio, fechas, estatus, firma del cliente y, cuando exista, los datos del equipo), **para** ver el detalle-maestro de la orden de un cliente; el estatus de la orden (en proceso, falta confirmar, cancelado o finalizado) determina qué parte de esa información se mostrará de forma parcial o total en la interfaz (la presentación concreta se resuelve en capas más externas del MVP).

---

## 4. Requisitos funcionales

Los criterios de aceptación usan notación EARS en español:

- **Evento:** "Cuando \<evento\>, el sistema \<respuesta\>."
- **Comportamiento no deseado:** "Si \<condición\>, entonces el sistema \<respuesta\>."
- **Estado:** "Mientras \<estado\>, el sistema \<respuesta\>."
- **Característica opcional:** "Donde \<característica\>, el sistema \<respuesta\>."

### Definiciones

- **Función vital:** la invocación del método del caso de uso asociado al endpoint. "No ejecutar ninguna función vital del endpoint" significa no invocar ese método.
- **Designación de acción:** constante de texto (`"FollowupList"` o `"BinnaclesReport"`) que identifica cada listado y que se pasa como argumento del parámetro `controllerAction` del método `GetAsyncAllInfo` del repositorio.
- **Identificador de usuario legible:** el `claim` `Id` de la sesión existe y su valor es numérico (`ReadUserIdClaim`); si el `claim` no existe o no es numérico, la sesión no porta identificador de usuario legible.

### Resumen de endpoints

| Método | Ruta | Nombre del endpoint (WithName) | Caso de uso (puerto primario) | Puerto secundario (repositorio) | DTO y propiedades usadas |
|--------|------|-------------------------------|-------------------------------|--------------------------------|---------------------------|
| POST | `/binnacle/` | `InsertBinnacle` | `IBinnacleService.AddAsyncInfo` | `IBinnacleRepository` | `BinnacleDTO` recibido en el cuerpo (`ContactId` obligatorio; `UserId` lo fija la sesión con `ReadUserIdClaim`; estatus, visibilidad, fecha de inicio, actividades y observaciones los fija el sistema) |
| GET | `/binnacle/{id}` | `GetBinnacle` | `IBinnacleService.GetAsyncInfo` | `IBinnacleRepository` | DTO generado: `Id` del parámetro de ruta; `UserId` = id de la sesión (`ReadUserIdClaim`); sin cuerpo de petición |
| GET | `/binnaclesfu/{page}/{elemsKey}` | `FollowupList` | `IBinnacleService.GetAsyncAllInfo` | `IBinnacleRepository` | DTO generado con **solo** `UserId` = id de la sesión (`ReadUserIdClaim`); constante `"FollowupList"` como argumento de `controllerAction`; `binnFilter` = null |
| POST | `/binnaclesr/` | `BinnaclesReport` | `IBinnacleService.GetAsyncAllInfo` | `IBinnacleRepository` | Wrapper de petición con `page`, `elemsKey` y `binnFilter` (`Dictionary<string, string>`); constante `"BinnaclesReport"` como argumento de `controllerAction`; DTO = null |
| PUT | `/binnacle/{id}` | `UpdateBinnacle` | `IBinnacleService.UpdateAsyncInfo` | `IBinnacleRepository` | `BinnacleDTO` del cuerpo con `Id` sustituido por el de la ruta y `Status` obligatorio |
| PUT | `/binnaclev/{id}` | `UpdateBinnacleVisibility` | `IBinnacleService.UpdateAsyncVisibility` | `IBinnacleRepository` | `BinnacleDTO` con `Id` de la ruta y `Visibility`; el resto de propiedades se ignoran |
| PUT | `/binnaclefup/{id}` | `FollowupPartial` | `IBinnacleService.FollowupPartialAsync` | `IBinnacleRepository` | `BinnacleDTO` del cuerpo con `Id` = ruta y `UserId` = id de la sesión (`ReadUserIdClaim`) |
| PUT | `/binnaclera/{id}` | `ResetActivities` | `IBinnacleService.ResetActivitiesAsync` | `IBinnacleRepository` | DTO generado: `Id` = ruta y `UserId` = id de la sesión (`ReadUserIdClaim`); **no tiene cuerpo** |
| PUT | `/binnaclecb/{id}` | `CancelBinnacle` | `IBinnacleService.CancelBinnacleAsync` | `IBinnacleRepository` | `BinnacleDTO` del cuerpo con `Id` = ruta y `UserId` = id de la sesión (`ReadUserIdClaim`); actualiza las observaciones (accessor `CancelDesc`, considerado en vez de `Hints` en el mapper a entidad) |
| PUT | `/binnaclefsh/{id}` | `FinishBinnacle` | `IBinnacleService.FinishBinnacleAsync` | `IBinnacleRepository` | `BinnacleDTO` del cuerpo con `Id` = ruta y `UserId` = id de la sesión (`ReadUserIdClaim`); actualiza `CustomerSignature` (firma del cliente) |

### RF-1 — Control de acceso previo a toda operación protegida

Todos los endpoints de esta spec **exigen sesión válida**.

| # | Criterio de aceptación |
|---|------------------------|
| RF-1.1 | Cuando una petición se dirige a cualquiera de los diez endpoints, el sistema verificará primero que la petición porta una sesión válida. |
| RF-1.2 | Si una petición no porta sesión válida o su sesión ha expirado (vigencia de 30 minutos desde la emisión), entonces el sistema responderá con el código `401` y no ejecutará ninguna función del endpoint. |
| RF-1.3 | Donde exista una sesión válida, el sistema comprobará **antes de ejecutar cualquier otra acción** que el rol de esa sesión sea `admin` **solo para los endpoints `BinnaclesReport`, `UpdateBinnacle` y `UpdateBinnacleVisibility`**. Ese rol es el leído de los `claims` emitidos en el login (spec 001) y es la única fuente que decide si el flujo del endpoint continúa. |
| RF-1.4 | Si el rol de la sesión no es `admin` **y el endpoint es `BinnaclesReport`, `UpdateBinnacle` o `UpdateBinnacleVisibility`**, entonces el sistema responderá con el código `403` y no ejecutará ninguna función vital del endpoint. |
| RF-1.5 | Si la sesión existe pero el rol no puede validarse, entonces el sistema responderá con el código `403` y no ejecutará ninguna función vital del endpoint. Criterios: (a) rol ausente: `ctx.User.FindFirst("role")` devuelve nulo, lo que indica que el emisor del token no incluyó el rol; (b) rol alterado: el valor leído no pertenece a la lista de roles válidos (`admin`, `user`). Un token con la firma alterada **no llega a constituir una sesión**: falla la validación de firma, la petición queda sin sesión autenticada y se rige por RF-1.2 con código `401`, igual que la caducidad. |
| RF-1.6 | Para los endpoints `InsertBinnacle`, `GetBinnacle`, `FollowupList`, `FollowupPartial`, `ResetActivities`, `CancelBinnacle` y `FinishBinnacle`, **basta con una sesión válida (usuario autenticado)**; no se verifica el rol `admin`. |
| RF-1.7 | Cuando `InsertBinnacle`, `GetBinnacle`, `FollowupList`, `FollowupPartial`, `ResetActivities`, `CancelBinnacle` o `FinishBinnacle` necesiten el identificador del usuario de la sesión y la sesión no porte un identificador de usuario legible en sus `claims`, entonces el sistema responderá con el código `401` y no ejecutará ninguna función del endpoint. |
| RF-1.8 | Ningún endpoint de esta spec es público: todos exigen sesión válida. No existe equivalente a `login` en este caso de uso. |

### RF-2 — `InsertBinnacle` (POST `/binnacle/`)

Registra una bitácora (orden de servicio) a partir del DTO recibido.

| # | Criterio de aceptación |
|---|------------------------|
| RF-2.1 | Cuando un usuario autenticado invoque `POST /binnacle/`, el sistema registrará una bitácora a partir del `BinnacleDTO` recibido en el cuerpo, asociándola al contacto (`ContactId`), al equipo (`DeviceId`, si se envía) y al usuario de la sesión: el accessor `UserId` del DTO se sustituye por el identificador del usuario de la sesión (`ReadUserIdClaim`), con independencia del `UserId` que venga en el cuerpo (se ignora). |
| RF-2.2 | El registro nace con estatus **"en proceso"**, visibilidad **"ENABLED"**, fecha de inicio igual a la fecha actual y actividades realizadas y observaciones vacías (`null`), con independencia de los valores de estatus, visibilidad, fecha de inicio, actividades u observaciones enviados en el cuerpo (se ignoran). |
| RF-2.3 | Si el DTO no cumple las reglas de negocio aplicables a `ContactId` (mayor que cero), `Service` (al menos 15 caracteres), `Amount` (mayor que cero) o `CustomerSignature` (máximo 255 caracteres), entonces el sistema responderá con el código `400` (`EntityException` → RF-12.2) y no creará el registro. |
| RF-2.4 | Si el `ContactId` del DTO o el `UserId` de la sesión no corresponden a registros existentes, entonces el sistema responderá con el código `500` y no creará el registro (violación de integridad referencial emitida por el motor de persistencia). |
| RF-2.5 | Cuando el registro se complete, el sistema responderá con el código `201` y sin cuerpo; el caso de uso de alta no produce ningún objeto de retorno. |
| RF-2.6 | La creación no expone en la respuesta ninguna referencia al recurso creado: la respuesta se limita al código `201` y no incluye cuerpo ni cabecera `Location`. |

### RF-3 — `GetBinnacle` (GET `/binnacle/{id}`)

Obtiene una bitácora de la orden del cliente perteneciente al usuario de la sesión. El parámetro `id` es obligatorio.

| # | Criterio de aceptación |
|---|------------------------|
| RF-3.1 | Cuando un usuario autenticado invoque `GET /binnacle/{id}`, el sistema generará un `BinnacleDTO` (la petición no tiene cuerpo) y asignará a su `Id` el valor del parámetro de ruta `id`. |
| RF-3.2 | El sistema asignará al `UserId` del DTO el identificador del usuario de la sesión leído de sus `claims` (`ReadUserIdClaim`); si la sesión no porta un identificador de usuario legible, entonces el sistema responderá con el código `401` (RF-1.7) y no ejecutará ninguna función del endpoint. |
| RF-3.3 | Cuando el DTO tenga asignados sus valores, el sistema devolverá con el código `200` la bitácora que coincida con ese `Id` y con el `UserId` de la sesión, con la información de la orden: servicio, monto, actividades, observaciones, fechas, estatus, firma del cliente, el usuario asignado, los datos del cliente (contacto y su empresa) y, **solo si la bitácora tiene equipo**, los datos del equipo (marca, modelo, número de serie, número de inventario y tipo). |
| RF-3.4 | Si no existe ninguna bitácora que coincida a la vez con ese `Id` y el `UserId` de la sesión, entonces el sistema responderá con el código `404`. |
| RF-3.5 | Si `id` no es un valor numérico válido, o si siendo numérico es cero o negativo, entonces el sistema responderá con el código `400` (valores numéricos fuera de rango los rechaza la entidad, `EntityException` → RF-12.2) y no consultará ningún registro. |

### RF-4 — `FollowupList` (GET `/binnaclesfu/{page}/{elemsKey}`)

Obtiene la lista paginada de seguimiento del usuario de la sesión: sus bitácoras en estatus **"en proceso"** o **"falta confirmar"**. El sistema identifica este listado con la constante **`"FollowupList"`**, pasada como argumento del parámetro `controllerAction` de `GetAsyncAllInfo`.

| # | Criterio de aceptación |
|---|------------------------|
| RF-4.1 | Cuando un usuario autenticado invoque `GET /binnaclesfu/{page}/{elemsKey}`, el sistema generará un `BinnacleDTO` (la petición no tiene cuerpo) y asignará a **solo** su `UserId` el identificador del usuario de la sesión leído de sus `claims`; ningún otro accessor del DTO se modifica. |
| RF-4.2 | Si la sesión no porta un identificador de usuario legible, entonces el sistema responderá con el código `401` (RF-1.7) y no ejecutará ninguna función del endpoint. |
| RF-4.3 | `page` y `elemsKey` deben ser valores enteros **mayores que cero**; si alguno no es numérico o siendo numérico es cero o negativo, entonces el sistema responderá con el código `400` y no consultará ningún registro. |
| RF-4.4 | Cuando los parámetros sean válidos, el sistema invocará el método de listado `GetAsyncAllInfo` con el DTO, `page`, `elemsKey`, la constante `"FollowupList"` como argumento del parámetro `controllerAction` y `binnFilter` sin filtrar (`null`), y responderá con el código `200` y un objeto `PaginationResult`: elementos de la página, página actual, total de páginas y total de registros. |
| RF-4.5 | El listado **solo** contiene bitácoras del usuario de la sesión cuyo estatus es "en proceso" o "falta confirmar"; el resto de estatus ("cancelado", "finalizado") no aparece. |
| RF-4.6 | Si el usuario no tiene bitácoras en seguimiento, **o si `page` excede el total de páginas**, entonces el sistema devolverá el código `200` con un resultado paginado de elementos vacíos (sin error que gestionar). |

### RF-5 — `BinnaclesReport` (POST `/binnaclesr/`)

Obtiene un reporte paginado de bitácoras de todo el sistema según un filtro. **Solo accesible para administrador.** El sistema identifica este listado con la constante **`"BinnaclesReport"`**, pasada como argumento del parámetro `controllerAction` de `GetAsyncAllInfo`.

| # | Criterio de aceptación |
|---|------------------------|
| RF-5.1 | Cuando un administrador autenticado invoque `POST /binnaclesr/`, el sistema leerá de la petición un wrapper con `page` (entero), `elemsKey` (entero) y `binnFilter` (pares clave-valor de texto). |
| RF-5.2 | `page` y `elemsKey` deben ser valores enteros **mayores que cero**; si alguno no es numérico o siendo numérico es cero o negativo, entonces el sistema responderá con el código `400` y no consultará ningún registro. |
| RF-5.3 | Cuando los parámetros sean válidos, el sistema invocará el método de listado `GetAsyncAllInfo` con `page`, `elemsKey`, `binnFilter`, la constante `"BinnaclesReport"` como argumento del parámetro `controllerAction` y **sin DTO** (nulo), y responderá con el código `200` y un objeto `PaginationResult`: elementos de la página, página actual, total de páginas y total de registros; los elementos contienen, por cada bitácora, su identificador, visibilidad, el usuario asignado, el contacto con su empresa. |
| RF-5.4 | El listado se obtiene aplicando el filtro recibido en `binnFilter` sobre el conjunto de bitácoras del sistema; la existencia y el formato de las claves del filtro los aseguran capas más externas del MVP (ver §7), y el filtro incluye la visibilidad solicitada, que se aplica en el paginado (RF-7.8). |
| RF-5.5 | Si la sesión no tiene rol `admin`, entonces el sistema responderá con el código `403` y no ejecutará ninguna función del endpoint. |
| RF-5.6 | Si no hay bitácoras que cumplan el filtro, **o si `page` excede el total de páginas**, entonces el sistema devolverá el código `200` con un resultado paginado de elementos vacíos (sin error que gestionar). |

### RF-6 — `UpdateBinnacle` (PUT `/binnacle/{id}`)

Actualiza una bitácora. **Solo accesible para administrador.**

| # | Criterio de aceptación |
|---|------------------------|
| RF-6.1 | Cuando un administrador autenticado invoque `PUT /binnacle/{id}`, el sistema sustituye el `Id` del `BinnacleDTO` por el valor del parámetro de ruta, con independencia del `Id` que venga en el cuerpo de la petición. |
| RF-6.2 | Cuando el DTO tenga asignado el `Id` de la ruta, el sistema actualizará la bitácora correspondiente; el valor del accessor `Status` del DTO (en proceso, falta confirmar, cancelado o finalizado) determina qué campos se actualizan (actualización dinámica según el estatus; la interfaz que muestra esos campos según el estatus se resuelve en capas más externas del MVP, ver §7). |
| RF-6.3 | Si el `Status` del DTO es nulo o no es ninguno de los 4 estatus admitidos ("en proceso", "falta confirmar", "cancelado", "finalizado"), entonces el sistema responderá con el código `400` (validación del caso de uso → `ApplicationException`, RF-12.3) y no realizará ninguna modificación. |
| RF-6.4 | Si el identificador no corresponde a ninguna bitácora, entonces el sistema responderá con el código `404` y no realizará ninguna modificación. |
| RF-6.5 | Si el `Id` de la ruta no es un valor numérico válido, o si siendo numérico es cero o negativo, entonces el sistema responderá con el código `400` (`EntityException`/`ApplicationException` → RF-12) y no realizará ninguna modificación. |
| RF-6.6 | Cuando la actualización se complete, el sistema responderá con el código `204` y sin cuerpo; la operación de actualización no produce ningún objeto de retorno. |
| RF-6.7 | Si la sesión no tiene rol `admin`, entonces el sistema responderá con el código `403` y no ejecutará ninguna función del endpoint. |

### RF-7 — `UpdateBinnacleVisibility` (PUT `/binnaclev/{id}`)

Actualiza **únicamente** el campo `Visibility` de una bitácora. **Solo accesible para administrador.**

| # | Criterio de aceptación |
|---|------------------------|
| RF-7.1 | Cuando un administrador autenticado invoque `PUT /binnaclev/{id}`, el sistema sustituye el `Id` del `BinnacleDTO` por el valor del parámetro de ruta. |
| RF-7.2 | Cuando el endpoint actualice la visibilidad, el sistema tomará del DTO **únicamente** las propiedades `Id` y `Visibility`; cualquier otra propiedad del DTO será ignorada. |
| RF-7.3 | Si el valor recibido en `Visibility` no es uno de los admitidos por las reglas de negocio (`ENABLED` o `DISABLED`), entonces el sistema responderá con el código `400` y no realizará ninguna modificación (la validación de `Id` y `Visibility` ocurre en el caso de uso → `ApplicationException`, RF-12.3). |
| RF-7.4 | Si el identificador no corresponde a ninguna bitácora, entonces el sistema responderá con el código `404` y no realizará ninguna modificación. |
| RF-7.5 | Si el `Id` de la ruta no es un valor numérico válido, o si siendo numérico es cero o negativo, entonces el sistema responderá con el código `400` y no realizará ninguna modificación. |
| RF-7.6 | Cuando la modificación se complete, el sistema responderá con el código `204` y sin cuerpo. |
| RF-7.7 | Si la sesión no tiene rol `admin`, entonces el sistema responderá con el código `403` y no ejecutará ninguna función del endpoint. |
| RF-7.8 | Donde `BinnaclesReport` pagine el resultado, el sistema excluirá las bitácoras cuya visibilidad no coincida con la solicitada en `binnFilter`, tanto en los elementos de la página como en el total de registros y de páginas; el efecto de la visibilidad se limita a este listado y no afecta a los demás endpoints. |

### RF-8 — `FollowupPartial` (PUT `/binnaclefup/{id}`)

Registra el seguimiento parcial de una bitácora del usuario de la sesión.

| # | Criterio de aceptación |
|---|------------------------|
| RF-8.1 | Cuando un usuario autenticado invoque `PUT /binnaclefup/{id}`, el sistema sustituye el `Id` del `BinnacleDTO` por el valor del parámetro de ruta y asigna a su `UserId` el identificador del usuario de la sesión leído de sus `claims`. |
| RF-8.2 | Si la sesión no porta un identificador de usuario legible, entonces el sistema responderá con el código `401` (RF-1.7) y no ejecutará ninguna función del endpoint. |
| RF-8.3 | Cuando el DTO tenga asignados `Id` y `UserId`, el sistema actualizará los campos de seguimiento de la bitácora (actividades realizadas, observaciones y fecha de inicio, cuyo valor procede del formulario de capas más externas convertido a tipo `DateOnly`, ver §7) y la bitácora quedará en estatus **"falta confirmar"**. |
| RF-8.4 | Si no existe ninguna bitácora que coincida a la vez con ese `Id` y ese `UserId`, entonces el sistema responderá con el código `404` y no realizará ninguna modificación. |
| RF-8.5 | Si los setters de la entidad rechazan los valores del DTO (`ActivitiesDone` o `Hints` con valor y menos de 15 caracteres, `Id` o `UserId` fuera de rango; la nulidad de los campos de texto se admite internamente), entonces el sistema responderá con el código `400` (`EntityException` → RF-12.2) y no realizará ninguna modificación. |
| RF-8.6 | Cuando la actualización se complete, el sistema responderá con el código `204` y sin cuerpo. |
| RF-8.7 | Si la bitácora no está en estatus **"en proceso"** (esto es, está en "falta confirmar", "cancelado" o "finalizado"), entonces el sistema responderá con el código `400` y no realizará ninguna modificación. |

### RF-9 — `ResetActivities` (PUT `/binnaclera/{id}`)

Reinicia las actividades de una bitácora del usuario de la sesión. **No tiene cuerpo.**

| # | Criterio de aceptación |
|---|------------------------|
| RF-9.1 | Cuando un usuario autenticado invoque `PUT /binnaclera/{id}`, el sistema generará un `BinnacleDTO` (la petición no tiene cuerpo) y asignará a su `Id` el valor del parámetro de ruta y a su `UserId` el identificador del usuario de la sesión leído de sus `claims`. |
| RF-9.2 | Si la sesión no porta un identificador de usuario legible, entonces el sistema responderá con el código `401` (RF-1.7) y no ejecutará ninguna función del endpoint. |
| RF-9.3 | Cuando el DTO tenga asignados `Id` y `UserId`, el sistema borrará las actividades realizadas y las observaciones de la bitácora y la dejará en estatus **"en proceso"** (valores predeterminados que aplica el repositorio). |
| RF-9.4 | Si la bitácora está en estatus "cancelado" o "finalizado", entonces el sistema responderá con el código `400` y no realizará ninguna modificación. |
| RF-9.5 | Si no existe ninguna bitácora que coincida a la vez con ese `Id` y ese `UserId`, entonces el sistema responderá con el código `404` y no realizará ninguna modificación. |
| RF-9.6 | Cuando la actualización se complete, el sistema responderá con el código `204` y sin cuerpo. |

### RF-10 — `CancelBinnacle` (PUT `/binnaclecb/{id}`)

Cancela una bitácora del usuario de la sesión registrando el motivo en sus observaciones.

| # | Criterio de aceptación |
|---|------------------------|
| RF-10.1 | Cuando un usuario autenticado invoque `PUT /binnaclecb/{id}`, el sistema sustituye el `Id` del `BinnacleDTO` por el valor del parámetro de ruta y asigna a su `UserId` el identificador del usuario de la sesión leído de sus `claims`. |
| RF-10.2 | Si la sesión no porta un identificador de usuario legible, entonces el sistema responderá con el código `401` (RF-1.7) y no ejecutará ninguna función del endpoint. |
| RF-10.3 | Cuando el DTO tenga asignados `Id` y `UserId`, el sistema actualizará las observaciones de la bitácora con el valor del accessor `CancelDesc` del DTO (considerado en vez de `Hints` en el mapper a entidad) y la dejará en estatus **"cancelado"**, estatus en el que solo el campo de observaciones tiene valor. |
| RF-10.4 | Si no existe ninguna bitácora que coincida a la vez con ese `Id` y ese `UserId`, entonces el sistema responderá con el código `404` y no realizará ninguna modificación. |
| RF-10.5 | Si el DTO viola una regla de negocio aplicable (`CancelDesc` con valor y menos de 15 caracteres, `Id` o `UserId` fuera de rango), entonces el sistema responderá con el código `400` y no realizará ninguna modificación. |
| RF-10.6 | Cuando la actualización se complete, el sistema responderá con el código `204` y sin cuerpo. |
| RF-10.7 | Si la bitácora está en estatus "falta confirmar" o "finalizado", entonces el sistema responderá con el código `400` y no realizará ninguna modificación (solo se puede cancelar una bitácora "en proceso" o ya "cancelado"). |

### RF-11 — `FinishBinnacle` (PUT `/binnaclefsh/{id}`)

Finaliza una bitácora del usuario de la sesión registrando la firma del cliente.

| # | Criterio de aceptación |
|---|------------------------|
| RF-11.1 | Cuando un usuario autenticado invoque `PUT /binnaclefsh/{id}`, el sistema sustituye el `Id` del `BinnacleDTO` por el valor del parámetro de ruta y asigna a su `UserId` el identificador del usuario de la sesión leído de sus `claims`. |
| RF-11.2 | Si la sesión no porta un identificador de usuario legible, entonces el sistema responderá con el código `401` (RF-1.7) y no ejecutará ninguna función del endpoint. |
| RF-11.3 | Cuando el DTO tenga asignados `Id` y `UserId`, el sistema actualizará la firma del cliente (`CustomerSignature` del DTO), fijará la fecha de fin a la fecha actual y dejará la bitácora en estatus **"finalizado"** (valores predeterminados que aplica el repositorio). |
| RF-11.4 | Si no existe ninguna bitácora que coincida a la vez con ese `Id` y ese `UserId`, entonces el sistema responderá con el código `404` y no realizará ninguna modificación. |
| RF-11.5 | Si el DTO viola una regla de negocio aplicable (`CustomerSignature` con más de 255 caracteres, `Id` o `UserId` fuera de rango), entonces el sistema responderá con el código `400` y no realizará ninguna modificación. |
| RF-11.6 | Cuando la actualización se complete, el sistema responderá con el código `204` y sin cuerpo. |
| RF-11.7 | Si la bitácora no está en estatus **"falta confirmar"** (esto es, está en "en proceso", "cancelado" o "finalizado"), entonces el sistema responderá con el código `400` y no realizará ninguna modificación. |

### RF-12 — Contrato de errores

Todo fallo de negocio producido por un caso de uso se traduce a un código HTTP y a un mensaje al usuario final.

| # | Criterio de aceptación |
|---|------------------------|
| RF-12.1 | Si un caso de uso falla por registro no encontrado (`KeyNotFoundException` del repositorio), entonces el sistema responderá con el código `404`. |
| RF-12.2 | Si un caso de uso falla por una violación de reglas de negocio de la entidad (`EntityException`), entonces el sistema responderá con el código `400`. |
| RF-12.3 | Si un caso de uso falla por una violación de reglas de negocio de la aplicación (`ApplicationException`: `Status` fuera de los 4 estatus, `Id`/`UserId`/`Visibility` inválidos, `page`/`elemsKey` fuera de rango), entonces el sistema responderá con el código `400`. |
| RF-12.4 | Si un caso de uso falla por cualquier otra causa de negocio (p. ej. la bitácora está cancelada o finalizada en `ResetActivities`), entonces el sistema responderá con el código `400`. |
| RF-12.5 | Si un caso de uso falla por una violación de integridad referencial emitida por el motor de persistencia, entonces el sistema responderá con el código `500`. |
| RF-12.6 | Donde la API produzca un mensaje de error, el mensaje será redactado en español. |

### RF-13 — Documentación del contrato (OpenAPI / Swagger)

Los diez endpoints deben exponer su contrato de respuestas documentado y su nombre público.

| # | Criterio de aceptación |
|---|------------------------|
| RF-13.1 | Cada endpoint usará `WithName(...)` con **exactamente** el nombre indicado en la tabla de la sección "Resumen de endpoints" (`InsertBinnacle`, `GetBinnacle`, `FollowupList`, `BinnaclesReport`, `UpdateBinnacle`, `UpdateBinnacleVisibility`, `FollowupPartial`, `ResetActivities`, `CancelBinnacle`, `FinishBinnacle`). |
| RF-13.2 | Cada endpoint usará `Produces(...)` para documentar **cada** código de respuesta que pueda emitir según sus RF. La siguiente tabla lista los códigos **exactos** que cada endpoint debe documentar, deducidos de RF-1 a RF-12. |
| RF-13.3 | Los nombres y códigos documentados coincidirán con los definidos en los RF-2 a RF-11, RF-12 y RF-1. |

#### Tabla de códigos `Produces(...)` por endpoint (verificable)

| Endpoint | WithName(...) | Códigos Produces(...) |
|----------|---------------|----------------------|
| `InsertBinnacle` | `InsertBinnacle` | `201`, `400`, `401`, `500` |
| `GetBinnacle` | `GetBinnacle` | `200`, `400`, `401`, `404` |
| `FollowupList` | `FollowupList` | `200`, `400`, `401` |
| `BinnaclesReport` | `BinnaclesReport` | `200`, `400`, `401`, `403` |
| `UpdateBinnacle` | `UpdateBinnacle` | `204`, `400`, `401`, `403`, `404` |
| `UpdateBinnacleVisibility` | `UpdateBinnacleVisibility` | `204`, `400`, `401`, `403`, `404` |
| `FollowupPartial` | `FollowupPartial` | `204`, `400`, `401`, `404` |
| `ResetActivities` | `ResetActivities` | `204`, `400`, `401`, `404` |
| `CancelBinnacle` | `CancelBinnacle` | `204`, `400`, `401`, `404` |
| `FinishBinnacle` | `FinishBinnacle` | `204`, `400`, `401`, `404` |

**Derivación:**
- `201/204/200` son los códigos de éxito de RF-2.5, RF-6.6, RF-7.6, RF-8.6, RF-9.6, RF-10.6, RF-11.6, RF-3.3, RF-4.4 y RF-5.3.
- `401` aplica a **todos** por RF-1.2; además a `InsertBinnacle`, `GetBinnacle`, `FollowupList`, `FollowupPartial`, `ResetActivities`, `CancelBinnacle` y `FinishBinnacle` por RF-1.7 (sesión sin identificador de usuario legible).
- `403` aplica solo a `BinnaclesReport`, `UpdateBinnacle` y `UpdateBinnacleVisibility` por RF-1.4, RF-1.5, RF-5.5, RF-6.7 y RF-7.7.
- `400` aplica a todos los que validan `Id`/`page`/`elemsKey` (RF-3.5, RF-4.3, RF-5.2, RF-6.5, RF-7.5), reglas de negocio de entidad (RF-2.3, RF-8.5, RF-10.5, RF-11.5), validaciones del caso de uso (RF-6.3, RF-7.3), `ResetActivities` sobre bitácora cancelada o finalizada (RF-9.4) y el control de estatus de `FollowupPartial`, `CancelBinnacle` y `FinishBinnacle` (RF-8.7, RF-10.7, RF-11.7) → RF-12.
- `404` aplica a los que buscan por `Id` (RF-3.4, RF-6.4, RF-7.4, RF-8.4, RF-9.5, RF-10.4, RF-11.4).
- `500` aplica solo a `InsertBinnacle` por violación de integridad referencial (RF-2.4).
- `FollowupList` y `BinnaclesReport` no devuelven `404`: la ausencia de registros es un `200` con elementos vacíos (RF-4.6, RF-5.6).

---

## 5. Requisitos no funcionales

| # | Requisito | Criterio de aceptación |
|---|-----------|------------------------|
| RNF-1 | Esquema por capas | La especificación respeta la separación entre dominio, aplicación, datos y repositorio establecida en `docs/constitution.md`. |
| RNF-2 | Puertos y adaptadores | Los casos de uso se comunican con la persistencia únicamente a través de los puertos de la capa de aplicación; ningún caso de uso accede directamente al mecanismo de almacenamiento. |
| RNF-3 | Inyección de dependencias | Los componentes se registran con ámbito de vida por petición (`scoped`). |
| RNF-4 | Plataforma | El sistema opera sobre .NET 10.0 usando únicamente biblioteca estándar. |
| RNF-5 | Persistencia | La persistencia de bitácoras se realiza mediante Entity Framework Core. |
| RNF-6 | Idioma | Los identificadores y los comentarios del código están en inglés; los mensajes destinados al usuario final están en español. |
| RNF-7 | Consistencia de datos | Cuando una operación de escritura se complete, el estado almacenado debe corresponder a los datos enviados por el consumidor de la API (y a los estatus impuestos por el caso de uso). |
| RNF-8 | Ausencia de filtración de secretos | Ninguna respuesta de la API debe exponer datos sensibles. |
| RNF-9 | Compatibilidad | El contrato de los diez endpoints no cambia de forma incompatible dentro de este caso de uso. |
| RNF-10 | Sin dependencias nuevas | No se añade ningún paquete NuGet fuera de los ya autorizados en la spec 001 (solo `Microsoft.AspNetCore.Authentication.JwtBearer` y EF Core). |

---

## 6. Casos límite

| # | Situación | Comportamiento esperado |
|---|-----------|------------------------|
| CE-1 | El `Id` del cuerpo de `PUT /binnacle/{id}`, `/binnaclev/{id}`, `/binnaclefup/{id}`, `/binnaclecb/{id}` o `/binnaclefsh/{id}` difiere del `Id` de la ruta. | Prevalece siempre el `Id` de la ruta. |
| CE-2 | `PUT /binnaclev/{id}` incluye propiedades ajenas a `Id` y `Visibility`. | Esas propiedades se ignoran y no producen efecto sobre la bitácora. |
| CE-3 | `PUT /binnaclera/{id}` se invoca con cuerpo. | El cuerpo se ignora: el sistema genera el DTO con `Id` de la ruta y `UserId` de la sesión. |
| CE-4 | Se solicita una bitácora inexistente (`GetBinnacle`, `UpdateBinnacle`, `UpdateBinnacleVisibility`, `FollowupPartial`, `ResetActivities`, `CancelBinnacle`, `FinishBinnacle`). | `404` y no se realiza ninguna modificación. |
| CE-5 | `GET /binnacle/{id}` sobre una bitácora que no coincide con el `UserId` de la sesión (pertenece a otro usuario). | `404`. |
| CE-6 | `GET /binnacle/{id}` o `POST /binnacle/` con sesión válida pero sin identificador de usuario legible en sus `claims`. | `401` y no se ejecuta ninguna función del endpoint (RF-1.7). |
| CE-7 | Cualquiera de las rutas recibe un `Id`, `page` o `elemsKey` no numérico, o numérico con valor cero o negativo. | `400` y no se ejecuta ninguna función del endpoint. |
| CE-8 | Se invocan los diez endpoints sin sesión o con sesión expirada (30 min). | `401` y no se ejecuta ninguna función del endpoint. |
| CE-9 | Se invocan `BinnaclesReport`, `UpdateBinnacle` o `UpdateBinnacleVisibility` con una sesión de rol distinto de `admin`. | `403` y no se ejecuta ninguna función del endpoint. |
| CE-10 | Se invocan los endpoints que exigen `admin` con sesión cuyo `Role` de los `claims` está ausente o cuyo valor no pertenece a `admin`/`user` (la sesión no ha caducado). | `403`. La caducidad de la sesión y un token con firma alterada se tratan aparte en CE-8 con `401`. |
| CE-11 | `InsertBinnacle`, `GetBinnacle`, `FollowupList`, `FollowupPartial`, `ResetActivities`, `CancelBinnacle` o `FinishBinnacle` con sesión válida pero sin identificador de usuario legible en sus `claims`. | `401` y no se ejecuta ninguna función del endpoint (RF-1.7). |
| CE-12 | `UpdateBinnacle` envía un `Status` nulo o distinto de los 4 estatus. | `400` y no se realiza ninguna modificación (RF-6.3). |
| CE-13 | `UpdateBinnacleVisibility` envía `Visibility` distinto de `ENABLED` o `DISABLED`. | `400` y no se realiza ninguna modificación. |
| CE-14 | `ResetActivities` se invoca sobre una bitácora cancelada o finalizada. | `400` y no se realiza ninguna modificación (RF-9.4). |
| CE-15 | El DTO de `InsertBinnacle` (o los campos editables de `FollowupPartial`, `CancelBinnacle`, `FinishBinnacle`) viola una regla de negocio de la entidad. | `400` y no se escribe ningún dato. |
| CE-16 | `POST /binnacle/` envía un `ContactId` (o la sesión porte un `UserId`) que no corresponde a un registro existente. | `500` y no se crea la bitácora (integridad referencial; RF-2.4). |
| CE-17 | El cuerpo de `InsertBinnacle` envía estatus, visibilidad, fecha de inicio, actividades, observaciones o un `UserId` distintos de los fijados por el sistema. | Se ignoran: nace "en proceso", "ENABLED", con la fecha de inicio en la fecha actual, actividades y observaciones vacías y con el `UserId` de la sesión (RF-2.1, RF-2.2). |
| CE-18 | Se invocan `InsertBinnacle`, `GetBinnacle`, `FollowupList`, `FollowupPartial`, `ResetActivities`, `CancelBinnacle` o `FinishBinnacle` con sesión de rol distinto de `admin`. | Se ejecuta normalmente (solo se exige sesión válida; RF-1.6). |
| CE-19 | `FollowupList` o `BinnaclesReport` se invocan sin registros que cumplir (o `page` excede el total de páginas). | `200` con resultado paginado de elementos vacíos (RF-4.6, RF-5.6). |
| CE-20 | `GetBinnacle` consulta una bitácora sin equipo asignado. | `200` con la orden sin datos de equipo (el equipo es opcional, RF-3.3). |
| CE-21 | `binnFilter` de `BinnaclesReport` no contiene las claves esperadas. | La existencia y el formato de las claves las aseguran capas más externas del MVP: fuera de alcance (§7). |
| CE-22 | Se emite un error en cualquiera de los diez endpoints. | El mensaje al usuario se entrega en español. |
| CE-23 | `FollowupPartial` se invoca sobre una bitácora que no está en estatus "en proceso" ("falta confirmar", "cancelado" o "finalizado"). | `400` y no se realiza ninguna modificación (RF-8.7). |
| CE-24 | `BinnaclesReport` se invoca con un filtro de visibilidad que excluye bitácoras deshabilitadas. | Esas bitácoras no aparecen ni en los elementos de la página ni en los totales (RF-7.8). |
| CE-25 | `UpdateBinnacle` envía campos que no pertenecen al estatus indicado en `Status`. | Esos campos se ignoran y no producen efecto sobre la bitácora (RF-6.2). |
| CE-26 | `CancelBinnacle` se invoca sobre una bitácora en estatus "falta confirmar" o "finalizado". | `400` y no se realiza ninguna modificación (RF-10.7). |
| CE-27 | `FinishBinnacle` se invoca sobre una bitácora que no está en estatus "falta confirmar" ("en proceso", "cancelado" o "finalizado"). | `400` y no se realiza ninguna modificación (RF-11.7). |
| CE-28 | `FollowupPartial`, `CancelBinnacle` o `FinishBinnacle` se invocan sobre una bitácora inexistente. | `404` (el registro no existe, no hay estatus que comprobar) y no se realiza ninguna modificación (RF-8.4, RF-10.4, RF-11.4). |

---

## 7. Fuera de alcance

- La **forma de mostrar la información de la orden de forma parcial o total según su estatus** (presentación en capas más externas del MVP).
- La **interfaz dinámica de campos según el estatus en `UpdateBinnacle`**: qué campos se muestran y se envían para cada estatus se resuelve en capas más externas del MVP (RF-6.2).
- La **conversión de las fechas de los formularios a tipo `DateOnly`** para el accessor `StartingDate` (capas más externas del MVP, RF-8.3).
- La **existencia, el formato y el significado de las claves de `binnFilter`** en `BinnaclesReport`: se aseguran en capas más externas del MVP.
- Persistencia de cualquier entidad distinta de la bitácora (usuarios, tipos, empresas, contactos y equipos pertenecen a specs 001–005).
- El alta de usuarios, empresas, contactos, tipos y equipos.
- Recuperación, restablecimiento o envío de contraseñas por correo.
- Esquema de autorización distinto del binario administrador / no administrador.
- Autenticación por proveedores externos, correo, SMS o segundo factor.
- Auditoría de los cambios realizados sobre las bitácoras.
- Versionado y publicación del contrato de la API más allá de RNF-9.
- Pruebas de rendimiento, de carga y de penetración.
- Modificación de `hexArch/repository` (prohibido por el principio 5 de la Constitución).
- Verificación de nulidad y longitud de los campos de texto de la bitácora (`Service`, `ActivitiesDone`, `Hints`, `CustomerSignature`, `Status`): la nulidad se admite internamente (la entidad solo valida los accessors que tienen valor y lanza `EntityException` en caso de incumplimiento) y la obligatoriedad de estos campos se verifica en las capas más externas del MVP; **las tareas de esta spec no contemplan esas comprobaciones**.
- Recorte (trim) de espacios en blanco de los campos de texto: la entidad solo exige longitudes mínimas y máximas; el recorte ocurre en capas más externas.
- Escritura directa de los strings de estatus o visibilidad por parte del usuario en la interfaz: las opciones predefinidas que el usuario no puede alterar se resuelven en capas más externas.
- La mecánica de captura de la firma del cliente (solo se transporta el valor en `CustomerSignature`).

---

## 8. Criterios de finalización

El caso de uso Bitácora se considera concluido cuando:

1. Los diez endpoints de la sección 4 están disponibles y responden conforme a su criterio de aceptación.
2. Los diez endpoints rechazan con `401` las peticiones sin sesión válida o con sesión vencida.
3. `BinnaclesReport`, `UpdateBinnacle` y `UpdateBinnacleVisibility` rechazan con `403` las peticiones cuyo `Role` de los `claims` no sea `admin` (incluido el `Role` ausente o con valor fuera de `admin`/`user`), sin ejecutar ninguna función del endpoint.
4. `InsertBinnacle`, `GetBinnacle`, `FollowupList`, `FollowupPartial`, `ResetActivities`, `CancelBinnacle` y `FinishBinnacle` aceptan usuario autenticado (no exigen `admin`).
5. `InsertBinnacle`, `GetBinnacle`, `FollowupList`, `FollowupPartial`, `ResetActivities`, `CancelBinnacle` y `FinishBinnacle` rechazan con `401` las peticiones cuya sesión no porte un identificador de usuario legible.
6. Un `Id`, `page` o `elemsKey` de ruta no numérico, o numérico con valor cero o negativo, se rechaza con `400` en todos los endpoints que lo reciben.
7. En `GetBinnacle` la petición no tiene cuerpo: el handler solo genera el DTO con `Id` de la ruta y `UserId` de la sesión. En los PUT con cuerpo, el `Id` de la ruta prevalece sobre el `Id` del cuerpo.
8. `UpdateBinnacleVisibility` aplica únicamente el campo `Visibility` e ignora cualquier otra propiedad del DTO recibido.
9. `UpdateBinnacle` rechaza con `400` un `Status` nulo o fuera de los 4 estatus, sin modificar nada.
10. `FollowupList` filtra solo las bitácoras del usuario de la sesión en estatus "en proceso" o "falta confirmar"; `BinnaclesReport` pasa `binnFilter` y mantiene el DTO nulo; ambos identifican su listado con las constantes `"FollowupList"` y `"BinnaclesReport"`, pasadas como argumento del parámetro `controllerAction`.
11. `ResetActivities` no tiene cuerpo y rechaza con `400` las bitácoras canceladas o finalizadas; al completarse deja la bitácora "en proceso" sin actividades ni observaciones.
12. `FollowupPartial` deja la bitácora en "falta confirmar", `CancelBinnacle` la deja "cancelado" con sus observaciones y `FinishBinnacle` la deja "finalizado" con la firma del cliente y la fecha de fin; en los tres, el `UserId` proviene de la sesión.
13. `InsertBinnacle` crea la bitácora en estatus "en proceso", visibilidad "ENABLED", fecha de inicio igual a la fecha actual y sin actividades ni observaciones (vacías), ignorando esos valores del cuerpo y usando el `UserId` de la sesión.
14. Los orígenes de fallo de la sección 4 se traducen a `404`, `400` o `500` según corresponda, con mensajes en español.
15. Las respuestas de éxito son: `201` sin cuerpo en `InsertBinnacle`; `204` sin cuerpo en los seis PUT; `200` con la bitácora en `GetBinnacle` y con un objeto `PaginationResult` (elementos de la página, página actual, total de páginas y total de registros) en `FollowupList` y `BinnaclesReport`.
16. Cada endpoint cuenta con `WithName(...)` usando exactamente los nombres de la tabla de resumen y `Produces(...)` para cada código de respuesta posible según la tabla de RF-13.
17. El proyecto compila sin errores y todas las pruebas pasan (`dotnet test` verde).
18. Cada endpoint cuenta con su prueba automatizada y con la prueba de su caso límite y su caso de error, según `docs/constitution.md` (sin contemplar la verificación de nulidad y longitud de los campos de texto, ver §7); cada prueba crea sus propios datos.
19. `BinnaclesReport` excluye del resultado paginado —elementos y totales— las bitácoras cuya visibilidad no coincida con la solicitada en `binnFilter`; la visibilidad no afecta a ningún otro endpoint.
20. `FollowupPartial` solo opera sobre bitácoras "en proceso" y rechaza con `400` las que estén en "falta confirmar", "cancelado" o "finalizado"; `CancelBinnacle` rechaza con `400` las bitácoras en "falta confirmar" o "finalizado"; `FinishBinnacle` solo opera sobre bitácoras "falta confirmar" y rechaza con `400` las que estén en "en proceso", "cancelado" o "finalizado"; en los tres casos, sin modificar nada y prevaleciendo el `404` cuando la bitácora no existe.

---

## 9. Dudas abiertas

- Ninguna (las cinco dudas planteadas en la fase de clarificación quedaron resueltas: constante `"FollowupList"`, codificación de respuestas heredada de specs 001–005, `401` cuando la sesión no porta identificador de usuario, validación de `Status` en el caso de uso y claves de `binnFilter` aseguradas fuera del MVP).
