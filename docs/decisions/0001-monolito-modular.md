# ADR 0001 · Monolito modular con Clean Architecture

**Estado:** aceptada

## Contexto
FlowDesk tiene varios módulos (Identity, Expedients, DocumentManagement, Workflow, Notifications,
Audit, Reporting) con transacciones que cruzan módulos: aprobar un expediente cambia su estado,
registra la aprobación y escribe auditoría de forma atómica.

## Decisión
Un único despliegue de API organizado por módulos y capas (Domain, Application, Infrastructure, API),
más un **Worker** independiente para el trabajo asíncrono.

## Consecuencias
- Transacciones locales simples y consistentes; sin sagas distribuidas.
- El Worker escala por separado, que es donde está la carga variable (procesamiento de documentos).
- Los límites entre módulos se respetan por convención y revisión. Si un módulo necesitara escalar o
  desplegarse aparte, su comunicación ya pasa por eventos (Outbox), lo que facilita extraerlo.
