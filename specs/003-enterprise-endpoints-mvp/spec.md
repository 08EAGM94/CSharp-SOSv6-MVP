# Especificación — Caso de Uso: Empresa

- **ID de spec:** `003-enterprise-endpoints-mvp`
- **Estado:** implementada y validada.
- **Constitución aplicable:** `docs/constitution.md`
- **Alcance de este documento:** QUÉ se construye y POR QUÉ. Las decisiones de implementación (mecanismo concreto de sesión, estructura del manejo de errores, estrategia de pruebas) corresponden al plan, no a esta spec.

---

## 1. Contexto y objetivo

El sistema SOS v6 administra **bitácoras** (órdenes de servicio registradas en campo). Cada bitácora referencia obligatoriamente un **contacto** (`ContactoId` obligatorio) y un contacto pertenece a una **empresa** (`EmpresaId` obligatorio). Esa cadena —**Empresa → Contacto → Bitácora**— hace que la empresa y el contacto sean la **información del cliente que solicitó el servicio**: la empresa identifica a la organización cliente y el contacto a la persona de referencia dentro de esa organización.

Esta spec define el **caso de uso Empresa**: la capacidad de registrar (junto con su contacto inicial), consultar, listar, actualizar y cambiar la visibilidad de las empresas, además de obtener un listado reducido para *selects* de la interfaz.

El objetivo es que **cualquier usuario autenticado** pueda consultar una empresa individual (`GetEnterprise`), crear una empresa con su contacto inicial (`InsertEnterprise`) y obtener el listado reducido para *selects* (`GetEnterprisesForSelects`), y que **únicamente un administrador autenticado** pueda **gobernar** dicho catálogo (consultar el listado completo mediante `GetEnterprises`, modificar y habilitar/deshabilitar empresas mediante `UpdateEnterprise` y `UpdateEnterpriseVisibility`).

**Por qué este caso de uso va después de Usuario y Tipo:** el control de acceso a los endpoints de Empresa depende de la sesión y el rol establecidos por el caso de uso Usuario (spec 001), y las empresas clasifican a los contactos que a su vez se usan en las bitácoras junto con los tipos (spec 002).

---

## 2. Usuarios

| Actor                          | Descripción                                                                                  | Puede hacer                                                                                                                                      |
| ------------------------------ | --------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------ |
| **Administrador**        | Usuario con rol`admin`. Único autorizado a gobernar el catálogo de empresas.              | Iniciar sesión (vía caso de uso Usuario), crear (empresa + contacto inicial), consultar, listar, actualizar y cambiar visibilidad de empresas. |
| **Usuario operativo**    | Usuario con un rol distinto de`admin`. Existe, puede autenticarse y consultar el catálogo. | Iniciar sesión, consultar una empresa, listar empresas para selects.                                                                            |
| **Consumidor de la API** | Cliente (aplicación o herramienta) que invoca los endpoints de esta spec.                    | Invocar los seis endpoints respetando el contrato de cada uno.                                                                                   |

---

## 3. Historias de usuario

### HU-1 — Gobernar el catálogo de empresas (y contacto inicial)

**Como** administrador del sistema, **quiero** registrar una empresa junto con su contacto inicial, consultar, listar, actualizar y cambiar la visibilidad de las empresas, **para** mantener al día la información de los clientes que solicitan los servicios; una empresa o contacto mal clasificados o deshabilitados indebidamente impactan en la trazabilidad de las bitácoras (órdenes de servicio) vinculadas a través de la cadena Empresa → Contacto → Bitácora.

### HU-2 — Consultar el catálogo de empresas

**Como** usuario operativo del sistema, **quiero** consultar una empresa por su identificador y obtener la lista reducida para selects, **para** poder seleccionar correctamente la empresa (y su contacto) al registrar una bitácora (orden de servicio), ya que la bitácora exige un contacto obligatorio que pertenece a una empresa, y el select de la interfaz se alimenta del endpoint reducido.

### HU-3 — Proteger el catálogo de empresas

**Como** administrador del sistema, **quiero** que las operaciones de escritura sobre el catálogo (**actualizar y cambiar visibilidad**) estén restringidas a usuarios con rol `admin`, **para** que ningún otro usuario pueda alterar la información de las empresas, lo cual repercute en los contactos que las usan y en las bitácoras vinculadas a esos contactos. La creación de empresas (`InsertEnterprise`) es accesible para cualquier usuario autenticado (permite registrar el cliente y su contacto de referencia en una sola transacción atómica).

---

## 4. Requisitos funcionales

Los criterios de aceptación usan notación EARS en español:

- **Evento:** "Cuando \<evento\>, el sistema \<respuesta\>."
- **Comportamiento no deseado:** "Si \<condición\>, entonces el sistema \<respuesta\>."
- **Estado:** "Mientras \<estado\>, el sistema \<respuesta\>."
- **Característica opcional:** "Donde \<característica\>, el sistema \<respuesta\>."

### Resumen de endpoints

| Método | Ruta                  | Nombre del endpoint (WithName) | Caso de uso                                        | DTO y propiedades usadas                                                                                                                                                                |
| ------- | --------------------- | ------------------------------ | -------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| POST    | `/enterprise/`      | `InsertEnterprise`           | `CommonService<EnterpriseEntity, EnterpriseDTO>` | Un único objeto que contiene`EnterpriseDTO` y, opcionalmente, `ContactDTO` (`FullName`, `Visibility` se fija a ENABLED)                                                        |
| GET     | `/enterprise/{id}`  | `GetEnterprise`              | `CommonService<EnterpriseEntity, EnterpriseDTO>` | DTO de entrada: solo`Id` (tomado del parámetro de ruta); **respuesta: `EnterpriseDTO` completo**                                                                             |
| GET     | `/enterprises/`     | `GetEnterprises`             | `CommonService<EnterpriseEntity, EnterpriseDTO>` | ninguno (devuelve`IEnumerable<EnterpriseDTO>` con `Id`, `CommercialName` y `TradeName`; el listado incluye empresas deshabilitadas y `Visibility` no viaja en la respuesta)   |
| PUT     | `/enterprise/{id}`  | `UpdateEnterprise`           | `CommonService<EnterpriseEntity, EnterpriseDTO>` | `EnterpriseDTO` con `Id` tomado del parámetro de ruta y resto de propiedades                                                                                                       |
| PUT     | `/enterprisev/{id}` | `UpdateEnterpriseVisibility` | `CommonService<EnterpriseEntity, EnterpriseDTO>` | `EnterpriseDTO` con `Id` de la ruta y `Visibility`                                                                                                                                |
| GET     | `/enterprisesct/`   | `GetEnterprisesForSelects`   | `SelectService<EnterpriseDTO>`                   | ninguno (devuelve`IEnumerable<EnterpriseDTO>` con `Id`, `CommercialName` y `TradeName`; el filtro es por `Visibility = ENABLED` pero `Visibility` no viaja en la respuesta) |

### RF-1 — Control de acceso previo a toda operación protegida

Todos los endpoints de esta spec **exigen sesión válida**.

| #      | Criterio de aceptación                                                                                                                                                                                                                                                                                                                                                                                |
| ------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| RF-1.1 | Cuando una petición se dirige a cualquiera de los seis endpoints, el sistema verificará primero que la petición porta una sesión válida.                                                                                                                                                                                                                                                          |
| RF-1.2 | Si una petición no porta sesión válida o su sesión ha expirado, entonces el sistema responderá con el código`401` y no ejecutará ninguna función del endpoint.                                                                                                                                                                                                                               |
| RF-1.3 | Donde exista una sesión válida, el sistema comprobará**antes de ejecutar cualquier otra acción** que el rol de esa sesión sea `admin` **para los endpoints `GetEnterprises`, `UpdateEnterprise` y `UpdateEnterpriseVisibility`**. Ese rol es el leído de los `claims` emitidos en el login (spec 001) y es la única fuente que decide si el flujo del endpoint continúa. |
| RF-1.4 | Si el rol de la sesión no es`admin` **y el endpoint es `GetEnterprises`, `UpdateEnterprise` o `UpdateEnterpriseVisibility`**, entonces el sistema responderá con el código `403` y no ejecutará ninguna función vital del endpoint.                                                                                                                                               |
| RF-1.6 | Para los endpoints`InsertEnterprise`, `GetEnterprise` y `GetEnterprisesForSelects`, **basta con una sesión válida (usuario autenticado)**; no se verifica el rol `admin`.                                                                                                                                                                                                              |
| RF-1.7 | Ningún endpoint de esta spec es público: todos exigen sesión válida. No existe equivalente a`login` en este caso de uso.                                                                                                                                                                                                                                                                         |

### RF-2 — `InsertEnterprise` (POST `/enterprise/`)

Registra una empresa nueva y, opcionalmente, su contacto inicial. El caso de uso fija `Visibility = "ENABLED"` al crear ambos. La creación es atómica: si falla el contacto, se revierte la empresa.

| #      | Criterio de aceptación                                                                                                                                                                                                                                                                                                                                                                                                                                        |
| ------ | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| RF-2.1 | Cuando un usuario autenticado invoque`POST /enterprise/`, el sistema registrará una empresa a partir de la parte `EnterpriseDTO` contenida en el único objeto del cuerpo de la petición.                                                                                                                                                                                                                                                                |
| RF-2.2 | Si el único objeto del cuerpo**opcionalmente** incluye una parte `ContactDTO` **con `FullName` informado**, el sistema creará también el contacto asociado a la empresa recién creada, usando la misma transacción. Si la parte `ContactDTO` no está presente, o llega sin `FullName` o con `FullName` en blanco, el contacto se ignora y solo se crea la empresa (ver RF-2.9).                                                    |
| RF-2.3 | El caso de uso asigna`Visibility = "ENABLED"` al nuevo registro de empresa y al de contacto (si se crea); el valor de `Visibility` enviado en la parte de empresa del cuerpo (si lo hay) es ignorado.                                                                                                                                                                                                                                                      |
| RF-2.4 | Si el`EnterpriseDTO` no cumple las reglas de negocio de la empresa (`CommercialName` máx. 150, `TradeName` máx. 150, `StreetNumber` máx. 50, `BetweenStreets` máx. 150, `ContactingWith` máx. 150, `Phones` máx. 150, `Schedule` máx. 150, `Atention` máx. 150, `Neighborhood` máx. 50, `Location` máx. 50, `Email` máx. 150), entonces el sistema responderá con el código `400` (→ RF-8.2) y no creará el registro. |
| RF-2.5 | Si el único objeto del cuerpo**opcionalmente** incluye una parte `ContactDTO` **con `FullName` informado** y este no cumple las reglas de negocio (longitud entre 10 y 150 caracteres), entonces el sistema responderá con el código `400` (→ RF-8.2) y no creará ni la empresa ni el contacto.                                                                                                                                         |
| RF-2.6 | Cuando el registro se complete, el sistema responderá con el código`201` y sin cuerpo: la operación de alta no produce ningún objeto de retorno.                                                                                                                                                                                                                                                                                                         |
| RF-2.7 | La creación no expone en la respuesta ninguna referencia al recurso creado: la respuesta se limita al código`201` y no incluye cuerpo ni cabecera `Location`.                                                                                                                                                                                                                                                                                            |
| RF-2.8 | Si el motor de persistencia genera un conflicto de unicidad (índice único sobre campos de empresa o contacto según modelo de datos), el sistema responderá con el código`500` y no creará ningún registro.                                                                                                                                                                                                                                            |
| RF-2.9 | Si el único objeto del cuerpo incluye una parte`ContactDTO` sin `FullName` o con `FullName` vacío o compuesto solo de espacios en blanco, entonces el sistema ignorará ese contacto y creará únicamente la empresa, respondiendo con `201`.                                                                                                                                                                                                       |

### RF-3 — `GetEnterprise` (GET `/enterprise/{id}`)

Obtiene un registro de empresa. **No filtra por visibilidad**: devuelve `200` con el registro aunque su `Visibility` sea `DISABLED` (opción A, coherente con specs 001 y 002). El `404` queda reservado exclusivamente a identificadores inexistentes.

| #      | Criterio de aceptación                                                                                                                                                                                |
| ------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| RF-3.1 | Cuando un usuario autenticado invoque`GET /enterprise/{id}`, el sistema sustituye el `Id` del `EnterpriseDTO` por el valor del parámetro de ruta.                                               |
| RF-3.2 | Cuando el DTO tenga asignado el`Id` del parámetro de ruta, el sistema devolverá el registro de empresa correspondiente a ese identificador (`EnterpriseDTO` completo con todas sus propiedades). |
| RF-3.3 | Si el identificador no corresponde a ninguna empresa, entonces el sistema responderá con el código`404`.                                                                                           |
| RF-3.4 | Si el`Id` de la ruta no es un valor numérico válido, entonces el sistema responderá con el código `400` y no consultará ningún registro.                                                     |
| RF-3.5 | Si la empresa existe pero tiene`Visibility = DISABLED`, el sistema devolverá `200` con el `EnterpriseDTO` completo; no responderá `404`.                                                     |

### RF-4 — `GetEnterprises` (GET `/enterprises/`)

Obtiene **todos** los registros de empresas, **incluyendo los deshabilitados**. Esta decisión es coherente con la consulta de listado del catálogo, que no aplica filtro de visibilidad: el listado completo sirve para gobernar el catálogo de clientes, de ahí que exista un endpoint dedicado (`GetEnterprisesForSelects`) con solo las habilitadas para los selects. **Este endpoint es accesible únicamente para administradores autenticados.**

| #      | Criterio de aceptación                                                                                                                                                                                                           |
| ------ | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| RF-4.1 | Cuando un**administrador autenticado** invoque `GET /enterprises/`, el sistema devolverá el conjunto de **todos** los registros de empresas, independientemente de su `Visibility`.                              |
| RF-4.2 | El conjunto devuelto**incluirá** registros con `Visibility = DISABLED`, con independencia de su estado de visibilidad en el almacenamiento.                                                                              |
| RF-4.3 | Si no existe ninguna empresa registrada, entonces el sistema devolverá un conjunto vacío con el código`200`.                                                                                                                 |
| RF-4.4 | La respuesta contiene únicamente`Id`, `CommercialName` y `TradeName` por cada empresa. Las propiedades no incluidas en este listado llegarán como `null` en el DTO y el consumidor no debe asumir que están presentes. |
| RF-4.5 | Si un usuario autenticado**sin rol `admin`** invoque `GET /enterprises/`, el sistema responderá con el código `403` y no ejecutará ninguna función del endpoint.                                                  |

### RF-5 — `UpdateEnterprise` (PUT `/enterprise/{id}`)

Actualiza una empresa. Solo accesible para administrador.

| #      | Criterio de aceptación                                                                                                                                                                                                                                                                                                                                                                                                                                        |
| ------ | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| RF-5.1 | Cuando un administrador autenticado invoque`PUT /enterprise/{id}`, el sistema sustituye el `Id` del `EnterpriseDTO` por el valor del parámetro de ruta, con independencia del `Id` que venga en el cuerpo de la petición.                                                                                                                                                                                                                            |
| RF-5.2 | Cuando el DTO tenga asignado el`Id` del parámetro de ruta, el sistema actualizará la empresa correspondiente a ese identificador con los datos del DTO (todas las propiedades de `EnterpriseDTO` excepto `Id` y `Visibility`, que se gestiona en endpoint separado).                                                                                                                                                                                 |
| RF-5.3 | Si el identificador no corresponde a ninguna empresa, entonces el sistema responderá con el código`404` y no realizará ninguna modificación.                                                                                                                                                                                                                                                                                                             |
| RF-5.4 | Si el DTO no cumple las reglas de negocio de la empresa (`CommercialName` máx. 150, `TradeName` máx. 150, `StreetNumber` máx. 50, `BetweenStreets` máx. 150, `ContactingWith` máx. 150, `Phones` máx. 150, `Schedule` máx. 150, `Atention` máx. 150, `Neighborhood` máx. 50, `Location` máx. 50, `Email` máx. 150), entonces el sistema responderá con el código `400` (→ RF-8.2) y no realizará ninguna modificación. |
| RF-5.5 | Cuando la actualización se complete, el sistema responderá con el código`204` y sin cuerpo: la operación de actualización no produce ningún objeto de retorno.                                                                                                                                                                                                                                                                                         |
| RF-5.6 | Si el`Id` de la ruta no es un valor numérico válido, entonces el sistema responderá con el código `400` y no realizará ninguna modificación.                                                                                                                                                                                                                                                                                                         |

### RF-6 — `UpdateEnterpriseVisibility` (PUT `/enterprisev/{id}`)

Actualiza **únicamente** el campo `Visibility` de una empresa. Solo accesible para administrador.

| #      | Criterio de aceptación                                                                                                                                                                                                |
| ------ | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| RF-6.1 | Cuando un administrador autenticado invoque`PUT /enterprisev/{id}`, el sistema sustituye el `Id` del `EnterpriseDTO` por el valor del parámetro de ruta.                                                        |
| RF-6.2 | Cuando el endpoint actualice el campo`Visibility`, el sistema tomará del DTO **únicamente** las propiedades `Id` y `Visibility`; cualquier otra propiedad del DTO será ignorada.                        |
| RF-6.3 | Si el valor recibido en`Visibility` no es uno de los admitidos por las reglas de negocio (`ENABLED` o `DISABLED`), entonces el sistema responderá con el código `400` y no realizará ninguna modificación. |
| RF-6.4 | Si el identificador no corresponde a ninguna empresa, entonces el sistema responderá con el código`404` y no realizará ninguna modificación.                                                                     |
| RF-6.5 | Cuando la modificación se complete, el sistema responderá con el código`204` y sin cuerpo: la operación de actualización de visibilidad no produce ningún objeto de retorno.                                   |
| RF-6.6 | Si el`Id` de la ruta no es un valor numérico válido, entonces el sistema responderá con el código `400` y no realizará ninguna modificación.                                                                 |

### RF-7 — `GetEnterprisesForSelects` (GET `/enterprisesct/`)

Obtiene los registros de empresas **habilitados únicamente** (`Visibility = ENABLED`), filtra por `Visibility = "ENABLED"` y devuelve `Id`, `CommercialName`, `TradeName`.

| #      | Criterio de aceptación                                                                                                                                                                                                                                                                                                                                              |
| ------ | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| RF-7.1 | Cuando un usuario autenticado invoque`GET /enterprisesct/`, el sistema devolverá el conjunto de registros de empresas con `Visibility = ENABLED`.                                                                                                                                                                                                               |
| RF-7.2 | El conjunto devuelto**no incluirá** ningún registro con `Visibility = DISABLED`, con independencia de su estado de visibilidad en el almacenamiento.                                                                                                                                                                                                       |
| RF-7.3 | Si no existe ninguna empresa habilitada registrada, entonces el sistema devolverá un conjunto vacío con el código`200`.                                                                                                                                                                                                                                         |
| RF-7.4 | La respuesta contiene únicamente`Id`, `CommercialName` y `TradeName` por cada empresa. El filtro `Visibility = ENABLED` se aplica en la consulta, pero el campo `Visibility` **no** se incluye en la respuesta. Las propiedades no incluidas en este listado llegarán como `null` en el DTO y el consumidor no debe asumir que están presentes. |

### RF-8 — Contrato de errores

Todo fallo de negocio producido por un caso de uso se traduce a un código HTTP y a un mensaje al usuario final.

| #      | Criterio de aceptación                                                                                                                                                                                             |
| ------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| RF-8.1 | Si un caso de uso falla por registro no encontrado (`KeyNotFoundException` del repositorio), entonces el sistema responderá con el código `404`.                                                              |
| RF-8.2 | Si un caso de uso falla por una violación de reglas de negocio de la entidad (`EntityException`), entonces el sistema responderá con el código `400`.                                                        |
| RF-8.3 | Si un caso de uso falla por una violación de reglas de negocio de la aplicación (`ApplicationException`, p. ej. `Visibility` inválido o `Id` < 1), entonces el sistema responderá con el código `400`. |
| RF-8.4 | Si un caso de uso falla por cualquier otra causa de negocio, entonces el sistema responderá con el código`400`.                                                                                                 |
| RF-8.5 | Si un caso de uso falla por un conflicto de unicidad emitido por el motor de persistencia, entonces el sistema responderá con el código`500`.                                                                   |
| RF-8.6 | Donde la API produzca un mensaje de error, el mensaje será redactado en español.                                                                                                                                  |

### RF-9 — Documentación del contrato (OpenAPI / Swagger)

Los seis endpoints deben exponer su contrato de respuestas documentado y su nombre público.

| #      | Criterio de aceptación                                                                                                                                                                                                                                                                 |
| ------ | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| RF-9.1 | Cada endpoint usará`WithName(...)` con **exactamente** el nombre indicado en la tabla de la sección "Resumen de endpoints" (`InsertEnterprise`, `GetEnterprise`, `GetEnterprises`, `UpdateEnterprise`, `UpdateEnterpriseVisibility`, `GetEnterprisesForSelects`). |
| RF-9.2 | Cada endpoint usará`Produces(...)` para documentar **cada** código de respuesta que pueda emitir según sus RF. La tabla siguiente lista los códigos **exactos** que cada endpoint debe documentar, deducidos de RF-1 a RF-8.                                          |
| RF-9.3 | Los nombres y códigos documentados coincidirán con los definidos en los RF-2 a RF-7 y RF-1.                                                                                                                                                                                           |

#### Tabla de códigos `Produces(...)` por endpoint (verificable)

| Endpoint                       | WithName(...)                  | Códigos Produces(...)                      |
| ------------------------------ | ------------------------------ | ------------------------------------------- |
| `InsertEnterprise`           | `InsertEnterprise`           | `201`, `400`, `401`, `500`          |
| `GetEnterprise`              | `GetEnterprise`              | `200`, `400`, `401`, `404`          |
| `GetEnterprises`             | `GetEnterprises`             | `200`, `401`, `403`                   |
| `UpdateEnterprise`           | `UpdateEnterprise`           | `204`, `400`, `401`, `403`, `404` |
| `UpdateEnterpriseVisibility` | `UpdateEnterpriseVisibility` | `204`, `400`, `401`, `403`, `404` |
| `GetEnterprisesForSelects`   | `GetEnterprisesForSelects`   | `200`, `401`                            |

**Derivación:**

- `201/204/200` son los códigos de éxito de RF-2.6, RF-5.5, RF-6.5, RF-3.2, RF-4.1, RF-7.1.
- `401` aplica a **todos** por RF-1.2.
- `403` aplica a `GetEnterprises`, `UpdateEnterprise` y `UpdateEnterpriseVisibility` por RF-1.4.
- `400` aplica a todos los que validan `Id` numérico (RF-3.4, RF-5.6, RF-6.6), reglas de negocio de entidad (RF-2.4, RF-2.5, RF-5.4), `Visibility` (RF-6.3) o DTO inválido (RF-8.2/8.3).
- `404` aplica a los que buscan por `Id` (RF-3.3, RF-5.3, RF-6.4).
- `500` aplica solo a `InsertEnterprise` por conflicto de unicidad (RF-2.8).

---

## 5. Requisitos no funcionales

| #      | Requisito                           | Criterio de aceptación                                                                                                                                                                                                                                                                                                                                                                                               |
| ------ | ----------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| RNF-1  | Esquema por capas                   | La especificación respeta la separación entre dominio, aplicación, datos y repositorio establecida en`docs/constitution.md`.                                                                                                                                                                                                                                                                                     |
| RNF-2  | Puertos y adaptadores               | Los casos de uso se comunican con la persistencia únicamente a través de los puertos de la capa de aplicación; ningún caso de uso accede directamente al mecanismo de almacenamiento.                                                                                                                                                                                                                             |
| RNF-3  | Inyección de dependencias          | Los componentes se registran con ámbito de vida por petición (`scoped`).                                                                                                                                                                                                                                                                                                                                          |
| RNF-4  | Plataforma                          | El sistema opera sobre .NET 10.0 usando únicamente biblioteca estándar.                                                                                                                                                                                                                                                                                                                                             |
| RNF-5  | Persistencia                        | La persistencia de empresas se realiza mediante Entity Framework Core.                                                                                                                                                                                                                                                                                                                                                |
| RNF-6  | Idioma                              | Los identificadores y los comentarios del código están en inglés; los mensajes destinados al usuario final están en español.                                                                                                                                                                                                                                                                                     |
| RNF-7  | Consistencia de datos               | Cuando una operación de escritura se complete, el estado almacenado debe corresponder a los datos enviados por el consumidor de la API.                                                                                                                                                                                                                                                                              |
| RNF-8  | Ausencia de filtración de secretos | Ninguna respuesta de la API debe exponer datos sensibles.                                                                                                                                                                                                                                                                                                                                                             |
| RNF-9  | Compatibilidad                      | El contrato de los seis endpoints no cambia de forma incompatible dentro de este caso de uso.                                                                                                                                                                                                                                                                                                                         |
| RNF-10 | Sin dependencias nuevas             | No se añade ningún paquete NuGet fuera de los ya autorizados en la spec 001 (solo`Microsoft.AspNetCore.Authentication.JwtBearer` y EF Core).                                                                                                                                                                                                                                                                      |
| RNF-11 | Transaccionalidad en creación      | `InsertEnterprise` debe ejecutar la creación de empresa y contacto (si se provee) en una única transacción atómica; si falla cualquiera, se revierte todo. La garantía transaccional reside en la capa de persistencia (repositorio intocable, principio 5 de la Constitución); este requisito se verifica en el alcance de las capas de aplicación y API (propagación de fallo → `500` sin reintentos). |

---

## 6. Casos límite

| #     | Situación                                                                                                                                | Comportamiento esperado                                                                                                                                                                                                                                                                                                          |
| ----- | ----------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| CE-1  | El`Id` del cuerpo de `PUT /enterprise/{id}` o `PUT /enterprisev/{id}` difiere del `Id` de la ruta.                                | Prevalece siempre el`Id` de la ruta.                                                                                                                                                                                                                                                                                           |
| CE-2  | `PUT /enterprisev/{id}` incluye propiedades ajenas a `Id` y `Visibility` (p. ej. `CommercialName`).                               | Esas propiedades se ignoran y no producen efecto sobre la empresa.                                                                                                                                                                                                                                                               |
| CE-3  | Se solicita una empresa inexistente (`GetEnterprise`, `UpdateEnterprise`, `UpdateEnterpriseVisibility`).                            | `404`.                                                                                                                                                                                                                                                                                                                         |
| CE-4  | Se intenta crear una empresa cuyo índice único provoque conflicto en el motor de persistencia.                                          | `500` y no se duplica el registro.                                                                                                                                                                                                                                                                                             |
| CE-5  | Se intenta actualizar una empresa inexistente.                                                                                            | `404` y no se realiza ninguna modificación.                                                                                                                                                                                                                                                                                   |
| CE-6  | Se solicitan empresas y no hay ninguna registrada.                                                                                        | `200` con conjunto vacío.                                                                                                                                                                                                                                                                                                     |
| CE-6b | Existe al menos una empresa deshabilitada y se invoca`GET /enterprises/`.                                                               | El conjunto devuelto**incluye** el registro con `Visibility = DISABLED` (RF-4.2).                                                                                                                                                                                                                                        |
| CE-6c | Existe al menos una empresa deshabilitada y se invoca`GET /enterprisesct/`.                                                             | El conjunto devuelto**no incluye** el registro con `Visibility = DISABLED` (RF-7.2).                                                                                                                                                                                                                                     |
| CE-7  | Se invocan los seis endpoints sin sesión o con sesión expirada.                                                                         | `401` y no se ejecuta ninguna función del endpoint.                                                                                                                                                                                                                                                                           |
| CE-8  | Se invocan`GetEnterprises`, `UpdateEnterprise` o `UpdateEnterpriseVisibility` con una sesión de rol distinto de `admin`.         | `403` y no se ejecuta ninguna función del endpoint.                                                                                                                                                                                                                                                                           |
| CE-9  | Se invocan`UpdateEnterprise` o `UpdateEnterpriseVisibility` con rol `admin`, pero la sesión está manipulada.                      | `403`.                                                                                                                                                                                                                                                                                                                         |
| CE-10 | `GetEnterprise` devuelve una empresa con `Visibility = DISABLED`.                                                                     | `200` con la empresa (no se filtra por visibilidad en consulta individual; decisión del usuario, opción A).                                                                                                                                                                                                                  |
| CE-11 | El DTO de`InsertEnterprise` o `UpdateEnterprise` viola una regla de negocio (longitudes de campos).                                   | `400` y no se escribe ningún dato.                                                                                                                                                                                                                                                                                            |
| CE-12 | El único objeto del cuerpo de`InsertEnterprise` incluye una parte `ContactDTO` con `FullName` inválido (< 10 o > 150 caracteres). | `400` y no se crea ni la empresa ni el contacto.                                                                                                                                                                                                                                                                               |
| CE-13 | El DTO de`UpdateEnterpriseVisibility` envía `Visibility` distinto de `ENABLED` o `DISABLED`.                                     | `400` y no se realiza ninguna modificación.                                                                                                                                                                                                                                                                                   |
| CE-14 | Cualquiera de las rutas recibe un`Id` no numérico.                                                                                     | `400` y no se ejecuta ninguna función del endpoint.                                                                                                                                                                                                                                                                           |
| CE-15 | La sesión del consumidor ha vencido su vigencia (30 min desde emisión, sin renovación).                                                | `401` en los seis endpoints.                                                                                                                                                                                                                                                                                                   |
| CE-16 | Se emite un error en cualquiera de los seis endpoints.                                                                                    | El mensaje al usuario se entrega en español.                                                                                                                                                                                                                                                                                    |
| CE-17 | Fallo de persistencia al crear contacto tras crear empresa en misma transacción (p. ej. excepción de BD al insertar contacto).          | El sistema responde con código`500` y no persiste ni la empresa ni el contacto. El rollback de la transacción es una garantía de la capa de persistencia (repositorio intocable, principio 5 de la Constitución); la parte observable y verificable por este caso de uso es la respuesta `500` sin registros duplicados. |
| CE-18 | `POST /enterprise/` incluye un único objeto con una parte `ContactDTO` sin `FullName` o con `FullName` en blanco.                | No se crea el contacto; se crea solo la empresa y se responde`201`.                                                                                                                                                                                                                                                            |

---

## 7. Fuera de alcance

- Persistencia de las órdenes de servicio (bitácoras) y de cualquier entidad distinta de la empresa.
- Edición del contenido de la orden de servicio.
- Asignación de órdenes de servicio a empresas.
- Gestión de contactos adicionales para una empresa (este caso de uso solo cubre el contacto inicial en la creación; contactos adicionales se gestionarían en un caso de uso separado).
- Recuperación, restablecimiento o envío de contraseñas por correo.
- Esquema de autorización distinto del binario administrador / no administrador.
- Autenticación por proveedores externos, correo, SMS o segundo factor.
- Auditoría de los cambios realizados sobre las empresas.
- Versionado y publicación del contrato de la API más allá de RNF-9.
- Pruebas de rendimiento, de carga y de penetración.
- Modificación de `hexArch/repository` (prohibido por el principio 5 de la Constitución).

---

## 8. Criterios de finalización

El caso de uso Empresa se considera concluido cuando:

1. Los seis endpoints de la sección 4 están disponibles y responden conforme a su criterio de aceptación.
2. Los seis endpoints rechazan con `401` las peticiones sin sesión válida o con sesión vencida.
3. `GetEnterprises`, `UpdateEnterprise` y `UpdateEnterpriseVisibility` rechazan con `403` las peticiones cuyo `Role` de los `claims` no sea `admin`, sin ejecutar ninguna función del endpoint y sin consultar `Visibility` ni el almacén.
4. `InsertEnterprise`, `GetEnterprise` y `GetEnterprisesForSelects` aceptan usuario autenticado (no exigen `admin`).
5. Un `Id` de ruta no numérico se rechaza con `400` en todos los endpoints que lo reciben.
6. El `Id` del parámetro de ruta prevalece sobre el `Id` del cuerpo en `GetEnterprise`, `UpdateEnterprise` y `UpdateEnterpriseVisibility`.
7. `UpdateEnterpriseVisibility` aplica únicamente el campo `Visibility` e ignora cualquier otra propiedad del DTO recibido.
8. `GetEnterprises` devuelve **todos** los registros (incluye `Visibility = DISABLED`).
9. `GetEnterprisesForSelects` devuelve **solo** registros con `Visibility = ENABLED`.
10. `GetEnterprise` devuelve el registro aunque tenga `Visibility = DISABLED` (no filtra en consulta individual; `404` solo para ID inexistente; decisión del usuario, opción A).
11. Los orígenes de fallo de la sección 4 se traducen a `404`, `400` o `500` según corresponda, con mensajes en español.
12. `InsertEnterprise` responde `201` sin cuerpo ni cabecera `Location`; `UpdateEnterprise` y `UpdateEnterpriseVisibility` responden `204` sin cuerpo; `GetEnterprise`, `GetEnterprises` y `GetEnterprisesForSelects` responden `200` con el DTO o colección correspondiente.
13. Cada endpoint cuenta con `WithName(...)` usando exactamente los nombres de la tabla de resumen y `Produces(...)` para cada código de respuesta posible.
14. El proyecto compila y todas las pruebas pasan (`dotnet test` verde).
15. Cada endpoint cuenta con su prueba automatizada y con la prueba de su caso límite y su caso de error, según `docs/constitution.md`; cada prueba crea sus propios datos.
16. `InsertEnterprise` ejecuta la creación de empresa y contacto (si se provee) en una única transacción atómica; el endpoint delega íntegramente en el caso de uso y no abre ni cierra transacciones por su cuenta. Verificación dentro del alcance de este caso de uso: el test del caso límite CE-17 comprueba que un fallo del caso de uso al crear el contacto se propaga y se traduce a `500` sin reintentos. El rollback efectivo del registro de empresa es una garantía de la capa de persistencia, declarada no modificable por el principio 5 de la Constitución y por tanto fuera del alcance de verificación de este caso de uso.

---

## 9. Dudas abiertas

- Ninguna.
