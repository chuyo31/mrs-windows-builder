PROMPT 10 — PROGRESO Y TERMINAL DE EJECUCIÓN

Proyecto:
C:\Users\B3RASCASA\Desktop\MRS-W11PRO-by_chuyo31

Contexto:
P09 está terminado y validado. El ciclo Mount → Execute → Verify → Commit/Discard → Unmount funciona correctamente y no debemos romperlo.

Objetivo de esta fase:
Mejorar la UI de ejecución para mostrar progreso real y un terminal/log en tiempo real durante la creación de la imagen.

IMPORTANTE:
- NO rehacer la arquitectura existente.
- NO modificar la lógica de montaje/desmontaje salvo lo estrictamente necesario para emitir eventos.
- NO introducir StartComponentCleanup, ResetBase ni Cleanup-Mountpoints.
- NO cambiar el comportamiento de RemovalEngine.
- Mantener cancelación, protección e idempotencia existentes.
- Mantener todos los tests actuales pasando.

1. TELEMETRÍA

Crear una abstracción sencilla para comunicar el progreso:

- etapa actual
- porcentaje
- mensaje
- nivel: Info / Success / Warning / Error
- timestamp

Ejemplo conceptual:

ProgressInfo
{
    Stage,
    Percent,
    Message,
    Level,
    Timestamp
}

Usar IProgress<ProgressInfo> o una abstracción equivalente, evitando acoplar RemovalEngine a WPF.

2. ETAPAS

Definir progreso aproximado por fases:

0-10   Preparación / validación
10-25  Exportación de la imagen de trabajo
25-35  Montaje
35-75  Aplicación de eliminaciones
75-85  Validación
85-95  Commit
95-100 Finalización / desmontaje

El porcentaje puede ser aproximado. No fingir progreso interno de DISM que no podamos conocer.

Cuando DISM esté ejecutándose, mostrar el mensaje:
"DISM: <operación actual>"

3. LOG EN TIEMPO REAL

La ventana de ejecución debe mostrar un terminal visual donde aparezcan los mensajes conforme ocurren.

Ejemplo:

[12:35:01] MRS    Preparando workspace...
[12:35:02] DISM   Export-Image iniciado
[12:35:28] DISM   Exportación completada
[12:35:29] DISM   Mount-Wim iniciado
[12:35:42] MRS    Imagen montada
[12:35:43] REMOVE Microsoft.Clipchamp
[12:35:45] REMOVE Microsoft.XboxApp
[12:35:47] MRS    Validando imagen...
[12:36:01] MRS    Commit iniciado
[12:36:18] OK     Imagen finalizada correctamente

El terminal debe:
- hacer scroll automático al final;
- conservar los mensajes durante toda la ejecución;
- distinguir visualmente Info/Success/Warning/Error;
- permitir seleccionar/copiar texto;
- no bloquear la interfaz.

4. UI

Modificar la pantalla de ejecución para incluir:

- Título: "CREANDO IMAGEN"
- barra de progreso principal;
- porcentaje;
- etapa actual;
- mensaje actual;
- terminal/log grande;
- botones Cancelar y Cerrar.

Mientras la operación esté activa:
- Cancelar habilitado;
- Cerrar deshabilitado.

Al finalizar:
- Cancelar deshabilitado;
- Cerrar habilitado.

Mantener el comportamiento idempotente implementado en P08.

5. DISM

Aprovechar el ProcessRunner/DismRunner existente.

No crear un segundo sistema de ejecución de procesos.

Si ya existe salida stdout/stderr de DISM, reutilizarla para alimentar el terminal cuando sea apropiado.

No mostrar secretos ni información innecesaria.

6. ARQUITECTURA

Mantener separación:

RemovalEngine
    ↓
Progress/Telemetry
    ↓
MainWindow

La capa de negocio no debe depender de WPF.

No crear una dependencia inversa desde MRS.RemovalEngine hacia MRS.WindowsBuilder.

7. TESTS

Añadir tests para:

- emisión de progreso;
- orden de etapas;
- progreso 0-100;
- mensajes de éxito/error;
- propagación de cancelación;
- que un fallo de DISM emita Error;
- que la UI no requiera WPF para probar la telemetría.

Ejecutar todos los tests existentes.

RESULTADO OBLIGATORIO:

Crear:

prompts\10-resultado.md

Debe incluir:
- resumen de cambios;
- archivos modificados/creados;
- arquitectura utilizada;
- tests ejecutados y resultado;
- build ejecutado y resultado;
- cualquier problema encontrado;
- commit recomendado.

Al finalizar ejecutar:

dotnet test
dotnet build

No hacer cambios fuera del alcance de P10.

Si encuentras una decisión arquitectónica dudosa, detente y descríbela en prompts\10-resultado.md en lugar de realizar una modificación invasiva.