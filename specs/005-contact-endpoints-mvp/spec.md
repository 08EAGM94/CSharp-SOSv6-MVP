# Especificación — Caso de Uso: Contacto

- **ID de spec:** `005-contact-endpoints-mvp`
- **Estado:** implementada y validada
- **Constitución aplicable:** `docs/constitution.md`
- **Alcance de este documento:** QUÉ se construye y POR QUÉ. Las decisiones de implementación (mecanismo concreto de sesión, estructura del manejo de errores, estrategia de pruebas) corresponden al plan, no a esta spec.

---

## 1. Contexto y objetivo

El sistema SOS v6 administra **bitácoras** (órdenes de servicio registradas en campo). Cada bitácora referencia obligatoriamente un **contacto** (`ContactoId` obligatorio) y un contacto pertenece obligatoriamente a una **empresa** (`EmpresaId` obligatorio). Esa cadena —**Empresa → Contacto → Bitácora**— hace que la empresa y el contacto sean la **información del cliente que solicitó el servicio**: la empresa identifica a la organización cliente y el contacto a la persona de referencia dentro de esa organización. El contacto tiene una relación **n:1 con Empresa** (muchos contactos pertenecen a una empresa) y una relación **1:n con Bitácora** (un contacto puede aparecer en muchas órdenes de servicio).

En el sistema existen **dos tipos de registro de contactos**:

1. **Registro inicial** — cuando se registra una **Empresa por primera vez** junto con el contacto que solicita el servicio (persona de referencia inicial). Este flujo ya está cubierto por la spec 003 (`InsertEnterprise`, que crea la empresa y su contacto inicial de forma atómica).
2. **Registro adicional** — cuando ya existe una **Empresa existente** y se le quiere **agregar otro contacto** (otra persona de referencia de esa organización). Este flujo es el que cubre **esta spec** mediante `InsertContact`.

Esta spec define el **caso de uso Contacto**: la capacidad de registrar un contacto adicional a una empresa existente, consultar, listar por empresa (listado completo y listado reducido para *selects*), obtener el listado reducido global de contactos (`GetContactsForSelect`), actualizar y cambiar la visibilidad de los contactos.

El objetivo es que **cualquier usuario autenticado** pueda crear un contacto para una empresa existente (`InsertContact`), consultar un contacto individual (`GetContact`) y obtener el listado reducido para *selects* de la interfaz (`GetContactsByEnterpriseForSelect`), y que **únicamente un administrador autenticado** pueda **gobernar** los contactos (listado completo por empresa con `GetContactsByEnterprise`, listado reducido global de contactos con `GetContactsForSelect`, y las modificaciones `UpdateContact` y `UpdateContactVisibility`).

**Por qué este caso de uso va después de Empresa (003):** el control de acceso a los endpoints de Contacto depende de la sesión y el rol establecidos por el caso de uso Usuario (spec 001), y todo contacto pertenece obligatoriamente a una empresa registrada por el caso de uso Empresa (spec 003); además, la bitácora (orden de servicio) referenciará un contacto como parte de la información del cliente.

---

## 2. Usuarios

| Actor | Descripción | Puede hacer |
|-------|-------------|-------------|
| **Administrador** | Usuario con rol `admin`. Único autorizado a gobernar los contactos de una empresa. | Iniciar sesión (vía caso de uso Usuario), crear, consultar, listar por empresa, obtener el listado reducido global de contactos, actualizar y cambiar visibilidad de contactos. |
| **Usuario operativo** | Usuario con un rol distinto de `admin`. Existe, puede autenticarse y consultar contactos. | Iniciar sesión, crear un contacto adicional para una empresa existente, consultar un contacto, listar contactos habilitados de una empresa para selects. |
| **Consumidor de la API** | Cliente (aplicación o herramienta) que invoca los endpoints de esta spec. | Invocar los siete endpoints respetando el contrato de cada uno. |

---

## 3. Historias de usuario

### HU-1 — Gobernar los contactos de una empresa

**Como** administrador del sistema, **quiero** registrar un contacto adicional para una empresa existente, consultar un contacto, listar todos los contactos de una empresa, obtener el listado reducido global de contactos, actualizar y cambiar la visibilidad de los contactos, **para** mantener al día la información de las personas de referencia de los clientes que solicitan los servicios; un contacto mal informado o deshabilitado indebidamente impacta en las bitácoras (órdenes de servicio) vinculadas a través de la cadena Empresa → Contacto → Bitácora.

### HU-2 — Consultar los contactos de una empresa

**Como** usuario operativo del sistema, **quiero** consultar un contacto por su identificador y obtener la lista reducida de contactos habilitados de una empresa para selects, **para** poder seleccionar correctamente el contacto (persona de referencia del cliente) al registrar una bitácora (orden de servicio), ya que la bitácora exige un contacto obligatorio que pertenece a una empresa, y el select de la interfaz se alimenta del endpoint reducido.

### HU-3 — Proteger los contactos de una empresa

**Como** administrador del sistema, **quiero** que las operaciones de **listado completo por empresa, listado reducido global de contactos, actualización y cambio de visibilidad** de los contactos estén restringidas a usuarios con rol `admin`, **para** que ningún otro usuario pueda alterar la información de los contactos, lo cual repercute en los clientes y en las bitácoras vinculadas a esos contactos. La creación de contactos (`InsertContact`) es accesible para cualquier usuario autenticado, pues responde al alta de una persona de referencia adicional de una empresa ya registrada.

### HU-4 — Alta de un contacto adicional a una empresa existente y relación con la bitácora

**Como** usuario operativo del sistema, **quiero** poder agregar un nuevo contacto a una empresa que ya está registrada, **para** que la organización cliente pueda referenciar a otra persona distinta de la que se dio de alta junto con la empresa (ese primer contacto se registró en la spec 003, al crear la empresa); así la Empresa → Contacto → Bitácora completa la información del cliente que solicitó el servicio, y un mismo contacto puede ser el referente de varias órdenes de servicio (relación 1:n Contacto → Bitácora).

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
| POST | `/contact/` | `InsertContact` | `IEnterpriseChildrenService<ContactDTO>` | `IByEnterpriseRepository<ContactEntity, ContactDTO>` | `ContactDTO` completo (`EnterpriseId`, `FullName` obligatorios; `Visibility` se fija a ENABLED) |
| GET | `/contact/{id}` | `GetContact` | `IEnterpriseChildrenService<ContactDTO>` | `IByEnterpriseRepository<ContactEntity, ContactDTO>` | DTO de entrada: solo `Id` (tomado del parámetro de ruta); **respuesta: `ContactDTO` con `Id`, `EnterpriseId`, `FullName` y el objeto `Enterprise` (datos de la empresa asociada); `Visibility` no viaja en la respuesta** |
| GET | `/contactsent/{enterpriseId}` | `GetContactsByEnterprise` | `IEnterpriseChildrenService<ContactDTO>` | `IByEnterpriseRepository<ContactEntity, ContactDTO>` | DTO de entrada: solo `EnterpriseId` (tomado del parámetro de ruta, valor int); **respuesta: `IEnumerable<ContactDTO>` con `Id`, `EnterpriseId`, `FullName`, `Visibility` (todas las propiedades del listado, incluidos contactos deshabilitados)** |
| GET | `/contactsentsct/{enterpriseId}` | `GetContactsByEnterpriseForSelect` | `IEnterpriseChildrenService<ContactDTO>` | `IByEnterpriseRepository<ContactEntity, ContactDTO>` | DTO de entrada: solo `EnterpriseId` (tomado del parámetro de ruta, valor int); **respuesta: `IEnumerable<ContactDTO>` con `Id`, `EnterpriseId`, `FullName`, `Visibility`; filtro por `Visibility = ENABLED`** |
| PUT | `/contact/{id}` | `UpdateContact` | `IEnterpriseChildrenService<ContactDTO>` | `IByEnterpriseRepository<ContactEntity, ContactDTO>` | `ContactDTO` con `Id` tomado del parámetro de ruta y `FullName` como único campo editable (`EnterpriseId` no es modificable por el usuario; `Visibility` se gestiona en endpoint separado) |
| PUT | `/contactv/{id}` | `UpdateContactVisibility` | `IEnterpriseChildrenService<ContactDTO>` | `IByEnterpriseRepository<ContactEntity, ContactDTO>` | `ContactDTO` con `Id` de la ruta y `Visibility` |
| GET | `/contactsct/` | `GetContactsForSelect` | `ISelectService<ContactDTO>` | `ISelectRepository<ContactDTO>` | Sin parámetros de entrada; **respuesta: `IEnumerable<ContactDTO>` con `Id` y `FullName` (contactos con `Visibility = ENABLED`); `EnterpriseId` y `Visibility` no viajan en la respuesta** |

### RF-1 — Control de acceso previo a toda operación protegida

Todos los endpoints de esta spec **exigen sesión válida**.

| # | Criterio de aceptación |
|---|------------------------|
| RF-1.1 | Cuando una petición se dirige a cualquiera de los siete endpoints, el sistema verificará primero que la petición porta una sesión válida. |
| RF-1.2 | Si una petición no porta sesión válida o su sesión ha expirado (vigencia de 30 minutos desde la emisión), entonces el sistema responderá con el código `401` y no ejecutará ninguna función del endpoint. |
| RF-1.3 | Donde exista una sesión válida, el sistema comprobará **antes de ejecutar cualquier otra acción** que el rol de esa sesión sea `admin` **solo para los endpoints `GetContactsByEnterprise`, `GetContactsForSelect`, `UpdateContact` y `UpdateContactVisibility`**. Ese rol es el leído de los `claims` emitidos en el login (spec 001) y es la única fuente que decide si el flujo del endpoint continúa. |
| RF-1.4 | Si el rol de la sesión no es `admin` **y el endpoint es `GetContactsByEnterprise`, `GetContactsForSelect`, `UpdateContact` o `UpdateContactVisibility`**, entonces el sistema responderá con el código `403` y no ejecutará ninguna función vital del endpoint. |
| RF-1.5 | Si la sesión existe pero el `Role` de sus `claims` está ausente, alterado o manipulado (la comparación del rol no puede realizarse), entonces el sistema responderá con el código `403` y no ejecutará ninguna función vital del endpoint. La caducidad de la sesión no entra aquí: se rige por RF-1.2 con código `401`. |
| RF-1.6 | Para los endpoints `InsertContact`, `GetContact` y `GetContactsByEnterpriseForSelect`, **basta con una sesión válida (usuario autenticado)**; no se verifica el rol `admin`. |
| RF-1.7 | Los endpoints `GetContactsByEnterprise` y `GetContactsForSelect` exigen rol `admin`; los endpoints `UpdateContact` y `UpdateContactVisibility` también exigen rol `admin`. |
| RF-1.8 | Ningún endpoint de esta spec es público: todos exigen sesión válida. No existe equivalente a `login` en este caso de uso. |

### RF-2 — `InsertContact` (POST `/contact/`)

Registra un **contacto adicional** para una empresa ya existente. El caso de uso fija `Visibility = "ENABLED"` al crear.

| # | Criterio de aceptación |
|---|------------------------|
| RF-2.1 | Cuando un usuario autenticado invoque `POST /contact/`, el sistema registrará un contacto a partir del DTO de contacto recibido, asociándolo a la empresa indicada en `EnterpriseId`. |
| RF-2.2 | El caso de uso (`EnterpriseChildrenService`) asigna `Visibility = "ENABLED"` al nuevo registro; el valor de `Visibility` enviado en el cuerpo (si lo hay) es ignorado. Por consiguiente, no es posible crear un contacto con `Visibility = "DISABLED"`. |
| RF-2.3 | Si el DTO no cumple las reglas de negocio del contacto (`EnterpriseId` > 0, `FullName` entre 10 y 150 caracteres — la entidad exige una longitud mínima de 10 y máxima de 150 sin recortar espacios en blanco), entonces el sistema responderá con el código `400` (`EntityException` → RF-8.2) y no creará el registro. El tratamiento de valores anulables y el recorte (trim) de `FullName` se resuelven en capas más externas y quedan fuera del alcance del MVP (ver §7). |
| RF-2.4 | Si el `EnterpriseId` del DTO no corresponde a ninguna empresa registrada, entonces el sistema responderá con el código `500` y no creará el registro (violación de integridad referencial emitida por el motor de persistencia). |
| RF-2.5 | Cuando el registro se complete, el sistema responderá con el código `201` y sin cuerpo; el caso de uso de alta no produce ningún objeto de retorno. |
| RF-2.6 | La creación no expone en la respuesta ninguna referencia al recurso creado: la respuesta se limita al código `201` y no incluye cuerpo ni cabecera `Location`. |

### RF-3 — `GetContact` (GET `/contact/{id}`)

Obtiene un registro de contacto con los datos de la empresa a la que pertenece. **No filtra por visibilidad**: devuelve `200` con el registro aunque su `Visibility` sea `DISABLED` (coherente con specs 001, 002, 003 y 004). El `404` queda reservado exclusivamente a identificadores inexistentes.

| # | Criterio de aceptación |
|---|------------------------|
| RF-3.1 | Cuando un usuario autenticado invoque `GET /contact/{id}`, el sistema sustituye el `Id` del `ContactDTO` por el valor del parámetro de ruta. |
| RF-3.2 | Cuando el DTO tenga asignado el `Id` del parámetro de ruta, el sistema devolverá el registro de contacto correspondiente a ese identificador (`ContactDTO` con `Id`, `EnterpriseId`, `FullName` y el objeto `Enterprise` con los datos de la empresa asociada). |
| RF-3.3 | Si el identificador no corresponde a ningún contacto, entonces el sistema responderá con el código `404`. |
| RF-3.4 | Si el `Id` de la ruta no es un valor numérico válido, o si siendo numérico es cero o negativo, entonces el sistema responderá con el código `400` (valores numéricos fuera de rango los rechaza la entidad, `EntityException` → RF-8.2) y no consultará ningún registro. |
| RF-3.5 | Si el contacto existe pero tiene `Visibility = DISABLED`, el sistema devolverá `200` con el `ContactDTO`; no responderá `404`. |
| RF-3.6 | El campo `Visibility` **no viaja** en la respuesta de `GetContact` (queda ausente en el `ContactDTO` devuelto); el consumidor no debe asumir su presencia. La decisión sobre cómo tratan las capas más externas los campos anulables queda fuera del alcance del MVP (ver §7). |

### RF-4 — `GetContactsByEnterprise` (GET `/contactsent/{enterpriseId}`)

Obtiene **todos** los registros de contactos de una empresa, **incluyendo los deshabilitados**. Esta decisión es coherente con la consulta de listado del catálogo por empresa, que no aplica filtro de visibilidad: el listado completo sirve para gobernar los contactos del cliente, de ahí que exista un endpoint dedicado (`GetContactsByEnterpriseForSelect`) con solo los habilitados para los selects. **Solo accesible para administrador.**

| # | Criterio de aceptación |
|---|------------------------|
| RF-4.1 | Cuando un administrador autenticado invoque `GET /contactsent/{enterpriseId}`, el sistema sustituye el `EnterpriseId` del `ContactDTO` por el valor del parámetro de ruta (valor int). |
| RF-4.2 | Cuando el DTO tenga asignado el `EnterpriseId` del parámetro de ruta, el sistema devolverá el conjunto de **todos** los registros de contactos de esa empresa, independientemente de su `Visibility`. |
| RF-4.3 | El conjunto devuelto **incluirá** registros con `Visibility = DISABLED`, con independencia de su estado de visibilidad en el almacenamiento. |
| RF-4.4 | Si no existe ningún contacto registrado para esa empresa, **o si el `enterpriseId` no corresponde a ninguna empresa registrada** (la consulta devuelve un conjunto sin registros, sin que se produzca un error que deba gestionarse), entonces el sistema devolverá un conjunto vacío con el código `200`. |
| RF-4.5 | La respuesta contiene por cada contacto `Id`, `EnterpriseId`, `FullName` y `Visibility`. |
| RF-4.6 | Si el `enterpriseId` de la ruta no es un valor numérico válido, o si siendo numérico es cero o negativo, entonces el sistema responderá con el código `400` (valores numéricos fuera de rango los rechaza la entidad, `EntityException` → RF-8.2) y no consultará ningún registro. |
| RF-4.7 | Si la sesión no tiene rol `admin`, entonces el sistema responderá con el código `403` y no ejecutará ninguna función del endpoint. |

### RF-5 — `UpdateContact` (PUT `/contact/{id}`)

Actualiza un contacto. Solo accesible para administrador.

| # | Criterio de aceptación |
|---|------------------------|
| RF-5.1 | Cuando un administrador autenticado invoque `PUT /contact/{id}`, el sistema sustituye el `Id` del `ContactDTO` por el valor del parámetro de ruta, con independencia del `Id` que venga en el cuerpo de la petición. |
| RF-5.2 | Cuando el DTO tenga asignado el `Id` del parámetro de ruta, el sistema actualizará el contacto correspondiente a ese identificador aplicando **únicamente el campo `FullName`**. El `Id` y el `EnterpriseId` no son modificables por el usuario: el contacto conserva su empresa y su identificador (la prohibición se impone en capas más externas, fuera del alcance del MVP, ver §7). |
| RF-5.3 | Si el identificador no corresponde a ningún contacto, entonces el sistema responderá con el código `404` y no realizará ninguna modificación. |
| RF-5.4 | Si el DTO no cumple las reglas de negocio aplicables al campo editable (`FullName` entre 10 y 150 caracteres, sin recortar espacios en blanco), entonces el sistema responderá con el código `400` (`EntityException` → RF-8.2) y no realizará ninguna modificación. El tratamiento de valores anulables y el recorte (trim) quedan fuera del alcance del MVP (ver §7). |
| RF-5.5 | Cuando la actualización se complete, el sistema responderá con el código `204` y sin cuerpo: la operación de actualización (vía `EnterpriseChildrenService.UpdateAsyncChild`) no produce ningún objeto de retorno. |
| RF-5.6 | Si el `Id` de la ruta no es un valor numérico válido, o si siendo numérico es cero o negativo, entonces el sistema responderá con el código `400` (valores numéricos fuera de rango los rechaza la entidad, `EntityException` → RF-8.2) y no realizará ninguna modificación. |

### RF-6 — `UpdateContactVisibility` (PUT `/contactv/{id}`)

Actualiza **únicamente** el campo `Visibility` de un contacto. Solo accesible para administrador.

| # | Criterio de aceptación |
|---|------------------------|
| RF-6.1 | Cuando un administrador autenticado invoque `PUT /contactv/{id}`, el sistema sustituye el `Id` del `ContactDTO` por el valor del parámetro de ruta. |
| RF-6.2 | Cuando el endpoint actualice el campo `Visibility`, el sistema tomará del DTO **únicamente** las propiedades `Id` y `Visibility`; cualquier otra propiedad del DTO será ignorada. |
| RF-6.3 | Si el valor recibido en `Visibility` no es uno de los admitidos por las reglas de negocio (`ENABLED` o `DISABLED`), entonces el sistema responderá con el código `400` y no realizará ninguna modificación. El usuario no escribe libremente ese string: los adaptadores de puerto secundario lo generan automáticamente y en la interfaz solo se ofrece dentro de opciones predefinidas que el usuario no puede alterar; el origen y formato concreto quedan fuera del alcance del MVP (ver §7). |
| RF-6.4 | Si el identificador no corresponde a ningún contacto, entonces el sistema responderá con el código `404` y no realizará ninguna modificación. |
| RF-6.5 | Cuando la modificación se complete, el sistema responderá con el código `204` y sin cuerpo: la operación de actualización de visibilidad (vía `EnterpriseChildrenService.UpdateAsyncVisibility`) no produce ningún objeto de retorno. |
| RF-6.6 | Si el `Id` de la ruta no es un valor numérico válido, o si siendo numérico es cero o negativo, entonces el sistema responderá con el código `400` (valores numéricos fuera de rango los rechaza la entidad, `EntityException` → RF-8.2) y no realizará ninguna modificación. |

### RF-7 — `GetContactsByEnterpriseForSelect` (GET `/contactsentsct/{enterpriseId}`)

Obtiene los registros de contactos **habilitados únicamente** (`Visibility = ENABLED`) de una empresa, filtra por `Visibility = "ENABLED"`.

| # | Criterio de aceptación |
|---|------------------------|
| RF-7.1 | Cuando un usuario autenticado invoque `GET /contactsentsct/{enterpriseId}`, el sistema sustituye el `EnterpriseId` del `ContactDTO` por el valor del parámetro de ruta (valor int). |
| RF-7.2 | Cuando el DTO tenga asignado el `EnterpriseId` del parámetro de ruta, el sistema devolverá el conjunto de registros de contactos de esa empresa con `Visibility = ENABLED`. |
| RF-7.3 | El conjunto devuelto **no incluirá** ningún registro con `Visibility = DISABLED`, con independencia de su existencia en el almacenamiento. |
| RF-7.4 | Si no existe ningún contacto habilitado registrado para esa empresa, **o si el `enterpriseId` no corresponde a ninguna empresa registrada** (la consulta devuelve un conjunto sin registros, sin que se produzca un error que deba gestionarse), entonces el sistema devolverá un conjunto vacío con el código `200`. |
| RF-7.5 | La respuesta contiene por cada contacto `Id`, `EnterpriseId`, `FullName` y `Visibility`. El filtro `Visibility = ENABLED` se aplica en la consulta; el campo `Visibility` de la respuesta es siempre `ENABLED`. |
| RF-7.6 | Si el `enterpriseId` de la ruta no es un valor numérico válido, o si siendo numérico es cero o negativo, entonces el sistema responderá con el código `400` (valores numéricos fuera de rango los rechaza la entidad, `EntityException` → RF-8.2) y no consultará ningún registro. |

### RF-8 — Contrato de errores

Todo fallo de negocio producido por un caso de uso se traduce a un código HTTP y a un mensaje al usuario final.

| # | Criterio de aceptación |
|---|------------------------|
| RF-8.1 | Si un caso de uso falla por registro no encontrado (`KeyNotFoundException` del repositorio), entonces el sistema responderá con el código `404`. |
| RF-8.2 | Si un caso de uso falla por una violación de reglas de negocio de la entidad (`EntityException`), entonces el sistema responderá con el código `400`. |
| RF-8.3 | Si un caso de uso falla por una violación de reglas de negocio de la aplicación (`ApplicationException`, p. ej. `Visibility` inválido o `Id` < 1), entonces el sistema responderá con el código `400`. |
| RF-8.4 | Si un caso de uso falla por cualquier otra causa de negocio, entonces el sistema responderá con el código `400`. |
| RF-8.5 | Si un caso de uso falla por una violación de integridad referencial emitida por el motor de persistencia, entonces el sistema responderá con el código `500`. |
| RF-8.6 | Donde la API produzca un mensaje de error, el mensaje será redactado en español. |

### RF-9 — Documentación del contrato (OpenAPI / Swagger)

Los siete endpoints deben exponer su contrato de respuestas documentado y su nombre público.

| # | Criterio de aceptación |
|---|------------------------|
| RF-9.1 | Cada endpoint usará `WithName(...)` con **exactamente** el nombre indicado en la tabla de la sección "Resumen de endpoints" (`InsertContact`, `GetContact`, `GetContactsByEnterprise`, `GetContactsByEnterpriseForSelect`, `UpdateContact`, `UpdateContactVisibility`, `GetContactsForSelect`). |
| RF-9.2 | Cada endpoint usará `Produces(...)` para documentar **cada** código de respuesta que pueda emitir según sus RF. La siguiente tabla lista los códigos **exactos** que cada endpoint debe documentar, deducidos de RF-1 a RF-8 y RF-10. |
| RF-9.3 | Los nombres y códigos documentados coincidirán con los definidos en los RF-2 a RF-7, RF-10 y RF-1. |

#### Tabla de códigos `Produces(...)` por endpoint (verificable)

| Endpoint | WithName(...) | Códigos Produces(...) |
|----------|---------------|----------------------|
| `InsertContact` | `InsertContact` | `201`, `400`, `401`, `500` |
| `GetContact` | `GetContact` | `200`, `400`, `401`, `404` |
| `GetContactsByEnterprise` | `GetContactsByEnterprise` | `200`, `400`, `401`, `403` |
| `UpdateContact` | `UpdateContact` | `204`, `400`, `401`, `403`, `404` |
| `UpdateContactVisibility` | `UpdateContactVisibility` | `204`, `400`, `401`, `403`, `404` |
| `GetContactsByEnterpriseForSelect` | `GetContactsByEnterpriseForSelect` | `200`, `400`, `401` |
| `GetContactsForSelect` | `GetContactsForSelect` | `200`, `401`, `403` |

**Derivación:**
- `201/204/200` son los códigos de éxito de RF-2.5, RF-5.5, RF-6.5, RF-3.2, RF-4.2, RF-7.2, RF-10.1.
- `401` aplica a **todos** por RF-1.2.
- `403` aplica a `GetContactsByEnterprise`, `GetContactsForSelect`, `UpdateContact` y `UpdateContactVisibility` por RF-1.7 y RF-4.7.
- `400` aplica a todos los que validan `Id`/`enterpriseId` numérico (RF-3.4, RF-4.6, RF-5.6, RF-6.6, RF-7.6), reglas de negocio de entidad (RF-2.3, RF-5.4), `Visibility` (RF-6.3) o DTO inválido (RF-8.2/8.3). `GetContactsForSelect` no recibe parámetros de entrada, por lo que no documenta `400`.
- `404` aplica a los que buscan por `Id` (RF-3.3, RF-5.3, RF-6.4).
- `500` aplica solo a `InsertContact` por violación de integridad referencial (RF-2.4).

### RF-10 — `GetContactsForSelect` (GET `/contactsct/`)

Obtiene el listado reducido global de contactos del sistema (algunos registros de contacto, con la proyección mínima que expone el puerto secundario de selección). No recibe parámetros de entrada. **Solo accesible para administrador.**

| # | Criterio de aceptación |
|---|------------------------|
| RF-10.1 | Cuando un administrador autenticado invoque `GET /contactsct/`, el sistema devolverá con el código `200` el conjunto de contactos habilitados (`Visibility = ENABLED`) de todo el sistema, con la proyección reducida del puerto secundario de selección: cada registro contiene `Id` y `FullName`. |
| RF-10.2 | El conjunto devuelto **no incluirá** ningún registro con `Visibility = DISABLED`; el filtro `Visibility = ENABLED` vive en la consulta del puerto secundario (intocable). |
| RF-10.3 | `EnterpriseId` y `Visibility` **no viajan** en la respuesta; el consumidor no debe asumir su presencia. |
| RF-10.4 | Si no existe ningún contacto habilitado registrado en el sistema, entonces el sistema devolverá un conjunto vacío con el código `200`. |
| RF-10.5 | Si la sesión no tiene rol `admin`, entonces el sistema responderá con el código `403` y no ejecutará ninguna función del endpoint. |
| RF-10.6 | Si la petición no porta sesión válida o la sesión ha caducado (30 minutos), entonces el sistema responderá con el código `401` (RF-1.2). |

---

## 5. Requisitos no funcionales

| # | Requisito | Criterio de aceptación |
|---|-----------|------------------------|
| RNF-1 | Esquema por capas | La especificación respeta la separación entre dominio, aplicación, datos y repositorio establecida en `docs/constitution.md`. |
| RNF-2 | Puertos y adaptadores | Los casos de uso se comunican con la persistencia únicamente a través de los puertos de la capa de aplicación; ningún caso de uso accede directamente al mecanismo de almacenamiento. |
| RNF-3 | Inyección de dependencias | Los componentes se registran con ámbito de vida por petición (`scoped`). |
| RNF-4 | Plataforma | El sistema opera sobre .NET 10.0 usando únicamente biblioteca estándar. |
| RNF-5 | Persistencia | La persistencia de contactos se realiza mediante Entity Framework Core. |
| RNF-6 | Idioma | Los identificadores y los comentarios del código están en inglés; los mensajes destinados al usuario final están en español. |
| RNF-7 | Consistencia de datos | Cuando una operación de escritura se complete, el estado almacenado debe corresponder a los datos enviados por el consumidor de la API. |
| RNF-8 | Ausencia de filtración de secretos | Ninguna respuesta de la API debe exponer datos sensibles. |
| RNF-9 | Compatibilidad | El contrato de los siete endpoints no cambia de forma incompatible dentro de este caso de uso. |
| RNF-10 | Sin dependencias nuevas | No se añade ningún paquete NuGet fuera de los ya autorizados en la spec 001 (solo `Microsoft.AspNetCore.Authentication.JwtBearer` y EF Core). |

---

## 6. Casos límite

| # | Situación | Comportamiento esperado |
|---|-----------|------------------------|
| CE-1 | El `Id` del cuerpo de `PUT /contact/{id}` o `PUT /contactv/{id}` difiere del `Id` de la ruta. | Prevalece siempre el `Id` de la ruta. |
| CE-2 | `PUT /contactv/{id}` incluye propiedades ajenas a `Id` y `Visibility` (p. ej. `FullName`, `EnterpriseId`). | Esas propiedades se ignoran y no producen efecto sobre el contacto. |
| CE-3 | Se solicita un contacto inexistente (`GetContact`, `UpdateContact`, `UpdateContactVisibility`). | `404`. |
| CE-5 | Se intenta actualizar un contacto inexistente. | `404` y no se realiza ninguna modificación. |
| CE-6 | Se solicitan contactos de una empresa y no hay ninguno registrado, o el `enterpriseId` no corresponde a ninguna empresa registrada. | `200` con conjunto vacío (la consulta devuelve cero registros; no hay error que gestionar). |
| CE-6b | Existe al menos un contacto deshabilitado y se invoca `GET /contactsent/{enterpriseId}`. | El conjunto devuelto **incluye** el registro con `Visibility = DISABLED` (RF-4.3). |
| CE-6c | Existe al menos un contacto deshabilitado y se invoca `GET /contactsentsct/{enterpriseId}`. | El conjunto devuelto **no incluye** el registro con `Visibility = DISABLED` (RF-7.3). |
| CE-7 | Se invocan los siete endpoints sin sesión o con sesión expirada. | `401` y no se ejecuta ninguna función del endpoint. |
| CE-8 | Se invocan `GetContactsByEnterprise`, `GetContactsForSelect`, `UpdateContact` o `UpdateContactVisibility` con una sesión de rol distinto de `admin`. | `403` y no se ejecuta ninguna función del endpoint. |
| CE-9 | Se invocan los endpoints que exigen `admin` con sesión cuyo `Role` de los `claims` está ausente, alterado o manipulado (la sesión no ha caducado). | `403`. La caducidad de la sesión se trata aparte en CE-14 con `401`. |
| CE-10 | `GetContact` devuelve un contacto con `Visibility = DISABLED`. | `200` con el contacto (no se filtra por visibilidad en consulta individual; `404` solo para ID inexistente). |
| CE-11 | El DTO de `InsertContact` (o el campo editable `FullName` del DTO de `UpdateContact`) viola una regla de negocio (`EnterpriseId` < 1 o `FullName` fuera de 10–150 caracteres). | `400` y no se escribe ningún dato. |
| CE-12 | El DTO de `UpdateContactVisibility` envía `Visibility` distinto de `ENABLED` o `DISABLED`. | `400` y no se realiza ninguna modificación. |
| CE-13 | Cualquiera de las rutas recibe un `Id` o `enterpriseId` no numérico, o numérico con valor cero o negativo. | `400` y no se ejecuta ninguna función del endpoint. |
| CE-14 | La sesión del consumidor ha vencido su vigencia (30 min desde emisión, sin renovación). | `401` en los siete endpoints. |
| CE-15 | Se emite un error en cualquiera de los siete endpoints. | El mensaje al usuario se entrega en español. |
| CE-16 | `POST /contact/` envía un `EnterpriseId` que no corresponde a ninguna empresa registrada. | `500` y no se crea el contacto (integridad referencial; RF-2.4). |
| CE-17 | Se invoca `InsertContact`, `GetContact` o `GetContactsByEnterpriseForSelect` con sesión de rol distinto de `admin`. | Se ejecuta normalmente (solo se exige sesión válida; RF-1.6). |
| CE-18 | Se invoca `GET /contactsct/` y no existe ningún contacto habilitado en el sistema. | `200` con conjunto vacío (RF-10.4). |

---

## 7. Fuera de alcance

- Persistencia de las órdenes de servicio (bitácoras) y de cualquier entidad distinta del contacto.
- Edición del contenido de la orden de servicio.
- Asignación de órdenes de servicio a contactos (la bitácora referencia obligatoriamente un contacto, pero esa lógica corresponde al caso de uso Bitácora).
- El registro del **contacto inicial** junto con la creación de la empresa (perteneciente a la spec 003, `InsertEnterprise`).
- Recuperación, restablecimiento o envío de contraseñas por correo.
- Esquema de autorización distinto del binario administrador / no administrador.
- Autenticación por proveedores externos, correo, SMS o segundo factor.
- Auditoría de los cambios realizados sobre los contactos.
- Versionado y publicación del contrato de la API más allá de RNF-9.
- Pruebas de rendimiento, de carga y de penetración.
- Modificación de `hexArch/repository` (prohibido por el principio 5 de la Constitución).
- Verificación de campos anulables del DTO (p. ej. `EnterpriseId`, `FullName` o `Visibility` nulos): las entidades son clases con propiedades anulables para adaptarse a la flexibilidad de los DTOs y dicha comprobación se realiza en capas más externas del MVP.
- Recorte (trim) de espacios en blanco de `FullName`: la entidad solo exige una longitud mínima de 10 y máxima de 150 caracteres; el recorte ocurre en capas más externas.
- Escritura directa del string de `Visibility` por parte del usuario: los adaptadores de puerto secundario lo generan automáticamente y en la interfaz el usuario solo elige entre opciones predefinidas que no puede alterar.
- Modificación de `Id` o `EnterpriseId` de un contacto por parte del usuario: en un `UpdateContact` solo es editable `FullName`; la prohibición se impone en capas más externas.

---

## 8. Criterios de finalización

El caso de uso Contacto se considera concluido cuando:

1. Los siete endpoints de la sección 4 están disponibles y responden conforme a su criterio de aceptación.
2. Los siete endpoints rechazan con `401` las peticiones sin sesión válida o con sesión vencida.
3. `GetContactsByEnterprise`, `GetContactsForSelect`, `UpdateContact` y `UpdateContactVisibility` rechazan con `403` las peticiones cuyo `Role` de los `claims` no sea `admin` (incluido el `Role` ausente, alterado o manipulado), sin ejecutar ninguna función del endpoint.
4. `InsertContact`, `GetContact` y `GetContactsByEnterpriseForSelect` aceptan usuario autenticado (no exigen `admin`).
5. Un `Id` o `enterpriseId` de ruta no numérico, o numérico con valor cero o negativo, se rechaza con `400` en todos los endpoints que lo reciben.
6. La consulta `GET /contact/{id}` no tiene cuerpo: el handler solo asigna al accesor `Id` del `ContactDTO` el valor numérico del parámetro de ruta. En `UpdateContact` y `UpdateContactVisibility` el `Id` de la ruta prevalece sobre el `Id` del cuerpo.
7. `UpdateContactVisibility` aplica únicamente el campo `Visibility` e ignora cualquier otra propiedad del DTO recibido.
8. `GetContactsByEnterprise` devuelve **todos** los contactos de la empresa (incluye `Visibility = DISABLED`).
9. `GetContactsByEnterpriseForSelect` devuelve **solo** contactos con `Visibility = ENABLED`.
10. `GetContact` devuelve el contacto aunque tenga `Visibility = DISABLED` (no filtra en consulta individual; `404` solo para ID inexistente) y su respuesta no incluye el campo `Visibility`.
11. Los orígenes de fallo de la sección 4 se traducen a `404`, `400` o `500` según corresponda, con mensajes en español.
12. `InsertContact` responde `201` sin cuerpo ni cabecera `Location`; `UpdateContact` y `UpdateContactVisibility` responden `204` sin cuerpo; `GetContact`, `GetContactsByEnterprise`, `GetContactsByEnterpriseForSelect` y `GetContactsForSelect` responden `200` con el DTO o colección correspondiente.
13. `GetContactsForSelect` devuelve solo contactos con `Visibility = ENABLED`, con la proyección reducida (`Id`, `FullName`) y sin `EnterpriseId` ni `Visibility` en la respuesta.
14. Cada endpoint cuenta con `WithName(...)` usando exactamente los nombres de la tabla de resumen y `Produces(...)` para cada código de respuesta posible.
15. El proyecto compila sin errores y todas las pruebas pasan (`dotnet test` verde).
16. Cada endpoint cuenta con su prueba automatizada y con la prueba de su caso límite y su caso de error, según `docs/constitution.md`; cada prueba crea sus propios datos.

---

## 9. Dudas abiertas

- Ninguna.
