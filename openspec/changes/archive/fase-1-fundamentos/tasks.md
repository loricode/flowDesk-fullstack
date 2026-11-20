# Tareas · Fase 1 · Fundamentos

## 1. Arquitectura
- [x] 1.1 Solución `FlowDesk.slnx` con proyectos por capa y referencias `Domain ← Application ← Infrastructure ← API / Worker`
- [x] 1.2 `FlowDesk.Shared` con contratos y `FileSignatureValidator`, compartido con WPF
- [x] 1.3 `nuget.config` del repositorio (solo nuget.org) y paquetes Microsoft fijados en 10.0.15 (sin dependencias vulnerables)

## 2. Persistencia
- [x] 1.4 `FlowDeskDbContext` con configuraciones explícitas, Ids asignados por el dominio y split query
- [x] 1.5 Proveedor configurable SQL Server / SQLite (conversión de `DateTimeOffset` en SQLite)
- [x] 1.6 HU-06 · Scripts `000`–`009`, vistas, procedimientos, seed y `deploy.ps1` idempotente (verificado dos veces en LocalDB)
- [x] 1.7 HU-06 · API probada contra SQL Server con `EnsureCreated = false` (prueba de humo completa)

## 3. Identidad
- [x] 1.8 HU-01 · Login con JWT (claims `perm` y `role`), `GET /api/auth/me`, auditoría de logins
- [x] 1.9 HU-03 · Una política de autorización por permiso; el dominio revalida el permiso de cada transición
- [x] 1.10 HU-04 · Usuarios: listar, crear, editar roles y activación, catálogo de roles, búsqueda de usuarios asignables
- [x] 1.11 HU-05 · PBKDF2-SHA256 y rate limiting del login (configurable)
- [x] 1.12 Seed idempotente de permisos, roles, flujo y usuarios de demostración

## 4. Frontend base
- [x] 1.13 Monorepo con npm workspaces; tema MUI con la paleta sapote, gris y negro azulado
- [x] 1.14 HU-02 · Layout con menú por permiso, rutas protegidas y redirección sin permiso
