# Tareas · Fase 6 · Calidad y despliegue

## 1. Pruebas
- [x] 6.1 HU-35 · 27 pruebas xUnit (dominio, validación, contraseñas, Outbox y Worker sobre SQLite) en verde
- [x] 6.2 HU-35 · 13 pruebas E2E de Playwright (login, permisos, flujo completo); levantan API y web si no están corriendo
- [ ] 6.3 HU-04 · Prueba de alta y edición de usuarios (correo duplicado, contraseña corta, rol inexistente, auditoría)
- [ ] 6.4 HU-05 · Prueba del rate limiting del login (429 tras el límite)
- [ ] 6.5 HU-08 · Pruebas de los filtros de la bandeja (estado, prioridad, texto, asignados a mí) y de la paginación
- [ ] 6.6 HU-09 · Prueba de `POST /api/expedients/{id}/assign` (activo, inactivo, expediente cerrado, notificación)
- [ ] 6.7 HU-10 · Prueba automatizada del 409 con un `concurrencyStamp` desactualizado (hoy verificado en la prueba de humo manual)
- [ ] 6.8 HU-14 · Prueba del visor y del selector de versión
- [ ] 6.9 HU-19 · Prueba del ciclo corrección → nueva versión → reenvío → aprobación
- [ ] 6.10 HU-23 · Prueba de que el correlation id de la petición llega a la auditoría del Worker
- [ ] 6.14 Pruebas unitarias del frontend (Vitest): `api-client` (ProblemDetails, 401, 429), `auth` (`can`), `useRealtime`

## 2. Entornos y despliegue
- [x] 6.15 Dockerfiles de API y Worker (usuario no root) y de la web (nginx)
- [x] 6.11 HU-36 · `docker compose up --build`: prueba de humo completa (403, 409, 422, aprobación) y web en :8081 con tiempo real
- [ ] 6.12 HU-37 · Validar `infrastructure/aws/flowdesk.yaml` con `cfn-lint` y desplegarla en una cuenta de prueba
- [ ] 6.13 HU-38 · Crear el repositorio git y ejecutar `.github/workflows/ci.yml` en GitHub

## 3. Documentación y presentación
- [x] 6.16 README, arquitectura con diagramas, ADR 0001–0006, README de base de datos y de TWAIN
- [x] 6.17 PRD, historias de usuario y OpenSpec
- [x] 6.18 HU-39 · Video narrado del camino feliz (`docs/presentacion/videos/01-camino-feliz.mp4`), reproducible con `grabar.js`
