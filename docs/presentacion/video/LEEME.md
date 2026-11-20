# Video de FlowDesk

Graba la **aplicación real**, primero la app de escritorio (WPF) y después la web (React + MUI). Lleva subtítulos, cursor,
narración y portada, y deja el MP4 en `docs/presentacion/videos/`. Es el mismo enfoque del video de SmartOps.

| # | Video | Qué muestra | Archivo |
|---|---|---|---|
| 1 | El camino feliz | **Escritorio:** el operador crea el expediente, llegan un PDF y una imagen (y una cédula falsa que se rechaza antes de enviar), revisa la vista previa, digitaliza con el simulador TWAIN y envía; el Worker valida los tres. **Web:** la validadora inicia la validación y envía a aprobación, el aprobador aprueba con observación, el operador ve sus notificaciones y la administradora revisa la auditoría y la matriz de permisos | `01-camino-feliz.mp4` |

Última grabación: 4 min 35 s, 1440×900, narrada (es-CO-SalomeNeural), sobre una base de demostración limpia.

## Cómo se graba

1. **Escritorio** (`escritorio.ps1`): abre FlowDesk Capture y lo maneja con UI Automation (inicio de sesión, nuevo expediente,
   archivos, vista previa, escáner y envío). En paralelo, `capturar-ventana.ps1` captura **solo la ventana de la app**, nunca el escritorio
   completo. Los archivos llegan por la **carpeta vigilada** (`entrada/`), así no aparece el cuadro de diálogo de Windows con carpetas
   personales. El escáner usa el simulador TWAIN, activado solo para esa ejecución.
2. **Web** (`grabar.js`): Edge sin interfaz, capturado con CDP screencast. Los cuadros del escritorio se reproducen en una página
   de escenario con la misma capa de subtítulos; después sigue la web real.
3. **Armado**: `ffmpeg-static` une los cuadros con su duración real y mezcla la narración en su instante.

## Requisitos

- .NET 10 y Node 24. Si la API (5080) o la web (5173) no están corriendo, el script las levanta con una base de demostración limpia
  (`backend/.data/demo`) y las detiene al terminar.
- Microsoft Edge (cámbialo con `NAVEGADOR=chrome`).
- `playwright-core` y `ffmpeg-static`, en esta carpeta (`npm i`) o en la carpeta de herramientas
  `%USERPROFILE%\OneDrive\Documentos\video` (cámbiala con `VIDEO_TOOLS`).
- Narración (opcional): `pip install edge-tts`. Voz por defecto `es-CO-SalomeNeural` (cámbiala con `VOZ_TTS`). El texto de cada
  escena está en `voz.js` y se envía al servicio de voz de Microsoft para generar el audio (queda en caché en `voz/`).

## Uso

```sh
node grabar.js --prueba                       # sin video: una captura por escena en ./capturas
node grabar.js                                # graba el video completo
node grabar.js --sin-voz                      # solo subtítulos
node grabar.js --reusar-escritorio            # no vuelve a manejar la app WPF; usa frames/escritorio
```

Durante el primer minuto la app de escritorio queda en primer plano: **no uses el teclado ni el mouse** (UI Automation y la captura
de la ventana dependen de que esté visible). Para que la bandeja de la web tenga un solo expediente, detén la API antes de grabar y
el script la levantará con la base limpia.

Si una escena web falla, queda `error-camino.png` con la pantalla en ese momento.
