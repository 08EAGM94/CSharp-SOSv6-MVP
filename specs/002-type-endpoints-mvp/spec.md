# Especificación — Caso de Uso: Tipo

- **ID de spec:** `002-type-endpoints-mvp`
- **Estado:** Implementada y validada
- **Constitución aplicable:** `docs/constitution.md`
- **Alcance de este documento:** QUÉ se construye y POR QUÉ. Las decisiones de implementación (mecanismo concreto de sesión, estructura del manejo de errores, estrategia de pruebas) corresponden al plan, no a esta spec.

---

## 1. Contexto y objetivo

El sistema SOS v6 administra **bitácoras**, que son las órdenes de servicio registradas en campo. Cada **bitácora** referencia como máximo un **equipo** (`EquipoId` es opcional: una bitácora puede registrarse sin equipo) y un equipo tiene muchas bitácoras. Cada **equipo** pertenece obligatoriamente a un **tipo** (`TipoId` es obligatorio) y un tipo clasifica muchos equipos. Esa cadena —**Tipo → Equipo → Bitácora**— hace que el catálogo de tipos sea la raíz de la clasificación y, por tanto, gobierne la trazabilidad de las órdenes de servicio: un tipo mal clasificado o deshabilitado indebidamente afecta a los equipos que lo usan y, a través de ellos, a las bitácoras ya registradas o por registrar.

Esta spec define el **caso de uso Tipo**: la capacidad de registrar, consultar, listar, actualizar y cambiar la visibilidad de los tipos que clasifican a los equipos.

El objetivo es que **cualquier usuario autenticado** pueda consultar el catálogo de tipos (incluido el listado reducido para *selects* de la interfaz al asignar un tipo a un equipo), y que **únicamente un administrador autenticado** pueda gobernar dicho catálogo (crear, modificar y habilitar/deshabilitar tipos).

**Por qué este caso de uso va después de Usuario:** el control de acceso a los endpoints de Tipo depende de la sesión y el rol establecidos por el caso de uso Usuario (spec 001).

---

## 2. Usuarios

| Actor                          | Descripción                                                                                                               | Puede hacer                                                                                                      |
| ------------------------------ | ------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------- |
| **Administrador**        | Usuario con rol `admin`. Único autorizado a gobernar el catálogo de tipos.                                              | Iniciar sesión (via caso de uso Usuario), crear, consultar, listar, actualizar y cambiar visibilidad de tipos. |
| **Usuario operativo**    | Usuario con un rol distinto de `admin`. Existe, puede autenticarse y consultar el catálogo.                             | Iniciar sesión, consultar un tipo, listar todos los tipos, listar tipos para selects.                            |
| **Consumidor de la API** | Cliente (aplicación o herramienta) que invoca los endpoints de esta spec.                                                 | Invocar los seis endpoints respetando el contrato de cada uno.                                                   |

---

## 3. Historias de usuario

### HU-1 — Gobernar el catálogo de tipos

**Como** administrador del sistema, **quiero** registrar, consultar, listar, actualizar y cambiar la visibilidad de los tipos, **para** mantener al día la clasificación de los equipos y, a través de ellos, de las bitácoras (órdenes de servicio); un tipo mal clasificado o deshabilitado indebidamente impacta en los equipos que lo usan y en la trazabilidad de las bitácoras asociadas.

### HU-2 — Consultar el catálogo de tipos

**Como** usuario operativo del sistema, **quiero** consultar un tipo por su identificador, listar todos los tipos existentes y obtener la lista reducida para selects, **para** poder clasificar correctamente el equipo al registrar una bitácora (orden de servicio), ya que el equipo exige un tipo obligatorio y el select de la interfaz se alimenta del endpoint reducido.

### HU-3 — Proteger el catálogo de tipos

**Como** administrador del sistema, **quiero** que las operaciones de escritura sobre el catálogo (**actualizar y cambiar visibilidad**) estén restringidas a usuarios con rol `admin`, **para** que ningún otro usuario pueda alterar la clasificación de los tipos, lo cual repercute en los equipos que los usan y en las bitácoras vinculadas a esos equipos. La creación de tipos (`InsertType`) es accesible para cualquier usuario autenticado.

---

## 4. Requisitos funcionales

Los criterios de aceptación usan notación EARS en español:

- **Evento:** "Cuando \<evento\>, el sistema \<respuesta\>."
- **Comportamiento no deseado:** "Si \<condición\>, entonces el sistema \<respuesta\>."
- **Estado:** "Mientras \<estado\>, el sistema \<respuesta\>."
- **Característica opcional:** "Donde \<característica\>, el sistema \<respuesta\>."

### Resumen de endpoints

| Método | Ruta           | Nombre del endpoint       | Caso de uso        | DTO y propiedades usadas                                               |
| ------ | -------------- | ------------------------- | ------------------ | ---------------------------------------------------------------------- |
| POST   | `/type/`       | `InsertType`              | `CommonService`    | `TypeDTO` completo (`Type` obligatorio, `Visibility` se fija a ENABLED) |
| GET    | `/type/{id}`   | `GetType`                 | `CommonService`    | DTO de entrada: solo `Id` (tomado del parámetro de ruta); **respuesta: `TypeDTO` completo (`Id`, `Type`, `Visibility`)** |
| GET    | `/type/`       | `GetTypes`                | `CommonService`    | ninguno                                                                |
| PUT    | `/type/{id}`   | `UpdateType`              | `CommonService`    | `TypeDTO` con `Id` tomado del parámetro de ruta y `Type`               |
| PUT    | `/typev/{id}`  | `UpdateTypeVisibility`    | `CommonService`    | `TypeDTO` con `Id` de la ruta y `Visibility`                           |
| GET    | `/typesct/`    | `GetTypesForSelects`      | `SelectService`    | ninguno                                                                |

### RF-1 — Control de acceso previo a toda operación protegida

Todos los endpoints de esta spec **exigen sesión válida**.

| #      | Criterio de aceptación                                                                                                                                                                                        |
| ------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| RF-1.1 | Cuando una petición se dirige a cualquiera de los seis endpoints, el sistema verificará primero que la petición porta una sesión válida.                                                                      |
| RF-1.2 | Si una petición no porta sesión válida o su sesión ha expirado, entonces el sistema responderá con el código `401` y no ejecutará ninguna función del endpoint.                                               |
| RF-1.3 | Donde exista una sesión válida, el sistema comprobará **antes de ejecutar cualquier otra acción** que el rol de esa sesión sea `admin` **solo para los endpoints `UpdateType` y `UpdateTypeVisibility`**. Ese rol es el leído de los `claims` emitidos en el login (spec 001) y es la única fuente que decide si el flujo del endpoint continúa. |
| RF-1.4 | Si el rol de la sesión no es `admin` **y el endpoint es `UpdateType` o `UpdateTypeVisibility`**, entonces el sistema responderá con el código `403` y no ejecutará ninguna función vital del endpoint.       |
| RF-1.5 | Si la comparación del rol no puede realizarse por una sesión corrupta o manipulada, entonces el sistema responderá con el código `403` y no ejecutará ninguna función vital del endpoint.                    |
| RF-1.6 | Para los endpoints `InsertType`, `GetType`, `GetTypes` y `GetTypesForSelects`, **basta con una sesión válida (cualquier rol autenticado)**; no se verifica el rol `admin`.                                    |
| RF-1.7 | Ningún endpoint de esta spec es público: todos exigen sesión válida. No existe equivalente a `login` en este caso de uso.                                                                                     |

### RF-2 — `InsertType` (POST `/type/`)

Registra un tipo nuevo. El caso de uso fija `Visibility = "ENABLED"` al crear.

| #      | Criterio de aceptación                                                                                                                          |
| ------ | ----------------------------------------------------------------------------------------------------------------------------------------------- |
| RF-2.1 | Cuando un usuario autenticado invoque `POST /type/`, el sistema registrará un tipo a partir del DTO de tipo recibido.                           |
| RF-2.2 | El caso de uso asigna `Visibility = "ENABLED"` al nuevo registro; el valor de `Visibility` enviado en el cuerpo (si lo hay) es ignorado.       |
| RF-2.3 | Si el DTO no cumple las reglas de negocio del tipo (`Type` con longitud entre 5 y 50 caracteres, validada en `TypeEntity` al mapear DTO→entidad), entonces el sistema responderá con el código `400` (`EntityException` → RF-8.2) y no creará el registro. |
| RF-2.4 | Cuando el registro se complete, el sistema responderá con el código `201` y sin cuerpo; el caso de uso `AddAsyncInfo` es `void` y no produce ningún objeto de retorno. |
| RF-2.5 | La creación no expone en la respuesta ninguna referencia al recurso creado: la respuesta se limita al código `201` y no incluye cuerpo ni cabecera `Location`. |
| RF-2.6 | Si el motor de persistencia genera un conflicto de unicidad por el índice único `uq_tipo` sobre `Tipo1` (modelo de datos), el sistema responderá con el código `500` y no creará un registro duplicado. |

### RF-3 — `GetType` (GET `/type/{id}`)

Obtiene un registro de tipo. **No filtra por visibilidad**: devuelve `200` con el registro aunque su `Visibility` sea `DISABLED` (decisión del usuario, opción A). El código `404` queda reservado exclusivamente a identificadores inexistentes. Este comportamiento es coherente con `TypeRepository.GetAsyncInfo()`, que no aplica filtro de visibilidad y solo lanza `KeyNotFoundException` cuando el ID no existe; `hexArch/repository` es intocable (Constitución principio 5).

| #      | Criterio de aceptación                                                                                                                               |
| ------ | ---------------------------------------------------------------------------------------------------------------------------------------------------- |
| RF-3.1 | Cuando un usuario autenticado invoque `GET /type/{id}`, el sistema sustituye el `Id` del DTO de tipo por el valor del parámetro de ruta.           |
| RF-3.2 | Cuando el DTO tenga asignado el `Id` del parámetro de ruta, el sistema devolverá el registro de tipo correspondiente a ese identificador.           |
| RF-3.3 | Si el identificador no corresponde a ningún tipo, entonces el sistema responderá con el código `404`.                                              |
| RF-3.4 | Si el `Id` de la ruta no es un valor numérico válido, entonces el sistema responderá con el código `400` y no consultará ningún registro.          |
| RF-3.5 | Si el tipo existe pero tiene `Visibility = DISABLED`, el sistema devolverá `200` con el `TypeDTO` completo; **no** responderá `404`.               |

### RF-4 — `GetTypes` (GET `/type/`)

Obtiene **todos** los registros de tipos, **incluyendo los deshabilitados**. Esta decisión es coherente con el comportamiento de `TypeRepository.GetAsyncAllInfo()`, que no aplica filtro de visibilidad.

| #      | Criterio de aceptación                                                                                                          |
| ------ | ------------------------------------------------------------------------------------------------------------------------------- |
| RF-4.1 | Cuando un usuario autenticado invoque `GET /type/`, el sistema devolverá el conjunto de **todos** los registros de tipos, independientemente de su `Visibility`. |
| RF-4.2 | El conjunto devuelto **incluirá** registros con `Visibility = DISABLED`, con independencia de su existencia en el almacen.     |
| RF-4.3 | Si no existe ningún tipo registrado, entonces el sistema devolverá un conjunto vacío con el código `200`.                       |

### RF-5 — `UpdateType` (PUT `/type/{id}`)

Actualiza un tipo. Solo accesible para rol `admin`.

| #      | Criterio de aceptación                                                                                                                                                                                                   |
| ------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| RF-5.1 | Cuando un administrador autenticado invoque `PUT /type/{id}`, el sistema sustituye el `Id` del DTO de tipo por el valor del parámetro de ruta, con independencia del `Id` que venga en el cuerpo de la petición. |
| RF-5.2 | Cuando el DTO tenga asignado el `Id` del parámetro de ruta, el sistema actualizará el tipo correspondiente a ese identificador con los datos del DTO (propiedad `Type`). |
| RF-5.3 | Si el identificador no corresponde a ningún tipo, entonces el sistema responderá con el código `404` y no realizará ninguna modificación.                                                                              |
| RF-5.4 | Si el DTO no cumple las reglas de negocio del tipo (`Type` con longitud entre 5 y 50 caracteres, validada en `TypeEntity` al mapear DTO→entidad), entonces el sistema responderá con el código `400` (`EntityException` → RF-8.2) y no realizará ninguna modificación. |
| RF-5.5 | Cuando la actualización se complete, el sistema responderá con el código `204` y sin cuerpo; el caso de uso `UpdateAsyncInfo` es `void` y no produce ningún objeto de retorno.                                                                                                          |
| RF-5.6 | Si el `Id` de la ruta no es un valor numérico válido, entonces el sistema responderá con el código `400` y no realizará ninguna modificación.                                                                          |

### RF-6 — `UpdateTypeVisibility` (PUT `/typev/{id}`)

Actualiza **únicamente** el campo `Visibility` de un tipo. Solo accesible para rol `admin`.

| #      | Criterio de aceptación                                                                                                                                                                                                       |
| ------ | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| RF-6.1 | Cuando un administrador autenticado invoque `PUT /typev/{id}`, el sistema sustituye el `Id` del DTO de tipo por el valor del parámetro de ruta.                                                                        |
| RF-6.2 | Cuando el endpoint actualice el campo `Visibility`, el sistema tomará del DTO **únicamente** las propiedades `Id` y `Visibility`; cualquier otra propiedad del DTO será ignorada. |
| RF-6.3 | Si el valor recibido en `Visibility` no es uno de los admitidos por las reglas de negocio (`ENABLED` o `DISABLED`), entonces el sistema responderá con el código `400` y no realizará ninguna modificación.                                        |
| RF-6.4 | Si el identificador no corresponde a ningún tipo, entonces el sistema responderá con el código `404` y no realizará ninguna modificación.                                                                                |
| RF-6.5 | Cuando la modificación se complete, el sistema responderá con el código `204` y sin cuerpo; el caso de uso `UpdateAsyncVisibility` es `void` y no produce ningún objeto de retorno.                                                   |
| RF-6.6 | Si el `Id` de la ruta no es un valor numérico válido, entonces el sistema responderá con el código `400` y no realizará ninguna modificación.                                                                                  |

### RF-7 — `GetTypesForSelects` (GET `/typesct/`)

Obtiene los registros de tipos **habilitados únicamente** (`Visibility = ENABLED`). Esta decisión es coherente con el comportamiento de `TypeRepository.GetAsyncInfoForSelects()`, que filtra por `Visibilidad == "ENABLED"`.

| #      | Criterio de aceptación                                                                                                          |
| ------ | ------------------------------------------------------------------------------------------------------------------------------- |
| RF-7.1 | Cuando un usuario autenticado invoque `GET /typesct/`, el sistema devolverá el conjunto de registros de tipos con `Visibility = ENABLED`. |
| RF-7.2 | El conjunto devuelto **no incluirá** ningún registro con `Visibility = DISABLED`, con independencia de su existencia en el almacen.     |
| RF-7.3 | Si no existe ningún tipo habilitado registrado, entonces el sistema devolverá un conjunto vacío con el código `200`.             |

### RF-8 — Contrato de errores

Todo fallo de negocio producido por un caso de uso se traduce a un código HTTP y a un mensaje al usuario final.

| #      | Criterio de aceptación                                                                                                                |
| ------ | ------------------------------------------------------------------------------------------------------------------------------------- |
| RF-8.1 | Si un caso de uso falla por registro no encontrado (`KeyNotFoundException` del repositorio), entonces el sistema responderá con el código `404`.                              |
| RF-8.2 | Si un caso de uso falla por una violación de reglas de negocio de la entidad (`EntityException`), entonces el sistema responderá con el código `400`.    |
| RF-8.3 | Si un caso de uso falla por una violación de reglas de negocio de la aplicación (`ApplicationException`, p. ej. `Visibility` inválido o `Id` < 1), entonces el sistema responderá con el código `400`. |
| RF-8.4 | Si un caso de uso falla por cualquier otra causa de negocio, entonces el sistema responderá con el código `400`.                     |
| RF-8.5 | Si un caso de uso falla por un conflicto de unicidad emitido por el motor de persistencia, entonces el sistema responderá con el código `500`. |
| RF-8.6 | Donde la API produzca un mensaje de error, el mensaje será redactado en español.                                                       |

### RF-9 — Documentación del contrato (OpenAPI / Swagger)

Los seis endpoints deben exponer su contrato de respuestas documentado y su nombre público.

| #      | Criterio de aceptación                                                                                                                |
| ------ | ------------------------------------------------------------------------------------------------------------------------------------- |
| RF-9.1 | Cada endpoint usará `WithName(...)` con **exactamente** el nombre indicado en la tabla de la sección "Resumen de endpoints" (`InsertType`, `GetType`, `GetTypes`, `UpdateType`, `UpdateTypeVisibility`, `GetTypesForSelects`). |
| RF-9.2 | Cada endpoint usará `Produces(...)` para documentar **cada** código de respuesta que pueda emitir según sus RF. La tabla siguiente lista los códigos **exactos** que cada endpoint debe documentar, deducidos de RF-1 a RF-8. |
| RF-9.3 | Los nombres y códigos documentados coincidirán con los definidos en los RF-2 a RF-7 y RF-1.                                             |

#### Tabla de códigos `Produces(...)` por endpoint (verificable)

| Endpoint               | WithName(...)        | Códigos Produces(...)                                                                 |
| ---------------------- | -------------------- | ------------------------------------------------------------------------------------- |
| `InsertType`           | `InsertType`         | `201`, `400`, `401`, `500`                                                            |
| `GetType`              | `GetType`            | `200`, `400`, `401`, `404`                                                            |
| `GetTypes`             | `GetTypes`           | `200`, `401`                                                                          |
| `UpdateType`           | `UpdateType`         | `204`, `400`, `401`, `403`, `404`                                                     |
| `UpdateTypeVisibility` | `UpdateTypeVisibility` | `204`, `400`, `401`, `403`, `404`                                                   |
| `GetTypesForSelects`   | `GetTypesForSelects` | `200`, `401`                                                                          |

**Derivación:**  
- `201/204/200` son los códigos de éxito de RF-2.4, RF-5.5, RF-6.5, RF-3.2, RF-4.1, RF-7.1.  
- `401` aplica a **todos** por RF-1.2.  
- `403` aplica **solo** a `UpdateType` y `UpdateTypeVisibility` por RF-1.4.  
- `400` aplica a todos los que validan `Id` numérico (RF-3.4, RF-5.6, RF-6.6), reglas de negocio `Type` (RF-2.3, RF-5.4), `Visibility` (RF-6.3) o DTO inválido (RF-8.2/8.3).  
- `404` aplica a los que buscan por `Id` (RF-3.3, RF-5.3, RF-6.4).  
- `500` aplica solo a `InsertType` por conflicto de unicidad (RF-2.6).

---

## 5. Requisitos no funcionales

| #     | Requisito                          | Criterio de aceptación                                                                                                                                                                |
| ----- | ---------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| RNF-1 | Esquema por capas                  | La especificación respeta la separación entre dominio, aplicación, datos y repositorio establecida en `docs/constitution.md`.                                                        |
| RNF-2 | Puertos y adaptadores              | Los casos de uso se comunican con la persistencia únicamente a través de los puertos de la capa de aplicación; ningún caso de uso accede directamente al mecanismo de almacenamiento. |
| RNF-3 | Inyección de dependencias          | Los componentes se registran con ámbito de vida por petición (`scoped`).                                                                                                              |
| RNF-4 | Plataforma                         | El sistema opera sobre .NET 10.0 usando únicamente biblioteca estándar.                                                                                                               |
| RNF-5 | Persistencia                       | La persistencia de tipos se realiza mediante Entity Framework Core.                                                                                                                   |
| RNF-6 | Idioma                             | Los identificadores y los comentarios del código están en inglés; los mensajes destinados al usuario final están en español.                                                          |
| RNF-7 | Consistencia de datos              | Cuando una operación de escritura se complete, el estado almacenado debe corresponder a los datos enviados por el consumidor de la API.                                               |
| RNF-8 | Ausencia de filtración de secretos | Ninguna respuesta de la API debe exponer datos sensibles.                                                                                                                             |
| RNF-9 | Compatibilidad                     | El contrato de los seis endpoints no cambia de forma incompatible dentro de este caso de uso.                                                                                         |
| RNF-10 | Sin dependencias nuevas             | No se añade ningún paquete NuGet fuera de los ya autorizados en la spec 001 (solo `Microsoft.AspNetCore.Authentication.JwtBearer` y EF Core).                                         |

---

## 6. Casos límite

| #     | Situación                                                                                             | Comportamiento esperado                                            |
| ----- | ----------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------ |
| CE-1  | El `Id` del cuerpo de `PUT /type/{id}` o `PUT /typev/{id}` difiere del `Id` de la ruta.          | Prevalece siempre el `Id` de la ruta.                             |
| CE-2  | `PUT /typev/{id}` incluye propiedades ajenas a `Id` y `Visibility` (p. ej. `Type`).              | Esas propiedades se ignoran y no producen efecto sobre el tipo.    |
| CE-3  | Se solicita un tipo inexistente (`GetType`, `UpdateType`, `UpdateTypeVisibility`).                | `404`.                                                           |
| CE-4  | Se intenta crear un tipo cuyo `Type` ya está registrado (índice único `uq_tipo` sobre `Tipo1` en el modelo de datos).               | `500` y no se duplica el registro.                               |
| CE-5  | Se intenta actualizar un tipo inexistente.                                                          | `404` y no se realiza ninguna modificación.                      |
| CE-6  | Se solicitan tipos y no hay ninguno registrado.                                                     | `200` con conjunto vacío.                                        |
| CE-6b | Existe al menos un tipo deshabilitado y se invoca `GET /type/`.                                  | El conjunto devuelto **incluye** el registro con `Visibility = DISABLED` (RF-4.2). |
| CE-6c | Existe al menos un tipo deshabilitado y se invoca `GET /typesct/`.                               | El conjunto devuelto **no incluye** el registro con `Visibility = DISABLED` (RF-7.2). |
| CE-7  | Se invocan los seis endpoints sin sesión o con sesión expirada.                                     | `401` y no se ejecuta ninguna función del endpoint.              |
| CE-8  | Se invocan `UpdateType` o `UpdateTypeVisibility` con una sesión de rol distinto de `admin`.       | `403` y no se ejecuta ninguna función del endpoint.              |
| CE-9  | Se invocan `UpdateType` o `UpdateTypeVisibility` con rol `admin`, pero la sesión está manipulada. | `403`.                                                           |
| CE-10 | `GetType` devuelve un tipo con `Visibility = DISABLED`.                                             | `200` con el tipo (no se filtra por visibilidad en consulta individual; decisión del usuario, opción A). |
| CE-11 | El DTO de `InsertType` o `UpdateType` viola una regla de negocio (`Type` < 5 o > 50 caracteres).  | `400` y no se escribe ningún dato.                               |
| CE-12 | El DTO de `UpdateTypeVisibility` envía `Visibility` distinto de `ENABLED` o `DISABLED`.           | `400` y no se realiza ninguna modificación.                      |
| CE-13 | Cualquiera de las rutas recibe un `Id` no numérico.                                                 | `400` y no se ejecuta ninguna función del endpoint.              |
| CE-14 | La sesión del consumidor ha vencido su vigencia (30 min desde emisión, sin renovación).             | `401` en los seis endpoints.                                     |
| CE-15 | Se emite un error en cualquiera de los seis endpoints.                                                | El mensaje al usuario se entrega en español.                       |

---

## 7. Fuera de alcance

- Persistencia de las órdenes de servicio y de cualquier entidad distinta del tipo.
- Edición del contenido de la orden de servicio.
- Asignación de órdenes de servicio a tipos.
- Gestión de la comparación entre `ConfPwd` y `Password` (corresponde a capas más externas).
- Recuperación, restablecimiento o envío de contraseñas por correo.
- Registro de tipos por parte de usuarios que no sean `admin` (para `UpdateType`, `UpdateTypeVisibility`).
- Esquema de autorización distinto del binario administrador / no administrador.
- Autenticación por proveedores externos, correo, SMS o segundo factor.
- Auditoría de los cambios realizados sobre los tipos.
- Versionado y publicación del contrato de la API más allá de RNF-9.
- Pruebas de rendimiento, de carga y de penetración.
- Modificación de `hexArch/repository` (prohibido por Constitución principio 5).

---

## 8. Criterios de finalización

El caso de uso Tipo se considera concluido cuando:

1. Los seis endpoints de la sección 4 están disponibles y responden conforme a su criterio de aceptación.
2. Los seis endpoints rechazan con `401` las peticiones sin sesión válida o con sesión vencida.
3. `UpdateType` y `UpdateTypeVisibility` rechazan con `403` las peticiones cuyo `Role` de los `claims` no sea `admin`, sin ejecutar ninguna función del endpoint y sin consultar `Visibility` ni el almacen.
4. `InsertType`, `GetType`, `GetTypes` y `GetTypesForSelects` aceptan cualquier rol autenticado (no exigen `admin`).
5. Un `Id` de ruta no numérico se rechaza con `400` en todos los endpoints que lo reciben.
6. El `Id` del parámetro de ruta prevalece sobre el `Id` del cuerpo en `GetType`, `UpdateType` y `UpdateTypeVisibility`.
7. `UpdateTypeVisibility` aplica únicamente el campo `Visibility` y ignora cualquier otra propiedad del DTO recibido.
8. `GetTypes` devuelve **todos** los registros (incluye `Visibility = DISABLED`).
9. `GetTypesForSelects` devuelve **solo** registros con `Visibility = ENABLED`.
10. `GetType` devuelve el registro aunque tenga `Visibility = DISABLED` (no filtra en consulta individual; `404` solo para ID inexistente; decisión del usuario, opción A).
11. Los orígenes de fallo de la sección 4 se traducen a `404`, `400` o `500` según corresponda, con mensajes en español.
12. `InsertType` responde `201` sin cuerpo ni cabecera `Location`; `UpdateType` y `UpdateTypeVisibility` responden `204` sin cuerpo; `GetType`, `GetTypes` y `GetTypesForSelects` responden `200` con el DTO o colección correspondiente.
13. Cada endpoint cuenta con `WithName(...)` usando exactamente los nombres de la tabla de resumen y `Produces(...)` para cada código de respuesta posible.
14. El proyecto compila y todas las pruebas pasan (`dotnet test` verde).
15. Cada endpoint cuenta con su prueba automatizada y con la prueba de su caso límite y su caso de error, según `docs/constitution.md`; cada prueba crea sus propios datos.