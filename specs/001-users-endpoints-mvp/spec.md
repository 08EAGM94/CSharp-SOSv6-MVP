# Especificacion — Caso de Uso: Usuario

- **ID de spec:** `001-users-endpoints-mvp`
- **Estado:** Implementada (2026-10-08): enmienda con RF-12 (`refresh`), HU-7 y CE-29 a CE-34 completada en la fase 10; VEREDICTO: APROBADO (2026-10-08), los diez endpoints en verde, 1090 pruebas y sin paquetes nuevos. La validacion previa de los nueve endpoints previos es de 2026-10-07 (1079 pruebas).
- **Constitucion aplicable:** `docs/constitution.md`
- **Alcance de este documento:** QUE se construye y POR QUE. Las decisiones de implementacion (mecanismo concreto de sesion, estructura del manejo de errores, estrategia de pruebas) corresponden al plan, no a esta spec.

---

## 1. Contexto y objetivo

El sistema SOS v6 administra ordenes de servicio en campo. Cada orden de servicio es registrada por un **usuario** identificado, por lo que sin un registro de usuarios confiable y correctamente protegido, la trazabilidad de las ordenes se pierde y cualquier persona podria suplantar al personal que las registra.

Esta spec define el **caso de uso Usuario**: la capacidad de registrar, consultar, listar, actualizar, habilitar/deshabilitar y autenticar usuarios, renovar la sesion mientras siga vigente, ademas de guardar y consultar la firma del usuario, y de una verificacion secundaria de la identidad del administrador.

El objetivo es que **unicamente un administrador autenticado** pueda gobernar el padron de usuarios, y que el personal operativo autentique su identidad de forma trazable.

**Por que este caso de uso va primero:** sin el no hay forma de saber quien registra cada orden de servicio, ni de proteger el resto de las operaciones del sistema.

---

## 2. Usuarios

| Actor                          | Descripcion                                                                                                               | Puede hacer                                                                                                      |
| ------------------------------ | ------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------- |
| **Administrador**        | Usuario con rol`admin`. Unico autorizado a gobernar el padron de usuarios.                                              | Iniciar sesion, verificar su contrasena, crear, consultar, listar, actualizar y habilitar/deshabilitar usuarios, y guardar o consultar la firma de un usuario. |
| **Usuario operativo**    | Usuario con un rol distinto de`admin`. Existe y puede autenticarse, pero no tiene permisos sobre el padron de usuarios. | Iniciar sesion e invocar`InsertSignature` y `GetSignature`. Cualquier intento de usar los demas endpoints de administracion de usuarios es rechazado. |
| **Consumidor de la API** | Cliente (aplicacion o herramienta) que invoca los endpoints de esta spec.                                                 | Invocar los diez endpoints respetando el contrato de cada uno.                                                   |

---

## 3. Historias de usuario

### HU-1 — Gobernar el padron de usuarios

**Como** administrador del sistema, **quiero** registrar, consultar, listar, actualizar y habilitar o deshabilitar usuarios, **para** mantener al dia el padron de personal que registra ordenes de servicio y retirer a quien ya no pertenece a la organizacion.

### HU-2 — Autenticarme

**Como** usuario del sistema, **quiero** demostrar mi identidad con mi apodo y mi contrasena, **para** que mis acciones queden asociadas a mi y solo yo pueda realizarlas.

### HU-3 — Proteger el padron de usuarios

**Como** administrador del sistema, **quiero** que las operaciones sobre el padron de usuarios esten restringidas a usuarios con rol `admin`, **para** que ningun otro usuario pueda crear, modificar, consultar ni deshabilitar cuentas ajenas.

### HU-4 — Reforzar mi identidad ante operaciones sensibles

**Como** administrador del sistema, **quiero** que se me vuelva a pedir mi propia contrasena antes de operaciones sensibles, **para** que el acceso indebido a mi sesion no baste para privilegiar el sistema.

### HU-5 — Obtener una sesion

**Como** consumidor de la API, **quiero** intercambiar credenciales por una sesion, **para** invocar los endpoints protegidos sin reenviar credenciales en cada llamada.

### HU-6 — Gestionar la firma del usuario

**Como** usuario autenticado, **quiero** guardar y consultar la firma de un usuario, **para** que quede asociada a las ordenes de servicio que ese usuario registra.

### HU-7 — Renovar mi sesion

**Como** consumidor de la API autenticado, **quiero** obtener una sesion nueva mientras mi sesion actual siga vigente, **para** continuar trabajando sin repetir el inicio de sesion.

---

## 4. Requisitos funcionales

Los criterios de aceptacion usan notacion EARS en espanol:

- **Evento:** "Cuando \<evento\>, el sistema \<respuesta\>."
- **Comportamiento no deseado:** "Si \<condicion\>, entonces el sistema \<respuesta\>."
- **Estado:** "Mientras \<estado\>, el sistema \<respuesta\>."
- **Caracteristica opcional:** "Donde \<caracteristica\>, el sistema \<respuesta\>."

### Resumen de endpoints

| Metodo | Ruta           | Nombre del endpoint      | Caso de uso       | DTO y propiedades usadas                            |
| ------ | -------------- | ------------------------ | ----------------- | --------------------------------------------------- |
| POST   | `user/`      | `InsertUser`           | `CommonService` | `UserDTO` completo                                |
| GET    | `user/{id}`  | `GetUser`              | `CommonService` | solo`Id` (tomado del parametro de ruta)           |
| GET    | `users/`     | `GetUsers`             | `CommonService` | ninguno                                             |
| PUT    | `user/{id}`  | `UpdateUser`           | `CommonService` | `UserDTO` con `Id` tomado del parametro de ruta |
| PUT    | `userv/{id}` | `UpdateUserVisibility` | `CommonService` | `UserDTO` con `Id` de la ruta y `Visibility`  |
| POST   | `login/`     | `login`                | `UserService`   | `UserDTO` con `Nickname` y `Password`         |
| POST   | `adminv/`    | `AdminVerification`    | `UserService`   | `UserDTO` con `AdminNickname` y `AdminPwd`    |
| PUT    | `userisre/{id}` | `InsertSignature`  | `SignatureService` | `UserDTO` con `Id` tomado del parametro de ruta y `Signature` |
| GET    | `usersre/{id}`  | `GetSignature`     | `SignatureService` | ninguno en el cuerpo; `UserDTO` generado con `Id` tomado del parametro de ruta |
| POST   | `refresh/`    | `Refresh`             | `CommonService` (lectura del usuario) y emision de sesion | ninguno en el cuerpo; `Id` tomado de los `claims` de la sesion |

### RF-1 — Control de acceso previo a toda operacion protegida

Todos los endpoints de esta spec, **excepto `login`**, estan protegidos: exigen sesion valida. De ellos, seis exigen ademas el rol`admin` (RF-1.3 a RF-1.6) y tres (`InsertSignature`, `GetSignature` y `refresh`) admiten cualquier rol autenticado (RF-1.8).

| #      | Criterio de aceptacion                                                                                                                                                                                                                                                    |
| ------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| RF-1.1 | Cuando una peticion se dirige a un endpoint distinto de`login`, el sistema verificara primero que la peticion porta una sesion valida.                                                                                                                                  |
| RF-1.2 | Si una peticion dirigida a un endpoint distinto de`login` no porta sesion valida o su sesion ha expirado, entonces el sistema respondera con el codigo `401` y no ejecutara ninguna funcion del endpoint.                                                             |
| RF-1.3 | Donde exista una sesion valida en los endpoints que exigen rol`admin`, el sistema comprobará**antes de ejecutar cualquier otra acción** que el rol de esa sesion sea `admin`. Ese rol es el leido de los`claims` emitidos en RF-7 y es la unica fuente que decide si el flujo del endpoint continua. |
| RF-1.4 | Si el rol de la sesion no es`admin` en uno de esos endpoints, entonces el sistema respondera con el codigo `403` y no ejecutara ninguna funcion vital del endpoint.                                                            |
| RF-1.5 | Si la comparacion del rol no puede realizarse por una sesion corrupta o manipulada en uno de esos endpoints, entonces el sistema respondera con el codigo`403` y no ejecutara ninguna funcion vital del endpoint.                        |
| RF-1.6 | Mientras el rol de la sesion no sea`admin`, el sistema mantendra inaccessibles los seis endpoints que exigen ese rol. La comprobacion no consultara la`Visibility` ni ningun otro dato del usuario en el almacen.                          |
| RF-1.7 | El endpoint`login` no exigira sesion previa, con el fin de permitir el establecimiento de la sesion inicial.                                                                                                                                                            |
| RF-1.8 | Donde la sesion sea valida en`InsertSignature`, `GetSignature` o `refresh`, el sistema continuara el flujo del endpoint con independencia del rol de esa sesion: estos tres endpoints no verifican el rol. |

### RF-2 — `InsertUser` (POST `user/`)

Registra un usuario nuevo.

| #      | Criterio de aceptacion                                                                                                                                                                          |
| ------ | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| RF-2.1 | Cuando un administrador autenticado invoque`POST user/`, el sistema registrara un usuario a partir del DTO de usuario recibido.                                                               |
| RF-2.2 | Si el`Alias` recibido ya esta registrado, entonces el motor de persistencia generara un conflicto de unicidad y el sistema respondera con el codigo `500`, sin crear un registro duplicado. |
| RF-2.3 | Si algun dato del DTO no cumple las reglas de negocio del usuario, entonces el sistema respondera con el codigo`400` y no creara el registro.                                                 |
| RF-2.4 | Cuando el registro se complete, el sistema respondera con el codigo`201` y sin cuerpo; el caso de uso`InsertUser` es`void` y no produce ningun objeto de retorno.                         |
| RF-2.5 | La creacion no expone en la respuesta ninguna referencia al recurso creado: la respuesta se limita al codigo`201` y no incluye cuerpo ni cabecera`Location`.                                |
| RF-2.6 | Si el DTO recibido no cumple las reglas de negocio aplicables a`Id`,`Alias` o`Role`, entonces el sistema respondera con el codigo`400` y no creara el registro.                         |

### RF-3 — `GetUser` (GET `user/{id}`)

Obtiene un registro de usuario. **No filtra por visibilidad**: devuelve `200` con el registro aunque su `Visibility` sea `DISABLED` (decisión del usuario, opción A). El código `404` queda reservado exclusivamente a identificadores inexistentes. Este comportamiento es coherente con el repositorio de usuarios, cuya consulta por identificador no aplica filtro de visibilidad y solo lanza `KeyNotFoundException` cuando el ID no existe; `hexArch/repository` es intocable (Constitución principio 5).

| #      | Criterio de aceptacion                                                                                                                               |
| ------ | ---------------------------------------------------------------------------------------------------------------------------------------------------- |
| RF-3.1 | Cuando un administrador autenticado invoque`GET user/{id}`, el sistema substituye el `Id` del DTO de usuario por el valor del parametro de ruta. |
| RF-3.2 | Cuando el DTO tenga asignado el`Id` del parametro de ruta, el sistema devolvera el registro de usuario correspondiente a ese identificador.        |
| RF-3.3 | Si el identificador no corresponde a ningun usuario, entonces el sistema respondera con el codigo`404`.                                            |
| RF-3.4 | Si el`Id` de la ruta no es un valor numerico valido, entonces el sistema respondera con el codigo`400` y no consultara ningun registro.          |
| RF-3.5 | Si el usuario existe pero tiene`Visibility = DISABLED`, el sistema devolvera `200` con el `UserDTO` completo; **no** respondera `404`. |

### RF-4 — `GetUsers` (GET `users/`)

Obtiene todos los registros de usuarios habilitados.

| #      | Criterio de aceptacion                                                                                                                                                     |
| ------ | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| RF-4.1 | Cuando un administrador autenticado invoque`GET users/`, el sistema devolvera el conjunto de todos los registros de usuarios con `Visibility` distinto de`DISABLED`. |
| RF-4.2 | El conjunto devuelto no incluira ningun registro con`Visibility = DISABLED`, con independencia de su existencia en el almacen.                                           |
| RF-4.3 | Si no existe ningun usuario habilitado registrado, entonces el sistema devolvera un conjunto vacio con el codigo`200`.                                                   |

### RF-5 — `UpdateUser` (PUT `user/{id}`)

Actualiza un usuario.

| #      | Criterio de aceptacion                                                                                                                                                                                                                                                                                                                                                                                                           |
| ------ | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| RF-5.1 | Cuando un administrador autenticado invoque`PUT user/{id}`, el sistema substituye el `Id` del DTO de usuario por el valor del parametro de ruta, con independencia del `Id` que venga en el cuerpo de la peticion.                                                                                                                                                                                                         |
| RF-5.2 | Cuando el DTO tenga asignado el`Id` del parametro de ruta, el sistema actualizara el usuario correspondiente a ese identificador con los datos del DTO.                                                                                                                                                                                                                                                                        |
| RF-5.3 | Si el identificador no corresponde a ningun usuario, entonces el sistema respondera con el codigo`404` y no realizara ninguna modificacion.                                                                                                                                                                                                                                                                                    |
| RF-5.4 | Si algun dato del DTO no cumple las reglas de negocio del usuario, entonces el sistema respondera con el codigo`400` y no realizara ninguna modificacion.                                                                                                                                                                                                                                                                      |
| RF-5.5 | Cuando la actualizacion se complete, el sistema respondera con el codigo`204` y sin cuerpo; el caso de uso`UpdateUser` es`void` y no produce ningun objeto de retorno.                                                                                                                                                                                                                                                     |
| RF-5.6 | Si el`Id` de la ruta no es un valor numerico valido, entonces el sistema respondera con el codigo`400` y no realizara ninguna modificacion.                                                                                                                                                                                                                                                                                  |
| RF-5.7 | Si el`Id` de la ruta corresponde al administrador autenticado y la peticion es `PUT user/{id}` para modificar su propio `Alias`, `Password` u otro campo del `UserDTO` (distinto de `Visibility`), el sistema realizara la actualizacion y respondera con el codigo `204`. La unica accion prohibida sobre la cuenta propia es la autodeshabilitacion, que se rige por RF-6.7 del bloque `UpdateUserVisibility`. |

### RF-6 — `UpdateUserVisibility` (PUT `userv/{id}`)

Actualiza unicamente el campo `Visibility` de un usuario.

| #      | Criterio de aceptacion                                                                                                                                                                                                       |
| ------ | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| RF-6.1 | Cuando un administrador autenticado invoque`PUT userv/{id}`, el sistema substituye el `Id` del DTO de usuario por el valor del parametro de ruta.                                                                        |
| RF-6.2 | Donde el endpoint realize una modificacion de tipo actualizacion sobre el campo`Visibility`, el sistema tomara del DTO unicamente las propiedades `Id` y `Visibility`; cualquier otra propiedad del DTO sera ignorada. |
| RF-6.3 | Si el valor recibido en`Visibility` no es uno de los admitidos por las reglas de negocio, entonces el sistema respondera con el codigo `400` y no realizara ninguna modificacion.                                        |
| RF-6.4 | Si el identificador no corresponde a ningun usuario, entonces el sistema respondera con el codigo`404` y no realizara ninguna modificacion.                                                                                |
| RF-6.5 | Cuando la modificacion se complete, el sistema respondera con el codigo`204` y sin cuerpo; el caso de uso`UpdateUserVisibility` es`void` y no produce ningun objeto de retorno.                                        |
| RF-6.6 | Si el`Id` de la ruta no es un valor numerico valido, entonces el sistema respondera con el codigo`400` y no realizara ninguna modificacion.                                                                              |
| RF-6.7 | Si el`Id` de la ruta corresponde al administrador autenticado y el`Visibility` solicitado es`DISABLED`, entonces el sistema respondera con el codigo`403` y mantendra la cuenta habilitada.                          |

### RF-7 — `login` (POST `login/`)

Autentica a un usuario y establece su sesion. Las credenciales viajan en el cuerpo de la peticion.

| #        | Criterio de aceptacion                                                                                                                                                                                                                                    |
| -------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| RF-7.0   | El endpoint de inicio de sesion utiliza el metodo`POST`; las credenciales se reciben en el cuerpo de la peticion y nunca en la URL.                                                                                                                     |
| RF-7.1   | Cuando un consumidor invoque`POST login/` con credenciales validas de un usuario habilitado, el sistema emitira una sesion para ese usuario y la devolvera al consumidor de la API.                                                                     |
| RF-7.2   | Si las credenciales no corresponden a un usuario habilitado, entonces el sistema respondera con el codigo`404`.                                                                                                                                         |
| RF-7.3   | Si la contrasena escrita no corresponde a la del usuario identificado, entonces el sistema respondera con el codigo`400` y no emitira ninguna sesion.                                                                                                   |
| RF-7.4   | Donde la sesion se emita correctamente, el sistema incluira en ella el rol del usuario autenticado, de modo que los endpoints protegidos puedan verificarlo.                                                                                              |
| RF-7.5   | El endpoint`login` no exigira sesion previa (ver RF-1.7).                                                                                                                                                                                               |
| RF-7.6   | La busqueda de credenciales solo contemplara registros de usuario con`Visibility = ENABLED`; un registro con`Visibility = DISABLED` sera tratado como inexistente.                                                                                    |
| RF-7.7   | La sesion emitida tendra una vigencia de**30 minutos** contados desde su emision y, una vez vencida, el sistema respondera con el codigo`401` en los endpoints protegidos.                                                                        |
| RF-7.7b  | La vigencia no se renueva automaticamente con la actividad del consumidor: solo cuenta desde la emision del token. La unica forma de obtener una sesion nueva mientras la actual siga vigente es invocar el endpoint`refresh` (RF-12); una sesion ya vencida no puede renovarse.                                                                     |
| RF-7.8   | La sesion emitida transportara en sus`claims` el DTO del usuario devuelto por el caso de uso.                                                                                                                                                           |
| RF-7.9   | Los`claims` contendran`Id`, `Name`, `Surname`, `Nickname`, `Role` y `Signature`; las propiedades`Password` y `ConfPwd` quedan excluidas de forma explicita al construir la sesion y no se incluyen aunque el DTO las traiga informadas. |
| RF-7.10  | El`Role` presente en los`claims` sera la unica fuente que determina si se continua el flujo de los endpoints que verifican precisamente ese campo.                                                                                                    |
| RF-7.10b | Si el`Role` de los`claims` no es`admin`, entonces los endpoints que verifican el rol responderan con`403` sin ejecutar ninguna funcion, con independencia de la`Visibility` del usuario.                                                        |
| RF-7.11  | La construccion de los`claims` respondera a una lista de cierre de propiedades permitidas; cualquier propiedad no incluida en ella se omitira, con independencia de su valor.                                                                           |
| RF-7.12  | `Visibility` no tendra relevancia en los`claims`: no se incluira, no se verificara y no participara en ninguna decision de control de acceso.                                                                                                         |

### RF-8 — `AdminVerification` (POST `adminv/`)

Verifica la contrasena del administrador. Es un endpoint protegido: exige sesion valida de rol`admin` como cualquier otro (RF-1.1 a RF-1.6). Su unica excepcion al control de acceso es el propio`login` (RF-1.7).

Las credenciales del administrador **no** provienen de los`claims` de la sesion ni de la URL: viajan en el cuerpo de la peticion, en las propiedades `AdminNickname` y `AdminPwd` del DTO. La sesion acredita que quien invoca es un administrador autorizado; el DTO aporta la contrasena que se debe contrastar.

| #      | Criterio de aceptacion                                                                                                                                                                                      |
| ------ | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| RF-8.0 | El endpoint de verificacion del administrador utiliza el metodo`POST`; las credenciales se reciben en el cuerpo de la peticion y nunca en la URL.                                                         |
| RF-8.1 | Cuando un administrador autenticado invoque`POST adminv/` con `AdminNickname` y `AdminPwd`, el sistema solicitara al caso de uso de usuario la confirmacion de la contrasena del administrador.       |
| RF-8.2 | Si la confirmacion resulta verdadera, entonces el sistema respondera con el codigo`200` y un cuerpo JSON con el indicador`confirmed` en `true`.                                                       |
| RF-8.3 | Si la confirmacion resulta falsa, entonces el sistema respondera con el codigo`403`.                                                                                                                      |
| RF-8.4 | Si la identificacion del administrador no corresponde a ningun usuario, entonces el sistema respondera con el codigo`404`.                                                                                |
| RF-8.5 | La resolucion de la contrasena usara`AdminNickname` como alias y`AdminPwd` como contrasena a contrastar; estas propiedades no se leeran de los`claims` ni del cuerpo de`UserDTO` con otros nombres. |
| RF-8.6 | Los`claims` de la sesion no se utilizaran para obtener la contrasena del administrador, y`Password` y`ConfPwd` no tendran papel alguno en este endpoint.                                              |

### RF-9 — Contrato de errores

Todo fallo de negocio producido por un caso de uso se traduce a un codigo HTTP y a un mensaje al usuario final.

| #      | Criterio de aceptacion                                                                                                                          |
| ------ | ----------------------------------------------------------------------------------------------------------------------------------------------- |
| RF-9.1 | Si un caso de uso falla por registro no encontrado, entonces el sistema respondera con el codigo`404`.                                        |
| RF-9.2 | Si un caso de uso falla por una violacion de reglas de negocio de la entidad, entonces el sistema respondera con el codigo`400`.              |
| RF-9.3 | Si un caso de uso falla por una violacion de reglas de negocio de la aplicacion, entonces el sistema respondera con el codigo`400`.           |
| RF-9.4 | Si un caso de uso falla por cualquier otra causa de negocio, entonces el sistema respondera con el codigo`400`.                               |
| RF-9.5 | Si un caso de uso falla por un conflicto de unicidad emitido por el motor de persistencia, entonces el sistema respondera con el codigo`500`. |
| RF-9.6 | Donde la API produzca un mensaje de error, el mensaje sera redigido en espanol.                                                                 |

### RF-10 — `InsertSignature` (PUT `userisre/{id}`)

Guarda la firma de un usuario. Actualiza unicamente el campo `Firma` del registro; el nombre del endpoint es el proporcionado por el consumidor de la API, con independencia de que la operacion sea una actualizacion. Es un endpoint protegido: exige sesion valida (RF-1.1 y RF-1.2), pero no restringe el rol, con independencia de que el rol sea`admin` o no (RF-1.8).

| #         | Criterio de aceptacion                                                                                                                                                                                                        |
| --------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| RF-10.1   | Cuando un usuario autenticado invoque`PUT userisre/{id}`, el sistema substituira el `Id` del DTO de usuario por el valor del parametro de ruta, con independencia del `Id` que venga en el cuerpo de la peticion.          |
| RF-10.2   | Cuando el DTO tenga asignado el`Id` del parametro de ruta, el sistema actualizara el campo `Firma` del usuario correspondiente a ese identificador con la propiedad `Signature` del DTO.                                        |
| RF-10.3   | Donde el endpoint realize una modificacion sobre el campo`Firma`, el sistema tomara del DTO unicamente las propiedades `Id` y `Signature`; cualquier otra propiedad del DTO sera ignorada y ninguna otra columna del usuario se modificara. |
| RF-10.4   | Si el identificador no corresponde a ningun usuario, entonces el sistema respondera con el codigo`404` y no realizara ninguna modificacion.                                                                                    |
| RF-10.5   | Si algun dato del DTO no cumple las reglas de negocio aplicables a`Id` (mayor que cero) o `Signature` (maximo 255 caracteres), entonces el sistema respondera con el codigo `400` y no realizara ninguna modificacion.          |
| RF-10.6   | Cuando la modificacion se complete, el sistema respondera con el codigo`204` y sin cuerpo; el caso de uso`InsertSignature` es`void` y no produce ningun objeto de retorno.                                                     |
| RF-10.7   | Si el`Id` de la ruta no es un valor numerico valido, entonces el sistema respondera con el codigo`400` y no realizara ninguna modificacion.                                                                                    |
| RF-10.8   | Si el usuario existe pero tiene`Visibility = DISABLED`, el sistema guardara la firma y respondera con el codigo `204`; no respondera `404`.                                                                                     |

### RF-11 — `GetSignature` (GET `usersre/{id}`)

Obtiene la firma de un usuario. **No filtra por visibilidad**: devuelve `200` con la firma aunque el registro tenga `Visibility = DISABLED`, en coherencia con RF-3.5 y con el repositorio de usuarios, cuya consulta por identificador no aplica filtro de visibilidad (`hexArch/repository` es intocable, Constitucion principio 5). Es un endpoint protegido: exige sesion valida (RF-1.1 y RF-1.2), pero no restringe el rol, con independencia de que el rol sea`admin` o no (RF-1.8).

| #         | Criterio de aceptacion                                                                                                                                                                  |
| --------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| RF-11.1   | Cuando un usuario autenticado invoque`GET usersre/{id}`, el sistema generara un DTO de usuario y asignara su propiedad `Id` con el valor del parametro de ruta; la peticion no tiene cuerpo. |
| RF-11.2   | Cuando el DTO tenga asignado el`Id` del parametro de ruta, el sistema consultara el registro de usuario correspondiente y devolvera su campo `Firma`.                                    |
| RF-11.3   | Cuando el usuario exista, el sistema respondera con el codigo`200` y un cuerpo JSON que contiene la propiedad `Signature` con la firma almacenada.                                        |
| RF-11.4   | Si el identificador no corresponde a ningun usuario, entonces el sistema respondera con el codigo`404`.                                                                                  |
| RF-11.5   | Si el`Id` de la ruta no es un valor numerico valido, entonces el sistema respondera con el codigo`400` y no consultara ningun registro.                                                  |
| RF-11.6   | Si el usuario existe pero tiene`Visibility = DISABLED`, el sistema devolvera `200` con la firma; **no** respondera `404`.                                                                  |

### RF-12 — `refresh` (POST `refresh/`)

Renueva la sesion del consumidor mientras la actual siga vigente. No recibe cuerpo: el usuario se identifica a partir de los`claims` de la sesion que porta la peticion, y los datos de la sesion nueva se toman del usuario consultado en el almacen al momento de la llamada. Es un endpoint protegido: exige sesion valida (RF-1.1 y RF-1.2), pero no restringe el rol (RF-1.8).

| #        | Criterio de aceptacion                                                                                                                                                                                                                                    |
| -------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| RF-12.1  | Cuando un consumidor invoque`POST refresh/` con una sesion valida, el sistema consultara el usuario correspondiente al`Id` de los`claims` de esa sesion y emitira una sesion nueva para ese usuario, devuelta con el codigo`200`.                              |
| RF-12.2  | La sesion nueva tendra una vigencia de**30 minutos** contados desde su emision.                                                                                                                                                                            |
| RF-12.3  | Los`claims` de la sesion nueva se construiran con la lista de cierre de RF-7.9 y RF-7.11 a partir del usuario consultado en el almacen, de modo que reflejen los datos del usuario al momento de la llamada.                                                  |
| RF-12.4  | El endpoint`refresh` no recibira cuerpo ni credenciales como parametros de consulta o de ruta (RNF-13).                                                                                                                                                    |
| RF-12.5  | Si el usuario del`Id` de los`claims` ya no existe en el almacen, entonces el sistema respondera con el codigo`404` y no emitira ninguna sesion.                                                                                                             |
| RF-12.6  | Si el usuario existe pero tiene`Visibility = DISABLED`, entonces el sistema respondera con el codigo`404` y no emitira ninguna sesion, en coherencia con el tratamiento del usuario deshabilitado en RF-7.2.                                                  |
| RF-12.7  | Si la sesion no porta un`Id` utilizable (ausente o no numerico), entonces el sistema respondera con el codigo`401` y no emitira ninguna sesion.                                                                                                             |
| RF-12.8  | Donde la sesion sea valida en`refresh`, el sistema continuara el flujo del endpoint con independencia del rol de esa sesion: este endpoint no verifica el rol (RF-1.8).                                                                                     |
| RF-12.9  | La sesion anterior permanece valida hasta su propio vencimiento: el sistema no dispone de mecanismo de revocacion de sesiones ya emitidas.                                                                                                                  |

---

## 5. Requisitos no funcionales

| #      | Requisito                           | Criterio de aceptacion                                                                                                                                                                                                                                                                                                                                                                |
| ------ | ----------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| RNF-1  | Esquema por capas                   | La especificacion respeta la separacion entre dominio, aplicacion, datos y repositorio establecida en`docs/constitution.md`.                                                                                                                                                                                                                                                        |
| RNF-2  | Puertos y adaptadores               | Los casos de uso se comunican con la persistencia unicamente a traves de los puertos de la capa de aplicacion; ningun caso de uso accede directamente al mecanismo de almacenamiento.                                                                                                                                                                                                 |
| RNF-3  | Inyeccion de dependencias           | Los componentes se registran con ambito de vida por peticion.                                                                                                                                                                                                                                                                                                                         |
| RNF-4  | Plataforma                          | El sistema opera sobre .NET 10.0 usando unicamente biblioteca estandar.                                                                                                                                                                                                                                                                                                               |
| RNF-5  | Persistencia                        | La persistencia de usuarios se realiza mediante Entity Framework Core.                                                                                                                                                                                                                                                                                                                |
| RNF-6  | Idioma                              | Los identificadores y los comentarios del codigo estan en ingles; los mensajes destinados al usuario final estan en espanol.                                                                                                                                                                                                                                                          |
| RNF-7  | Consistencia de datos               | Cuando una operacion de escritura se complete, el estado almacenado debe corresponder a los datos enviados por el consumidor de la API.                                                                                                                                                                                                                                               |
| RNF-8  | Ausencia de filtracion de secretos  | Ninguna respuesta de la API debe exponer contrasenas ni otros datos de credencial. Las credenciales se reciben en el cuerpo de la peticion y viajan cifradas en transito.                                                                                                                                                                                                             |
| RNF-9  | Compatibilidad                      | El contrato de los diez endpoints no cambia de forma incompatible dentro de este caso de uso; la adicion de`refresh` es un cambio compatible.                                                                                                                                                                                                                                                                                 |
| RNF-10 | Credenciales en transito y reposo   | Ninguna contrasena se persiste en texto plano: las operaciones de`CREATE` y `UPDATE` la almacenan cifradas.                                                                                                                                                                                                                                                                       |
| RNF-11 | Roles predefinidos                  | `Role` solo admite los valores`admin` y `user`; ningun otro valor es aceptado.                                                                                                                                                                                                                                                                                                  |
| RNF-12 | Credenciales en la sesion           | Los`claims` de la sesion emitida se construyen a partir de una lista de cierre de propiedades permitidas que excluye`Password` y `ConfPwd`; ninguna otra via de lectura del usuario puede introducirlas en el token.                                                                                                                                                            |
| RNF-13 | Credenciales fuera de la URL        | Ningun endpoint recibe credenciales como parametros de consulta o de ruta.                                                                                                                                                                                                                                                                                                            |
| RNF-14 | Sesion con JWT                      | La sesion se implementa con JWT. Esta spec autoriza expresamente la instalacion y el uso del paquete`Microsoft.AspNetCore.Authentication.JwtBearer` en`net10.0`, por ser imprescindible para emitir y validar el token. No se autoriza ningun otro paquete.                                                                                                                       |
| RNF-15 | Cadena de conexion en configuracion | La cadena de conexion no reside en el codigo del contexto. Esta spec autoriza expresamente modificar`hexArch/data/Models/Sosv6DbContext.cs` para retirar la cadena fija de`OnConfiguring`, que pasa a obtenerse de la seccion`ConnectionStrings` de`appsettings.json` y a registrarse en`AddDbContext`. El resto del contexto, incluido`OnModelCreating`, no se modifica. |

---

## 6. Casos limite

| #      | Situacion                                                                                                                                                                      | Comportamiento esperado                                                                                                                                                                    |
| ------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| CE-1   | El`Id` del cuerpo de `PUT user/{id}`, `PUT userv/{id}` o `PUT userisre/{id}` difiere del `Id` de la ruta.                                                                       | Prevalece siempre el`Id` de la ruta.                                                                                                                                                     |
| CE-2   | `PUT userv/{id}` incluye propiedades ajenas a `Id` y `Visibility`.                                                                                                       | Esas propiedades se ignoran y no producen efecto sobre el usuario.                                                                                                                         |
| CE-2b  | Se espera un objeto o una cabecera`Location` en la respuesta de`POST user/`, `PUT user/{id}` o `PUT userv/{id}`.                                                       | Ninguno devuelve objeto ni`Location`: `POST user/` responde`201` y los`PUT` responden`204`.                                                                                      |
| CE-3   | Se solicita un usuario inexistente.                                                                                                                                            | `404`.                                                                                                                                                                                   |
| CE-4   | Se intenta crear un usuario cuyo`Alias` ya esta registrado.                                                                                                                  | `500` y no se duplica el registro.                                                                                                                                                       |
| CE-5   | Se intenta actualizar un usuario inexistente.                                                                                                                                  | `404` y no se realiza ninguna modificacion.                                                                                                                                              |
| CE-6   | Se solicitan usuarios y no hay ninguno habilitado registrado.                                                                                                                  | `200` con conjunto vacio.                                                                                                                                                                |
| CE-6b  | Existe al menos un usuario deshabilitado y se invocan los listados de multiples registros.                                                                                     | El conjunto devuelto no incluye los registros con`Visibility = DISABLED`.                                                                                                                |
| CE-7   | Se invocan los nueve endpoints protegidos sin sesion o con sesion expirada.                                                                                                     | `401` y no se ejecuta ninguna funcion del endpoint.                                                                                                                                      |
| CE-8   | Se invocan los seis endpoints que exigen rol`admin` con una sesion de rol distinto de`admin`.                                                                            | `403` y no se ejecuta ninguna funcion del endpoint.                                                                                                                                      |
| CE-9   | Se invocan los seis endpoints que exigen rol`admin` con rol`admin`, pero la sesion esta manipulada o corrupta.                                                          | `403`.                                                                                                                                                                                   |
| CE-10  | Se invocan credenciales de un usuario deshabilitado en`login`.                                                                                                               | `404` y no se emite sesion.                                                                                                                                                              |
| CE-10b | Se inspeccionan los`claims` de una sesion emitida tras un`login` exitoso.                                                                                                  | Contienen`Id`, `Name`, `Surname`, `Nickname`, `Role` y `Signature`, y ninguna otra propiedad.                                                                                  |
| CE-10d | El DTO devuelto por el caso de uso llega con`Password` y `ConfPwd` informadas.                                                                                             | Los`claims` emitidos siguen sin contienen`Password` ni `ConfPwd`.                                                                                                                    |
| CE-10e | Se anade una propiedad nueva al DTO de usuario en una modificacion posterior.                                                                                                  | La propiedad no aparece en los`claims` hasta que se incorporate de forma explicita a la lista de cierre.                                                                                 |
| CE-10f | Se inspeccionan los`claims` buscando`Visibility`.                                                                                                                          | No esta presente:`Visibility` no se incluye ni se verifica en la sesion (RF-7.12).                                                                                                       |
| CE-10g | Una sesion emitida con`Role = admin` se usa tras degradar al usuario a`Role = user`.                                                                                       | El flujo continua hasta que la sesion venza, porque el rol de los`claims` es la unica fuente (RF-7.10, RF-7.10b, CE-19c).                                                                |
| CE-10c | Se invoca`login` con las credenciales en parametros de consulta en lugar del cuerpo.                                                                                         | El metodo`POST` no las procesa y no se emite sesion.                                                                                                                                     |
| CE-11  | Se invocan credenciales válidas de apodo pero con contrasena incorrecta en`login`.                                                                                          | `400` y no se emite sesion.                                                                                                                                                              |
| CE-12  | Se invocan credenciales de un usuario inexistente en`login`.                                                                                                                 | `404`.                                                                                                                                                                                   |
| CE-13  | Se solicita`AdminVerification` y la contrasena del administrador no coincide.                                                                                                | `403`.                                                                                                                                                                                   |
| CE-14  | Se solicita`AdminVerification` y la contrasena del administrador coincide.                                                                                                   | `200` con`confirmed = true`.                                                                                                                                                           |
| CE-14b | Se solicita`AdminVerification` con un`AdminNickname` que no corresponde a ningun usuario.                                                                                  | `404`.                                                                                                                                                                                   |
| CE-14d | Se invoca`POST adminv/` sin`AdminNickname` o sin`AdminPwd` en el cuerpo.                                                                                                 | `400` y no se invoca la verificacion.                                                                                                                                                    |
| CE-14e | Se espera que la contrasena del administrador se tome de los`claims` de la sesion.                                                                                           | No se toma de ahi: se contrasta contra el`AdminPwd` recibido en el cuerpo (RF-8.5).                                                                                                      |
| CE-14c | El administrador autenticado invoca`PUT userv/{id}` sobre su propio`Id` pidiendo`DISABLED`.                                                                              | `403` y su cuenta permanece habilitada.                                                                                                                                                  |
| CE-18  | Cualquiera de las rutas recibe un`Id` no numerico.                                                                                                                           | `400` y no se ejecuta ninguna funcion del endpoint.                                                                                                                                      |
| CE-19  | La sesion del consumidor ha vencido su vigencia.                                                                                                                               | `401` en los endpoints protegidos.                                                                                                                                                       |
| CE-19b | La sesion se emitio hace mas de 30 minutos, aunque el consumidor no haya dejado de usarla.                                                                                     | La sesion esta vencida:`401` en los endpoints protegidos, incluido`refresh` (la vigencia no se renueva automaticamente; RF-7.7b, RF-1.2).                                          |
| CE-19c | El usuario cambia su`Role` o su`Visibility` despues de haber iniciado sesion.                                                                                              | La sesion en curso conserva el`Role` emitido en sus`claims` hasta que venza; no se revalida ni se consulta el almacen.                                                                 |
| CE-15  | El DTO de`InsertUser` o `UpdateUser` viola una regla de negocio.                                                                                                           | `400` y no se escribe ningun dato.                                                                                                                                                       |
| CE-16  | Se emite un error en cualquiera de los diez endpoints.                                                                                                                       | El mensaje al usuario se entrega en espanol.                                                                                                                                               |
| CE-17  | El campo`Visibility` recibe un valor no admitido.                                                                                                                            | `400` y no se realiza ninguna modificacion.                                                                                                                                              |
| CE-20  | `GetUser` devuelve un usuario con `Visibility = DISABLED`.                                                                                                                 | `200` con el usuario (no se filtra por visibilidad en consulta individual; decisión del usuario, opción A).                                                                            |
| CE-21  | Un administrador autenticado invoca`PUT user/{id}` con su propio `Id` para modificar su `Alias`, `Password` u otro campo del `UserDTO` (distinto de `Visibility`). | `204` con la cuenta actualizada (no `403`; decisión del usuario, opción A: el admin SÍ puede modificar su propia cuenta; la unica prohibicion es la autodeshabilitacion de RF-6.7). |
| CE-22  | `PUT userisre/{id}` incluye propiedades ajenas a `Id` y `Signature`.                                                                                                   | Esas propiedades se ignoran y ninguna otra columna del usuario se modifica (RF-10.3).                                                                                                    |
| CE-23  | `PUT userisre/{id}` recibe`Signature` en `null`.                                                                                                                         | Se guarda el valor nulo, la firma queda vacia y el sistema responde `204`; no es un error (la nulidad esta admitida por las reglas de negocio de la firma).                                |
| CE-24  | `PUT userisre/{id}` recibe una`Signature` de mas de 255 caracteres.                                                                                                      | `400` y no se realiza ninguna modificacion (RF-10.5).                                                                                                                                     |
| CE-25  | Se invoca`PUT userisre/{id}` o `GET usersre/{id}` sobre un identificador que no corresponde a ningun usuario.                                                             | `404` y no se ejecuta ninguna funcion de escritura del endpoint (RF-10.4, RF-11.4).                                                                                                       |
| CE-26  | `GET usersre/{id}` devuelve la firma de un usuario con`Visibility = DISABLED`.                                                                                           | `200` con la firma (no se filtra por visibilidad en consulta individual, RF-11.6).                                                                                                        |
| CE-27  | Se espera que`GET usersre/{id}` devuelva el registro completo del usuario.                                                                                               | No lo devuelve: la respuesta contiene unicamente la propiedad `Signature`; el resto de propiedades no forma parte del contrato (RF-11.3).                                                  |
| CE-28  | Se invoca`PUT userisre/{id}` o `GET usersre/{id}` con una sesion valida de rol distinto de `admin`.                                                                       | El flujo continua y el endpoint responde con normalidad (`204` o `200`); no se responde `403` por el rol (RF-1.8).                                                                        |
| CE-29  | Se invoca`POST refresh/` con una sesion ya vencida.                                                                                                                       | `401` emitido por el control de sesion; el endpoint no se ejecuta y no se emite ninguna sesion (RF-1.1, RF-1.2).                                                                         |
| CE-30  | Se invoca`POST refresh/` con una sesion valida cuyos`claims` carecen de`Id` o lo traen con un valor no numerico.                                                          | `401` y no se emite ninguna sesion (RF-12.7).                                                                                                                                            |
| CE-31  | El usuario se deshabilita o se elimina despues del`login` y se invoca`POST refresh/`.                                                                                     | `404` y no se emite ninguna sesion (RF-12.5, RF-12.6).                                                                                                                                   |
| CE-32  | El`Role` del usuario cambia (se degrada o se promueve) despues del`login` y se invoca`POST refresh/`.                                                                     | La sesion nueva refleja el rol actual del almacen; la sesion anterior conserva su rol hasta vencer (RF-12.3 frente a CE-10g y CE-19c).                                                    |
| CE-33  | Se inspeccionan los`claims` de la sesion emitida por`refresh`.                                                                                                           | Contienen`Id`, `Name`, `Surname`, `Nickname`, `Role` y `Signature`, y ninguna otra propiedad: la misma lista de cierre de RF-7.9, sin `Password`, `ConfPwd` ni `Visibility` (RF-12.3).     |
| CE-34  | Se usa la sesion anterior despues de haber refrescado con exito.                                                                                                         | Sigue valida hasta su propio vencimiento: no se revoca (RF-12.9).                                                                                                                       |

---

## 7. Fuera de alcance

- Persistencia de las ordenes de servicio y de cualquier entidad distinta del usuario.
- Edicion del contenido de la orden de servicio.
- Asignacion de ordenes de servicio a usuarios.
- Gestion de la comparacion entre `ConfPwd` y `Password`, que corresponde a capas mas externas que la API y quedan fuera del alcance del MVP.
- Recuperacion, restablecimiento o envio de contrasenas por correo.
- Registro de usuarios por parte de usuarios que no sean `admin`.
- Esquema de autorizacion distinto del binario administrador / no administrador.
- Autenticacion por proveedores externos, correo, SMS o segundo factor.
- Auditoria de los cambios realizados sobre los usuarios.
- Revocacion o anulacion de sesiones ya emitidas: el token anterior permanece valido hasta su propio vencimiento (RF-12.9).
- Versionado y publicacion del contrato de la API mas alla de RNF-9.
- Pruebas de rendimiento, de carga y de penetracion.

---

## 8. Criterios de finalizacion

El caso de uso Usuario se considera concluido cuando:

1. Los diez endpoints de la seccion 4 estan disponibles y responden conforme a su criterio de aceptacion.
2. Los nueve endpoints protegidos rechazan con `401` las peticiones sin sesion valida o con sesion vencida.
3. Los seis endpoints que exigen rol`admin` rechazan con `403` las peticiones cuyo`Role` de los`claims` no sea `admin`, sin ejecutar ninguna funcion del endpoint y sin consultar `Visibility` ni el almacen (RF-7.10, RF-7.10b, RF-7.12, RF-1.3, RF-1.6); `InsertSignature`, `GetSignature` y `refresh` no verifican el rol y solo exigen sesion valida (RF-1.8).
4. Un`Id` de ruta no numerico se rechaza con `400` en todos los endpoints que lo reciben.
5. El `Id` del parametro de ruta prevalece sobre el `Id` del cuerpo en `GetUser`, `UpdateUser`, `UpdateUserVisibility` e `InsertSignature`.
6. `UpdateUserVisibility` aplica unicamente el campo `Visibility` y rechaza con `403` que el administrador se deshabilite a si mismo.
7. `GetUsers` no devuelve registros con `Visibility = DISABLED`.
8. `GetUser` devuelve el registro aunque tenga `Visibility = DISABLED` (no filtra en consulta individual; `404` solo para ID inexistente; decisión del usuario, opción A).
9. Los origenes de fallo de la seccion 4 se traducen a `404`, `400` o `500` segun corresponda, con mensajes en espanol.
10. `login` se expone unicamente por`POST`, recibe las credenciales en el cuerpo, solo reconoce registros con `Visibility = ENABLED`, emite sesion con el rol del usuario autenticado, ninguna sesion se emite para credenciales invalidas y la sesion vence a los 30 minutos de su emision, sin renovacion automatica por actividad; la renovacion mientras la sesion siga vigente se rige por RF-12.
11. Los`claims` de la sesion emitida se construyen por lista de cierre: contienen el DTO del usuario limitado a`Id`, `Name`, `Surname`, `Nickname`, `Role` y `Signature`, y omiten`Password` y `ConfPwd` aunque el DTO las informe (RF-7.8, RF-7.9, RF-7.11 y RNF-12). `Visibility` no forma parte de los`claims` ni participa en el control de acceso (RF-7.12).
12. `InsertUser` responde `201` sin cuerpo ni cabecera `Location`; `UpdateUser` y `UpdateUserVisibility` responden `204` sin cuerpo, por ser `void` sus casos de uso; `AdminVerification` responde `200` con `confirmed = true` cuando la contrasena coincide y `403` cuando no coincide.
13. Ninguna respuesta de la API expone contrasenas, ninguna contrasena se almacena en texto plano y ninguna credencial se recibe por la URL (RNF-8, RNF-10 y RNF-13).
14. `UpdateUserVisibility` solo propaga a la capa de datos las propiedades `Id` y `Visibility`, ignorando cualquier otra propiedad del DTO recibido (RF-6.2 y CE-2).
15. `AdminVerification` se expone unicamente por`POST`, recibe `AdminNickname` y `AdminPwd` en el cuerpo, exige sesion valida de rol`admin` y nunca toma la contrasena del administrador de los`claims` ni de la URL (RF-8.0, RF-8.5 y RF-8.6).
16. El paquete`Microsoft.AspNetCore.Authentication.JwtBearer` es el unico autorizado por RNF-14, y`dotnet list package` no muestra ningun otro paquete fuera de la lista de la constitucion.
17. Cada endpoint cuenta con su prueba automatizada y con la prueba de su caso limite y su caso de error, segun `docs/constitution.md`; cada prueba crea sus propios datos.
18. El proyecto compila y todas las pruebas pasan.
19. `Sosv6DbContext` no contiene ninguna cadena de conexion en su codigo, y el arranque falla si la seccion`ConnectionStrings` no trae la clave esperada (RNF-15).
20. Un administrador autenticado puede invocar `PUT user/{id}` con su propio `Id` para modificar su `Alias`, `Password` u otros campos del `UserDTO` (distinto de `Visibility`) y la operacion responde `204` con la cuenta actualizada; la unica prohibicion sobre la cuenta propia es la autodeshabilitacion (RF-6.7).
21. `InsertSignature` se expone unicamente por `PUT userisre/{id}`: exige sesion valida sin restringir el rol, sustituye el `Id` del cuerpo por el de la ruta, actualiza unicamente el campo `Firma` con la `Signature` del DTO y responde `204` sin cuerpo; `404` si el usuario no existe y `400` si el `Id` no es numerico o la firma supera los 255 caracteres (RF-10, RF-1.8).
22. `GetSignature` se expone unicamente por `GET usersre/{id}`: exige sesion valida sin restringir el rol, genera un DTO con el `Id` de la ruta, responde `200` con un cuerpo que contiene la propiedad `Signature`, `404` si el usuario no existe y `400` si el `Id` no es numerico, sin filtrar por `Visibility` (RF-11, RF-1.8).
23. `refresh` se expone unicamente por `POST refresh/`: exige sesion valida sin restringir el rol, no recibe cuerpo ni credenciales en la URL, toma el`Id` de los`claims` de la sesion, consulta el usuario en el almacen y responde `200` con una sesion nueva de 30 minutos cuyos`claims` siguen la lista de cierre; `404` si el usuario no existe o esta `DISABLED`, `401` si la sesion no porta un`Id` utilizable, y la sesion anterior no se revoca (RF-12).

---
