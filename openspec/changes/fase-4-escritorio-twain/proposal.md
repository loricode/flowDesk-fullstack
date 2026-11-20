# Fase 4 · Escritorio y TWAIN

## Por qué
El navegador no puede hablar con escáneres. La captura física necesita una aplicación de escritorio, pero el MVP debe funcionar sin
hardware y la integración TWAIN no debe acoplarse al negocio ([ADR 0004](../../../docs/decisions/0004-captura-desacoplada-twain.md)).

## Qué cambia
- FlowDesk Capture (WPF, MaterialDesignInXaml, MVVM): login, expediente destino, bandeja local, vista previa, envío y resultado.
- `IDocumentCaptureService` con captura por archivos, arrastrar, "Abrir con" y carpeta vigilada.
- `TwainCaptureService` completo sobre `ITwainSession`, con simulador; la sesión nativa queda como siguiente paso.

## Specs afectadas
[captura-escritorio](../../specs/captura-escritorio/spec.md)

## Historias
HU-25 a HU-30.
