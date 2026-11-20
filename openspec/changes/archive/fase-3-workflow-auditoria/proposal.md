# Fase 3 · Workflow y auditoría

## Por qué
El valor de FlowDesk frente a un repositorio de archivos es controlar el proceso: transiciones explícitas con permisos y
condiciones, constancia de cada decisión y un rastro de auditoría que nadie pueda alterar.

## Qué cambia
- `WorkflowDefinition` persistido con 8 transiciones, permisos y observaciones obligatorias.
- `Expedient.ApplyTransition` con validación de estado, permiso, observación y condiciones de negocio.
- Aprobaciones e historial; detalle del expediente con transiciones disponibles por usuario.
- Auditoría de solo inserción con correlation id, trigger en SQL Server; dashboard operativo.

## Specs afectadas
[workflow-aprobaciones](../../../specs/workflow-aprobaciones/spec.md), [auditoria-reportes](../../../specs/auditoria-reportes/spec.md)

## Historias
HU-16 a HU-24.
