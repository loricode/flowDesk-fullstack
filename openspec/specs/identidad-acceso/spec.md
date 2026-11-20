# Identidad y acceso

Implementación: [`AuthService`](../../../backend/FlowDesk.Application/Identity/AuthService.cs),
[`UserService`](../../../backend/FlowDesk.Application/Identity/UserService.cs),
[`Security.cs`](../../../backend/FlowDesk.Infrastructure/Security/Security.cs), políticas en
[`Program.cs`](../../../backend/FlowDesk.API/Program.cs). Seed en [`DbSeeder`](../../../backend/FlowDesk.Infrastructure/Persistence/DbSeeder.cs).

## Endpoints

| Método | Ruta | Acceso | Respuesta |
|---|---|---|---|
| POST | `/api/auth/login` | público, limitado | 200 `{ accessToken, expiresAt, user }` · 400 · 401 · 429 |
| GET | `/api/auth/me` | autenticado | 200 `UserDto` |
| GET | `/api/users` | `users.manage` | 200 `UserDto[]` |
| POST | `/api/users` | `users.manage` | 201 · 400 |
| PUT | `/api/users/{id}` | `users.manage` | 200 · 400 · 404 |
| GET | `/api/users/lookup?permission=` | `expedients.read` | 200 `UserLookupDto[]` (activos) |
| GET | `/api/roles` | `users.manage` | 200 `RoleDto[]` |

## Requisito: autenticación
Cada petición a `/api/**` (salvo el login) DEBE llevar `Authorization: Bearer <JWT>`. El token DEBE incluir `sub`, `name`, `email`,
un claim `role` por rol y un claim `perm` por permiso, y DEBE expirar a los `Jwt:ExpiresMinutes` (480 por defecto).
SignalR DEBE aceptar el token en `access_token` de la query string, solo en `/hubs/**`.

### Escenario: credenciales inválidas
- **Dado** un correo existente y una contraseña incorrecta
- **Cuando** llama `POST /api/auth/login`
- **Entonces** recibe 401 "Credenciales inválidas."
- **Y** queda un registro `auth.loginFailed` en la auditoría con el correo intentado

### Escenario: usuario inactivo
- **Dado** un usuario con `IsActive = false`
- **Cuando** inicia sesión con su contraseña correcta
- **Entonces** recibe 401, igual que con credenciales inválidas

## Requisito: autorización por permiso
La API DEBE registrar una política por permiso y cada endpoint DEBE declarar la suya. El dominio DEBE revalidar el permiso de cada
transición del workflow. Ocultar opciones en React o WPF es solo usabilidad.

| Rol | expedients.read | expedients.create | expedients.assign | documents.upload | workflow.validate | workflow.approve | audit.read | users.manage |
|---|---|---|---|---|---|---|---|---|
| Operador | ✔ | ✔ | | ✔ | | | | |
| Validador | ✔ | | ✔ | ✔ | ✔ | | | |
| Aprobador | ✔ | | | | | ✔ | | |
| Auditor | ✔ | | | | | | ✔ | |
| Administrador | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ |

### Escenario: el operador no ve la auditoría
- **Dado** el usuario `operador`
- **Cuando** consulta `GET /api/audit`
- **Entonces** recibe 403

### Escenario: ruta sin permiso en la web
- **Dado** el usuario `operador`
- **Cuando** navega a `/audit`
- **Entonces** la web lo redirige a `/`

## Requisito: credenciales
Las contraseñas DEBEN guardarse con PBKDF2-SHA256, 100.000 iteraciones y sal aleatoria de 16 bytes, y compararse en tiempo
constante. Un usuario nuevo DEBE tener correo único (normalizado), nombre y contraseña de al menos 8 caracteres.
En la web, la sesión vive en `sessionStorage` y se borra al cerrar sesión o al recibir un 401.

## Requisito: protección del login
El login DEBE limitarse a `RateLimiting:LoginPermitsPerMinute` peticiones por minuto e IP (10 por defecto); el exceso recibe 429.

## Fuera de alcance del MVP
OIDC, refresh tokens, segundo factor, recuperación de contraseña. Los permisos de un usuario se renuevan al volver a iniciar sesión.
