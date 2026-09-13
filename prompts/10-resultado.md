# Resultado - P10: progreso y terminal de ejecución

## Resumen de cambios

Se añadió una telemetría de progreso (`ProgressInfo`/`ProgressLevel`) que
`WorkingImageFactory` y `RemovalEngine` reportan a través de un
`IProgress<ProgressInfo>` **opcional** (parámetro nuevo, con valor por
defecto `null`), y se conectó a una pantalla de ejecución mejorada con barra
de progreso, etapa/porcentaje/mensaje y un terminal en tiempo real con
scroll automático y colores por nivel. El ciclo
Mount → Execute → Verify → Commit/Discard → Unmount de P07-P09 **no se ha
modificado**: solo se han añadido llamadas a `progress?.Report(...)` en los
puntos donde ya existía logging, sin tocar ninguna decisión ni ningún
comando DISM.

## Archivos modificados/creados

- `src/MRS.RemovalEngine/Models/ProgressInfo.cs` (nuevo)
- `src/MRS.RemovalEngine/Models/ProgressLevel.cs` (nuevo)
- `src/MRS.RemovalEngine/IWorkingImageFactory.cs` — parámetro `progress` opcional.
- `src/MRS.RemovalEngine/WorkingImageFactory.cs` — reporta 0-25% (preparación + Export-Image).
- `src/MRS.RemovalEngine/IRemovalEngine.cs` — parámetro `progress` opcional.
- `src/MRS.RemovalEngine/RemovalEngine.cs` — reporta 25-100% (montaje, cada acción, commit, finalización); ninguna otra lógica cambiada.
- `src/MRS.WindowsBuilder/MainWindow.xaml` — pantalla de ejecución: título "CREANDO IMAGEN", barra de progreso + porcentaje + etapa, terminal (`RichTextBox`) junto a la lista de componentes existente.
- `src/MRS.WindowsBuilder/MainWindow.xaml.cs` — crea el `Progress<ProgressInfo>`, lo pasa a `WorkingImageFactory`/`RemovalEngine`, y lo traduce a la UI (`OnExecutionProgress`, `AppendTerminalLine`).
- `tests/MRS.RemovalEngine.Tests/RecordingProgress.cs` (nuevo).
- `tests/MRS.RemovalEngine.Tests/ProgressReportingTests.cs` (nuevo, 7 tests).

No se ha tocado `MRS.ComponentCatalog`, `MRS.RemovalPlanning`, ni la lógica
de `RemovalPlanBuilder`/protección/DISM del `RemovalEngine` (solo se
insertaron llamadas de reporte junto a logging que ya existía).

## Arquitectura utilizada

```
WorkingImageFactory / RemovalEngine   (MRS.RemovalEngine, sin WPF)
        │  IProgress<ProgressInfo>   (Stage, Percent, Message, Level, Timestamp)
        ▼
MainWindow (System.Progress<ProgressInfo>)
        │  reenviado por el SynchronizationContext de la UI (no bloquea)
        ▼
ProgressBar + textos de etapa/% + RichTextBox terminal
```

`ProgressInfo` vive en `MRS.RemovalEngine.Models`: ninguna clase de negocio
referencia WPF. `MainWindow` es la única capa que conoce `System.Progress<T>`
y las clases de WPF (`ProgressBar`, `RichTextBox`).

Etapas y rango de porcentaje (aproximado, sin fingir el progreso interno de
DISM que no podemos conocer):

| Rango | Etapa | Quién reporta |
|---|---|---|
| 0-10 | Preparación / validación | `MainWindow` (montaje ISO) + `WorkingImageFactory` (workspace) |
| 10-25 | Exportación de la imagen de trabajo | `WorkingImageFactory` (Export-Image) |
| 25-35 | Montaje | `RemovalEngine` (preflight + Mount-Wim) |
| 35-75 | Aplicación de eliminaciones | `RemovalEngine` (reparto lineal entre las acciones del plan) |
| 75-85 | Validación | `RemovalEngine` (checkpoint tras las acciones, antes del commit) |
| 85-95 | Commit | `RemovalEngine` (Unmount-Wim /Commit) |
| 95-100 | Finalización / desmontaje | `RemovalEngine` (verificación de montajes + resultado final) |

Cancelación y error terminan siempre en 100% (con nivel `Warning`/`Error`
respectivamente) para que la barra no quede a medias.

## Terminal

`RichTextBox` de solo lectura (permite seleccionar/copiar texto de forma
nativa) con una línea por evento: `[HH:mm:ss] ORIGEN  mensaje`, coloreada
según el nivel (Info=verde claro, Success=verde, Warning=ámbar, Error=rojo)
y con `ScrollToEnd()` tras cada línea. Se alimenta exclusivamente de lo que
ya reporta `WorkingImageFactory`/`RemovalEngine`; no se reutiliza un segundo
sistema de ejecución de procesos ni se muestra nada que no proceda ya del
`ProcessRunner`/`DismRunner` existentes.

## Decisión arquitectónica a señalar (no se ha modificado nada invasivo por ella)

El mockup del prompt muestra una columna "ORIGEN" (`MRS`/`DISM`/`REMOVE`/`OK`)
en el terminal, distinta del nivel Info/Success/Warning/Error. `ProgressInfo`
se mantuvo exactamente con las 5 propiedades que pide el prompt (Stage,
Percent, Message, Level, Timestamp), sin añadir un sexto campo "Source": el
origen visual se deriva en la UI a partir del `Level` y de si el mensaje
empieza por `"DISM:"` o contiene `"eliminado"`. Es una heurística de
presentación, no de negocio, y evita ensanchar el contrato de la telemetría;
si se prefiriera un campo `Source` explícito, es un cambio pequeño y
aislado a `ProgressInfo` y a los `Report(...)` existentes.

También se mantuvo la lista de componentes con iconos (○/⏳/✓/✗/⊘) de P07 en
vez de sustituirla por el terminal: el prompt pide añadir un terminal, no
quitar lo existente, así que ambos coexisten en dos columnas de la misma
pantalla.

## Tests añadidos

`tests/MRS.RemovalEngine.Tests/ProgressReportingTests.cs` (usa
`RecordingProgress`, un `IProgress<ProgressInfo>` que registra de forma
síncrona — no necesita WPF ni ninguna UI):

- `ProgressInfo_clamps_percent_to_0_100`.
- Éxito completo: porcentaje no decreciente y termina en 100.
- Éxito completo: las etapas se reportan en el orden esperado (Montaje →
  Aplicación de eliminaciones → Validación → Commit → Finalización).
- Un fallo de DISM emite al menos un reporte `Level = Error`.
- Cancelación: emite `Level = Warning` y también termina en 100%.
- `progress` es opcional: la ejecución funciona igual sin pasarlo (regresión).
- `WorkingImageFactory` reporta el tramo de exportación hasta el 25% y
  termina con un `Level = Success`.

## Resultado de `dotnet test`

```
MRS.ImageEngine.Tests       : 78/78
MRS.ComponentCatalog.Tests  : 41/41
MRS.RemovalPlanning.Tests   : 48/48
MRS.RemovalEngine.Tests     : 48/48   (+7 nuevos)
```

Total: **215/215**. Sin regresiones; los tests de P07-P09 (éxito, error,
commit, discard, cancelación, workspace, verificador) siguen intactos y en
verde.

## Resultado de `dotnet build`

`MRS.RemovalEngine` y los 4 proyectos de test: **0 errores, 0 advertencias**.
`MRS.WindowsBuilder` compila su XAML y C# sin errores. El build de la
solución completa sigue bloqueado únicamente por el `.exe` de las sesiones
elevadas que el usuario mantiene abiertas (no es un error de código).

## Problemas encontrados

- `Progress<T>.Report` es una implementación explícita de `IProgress<T>` (no
  es accesible sobre una variable declarada como `Progress<T>`): la variable
  en `MainWindow` se declaró como `IProgress<ProgressInfo>` para poder
  llamar `.Report(...)` también desde el propio código de `MainWindow`
  (para el tramo de montaje de la ISO, antes de invocar a
  `WorkingImageFactory`).
- Se descartó un `ControlTemplate` propio para `ProgressBar` (relleno vía
  `ScaleTransform`/columnas proporcionales) por el riesgo de un enlace WPF
  frágil para un detalle puramente visual; se usó el `ProgressBar` estándar
  con `Background`/`Foreground`/`BorderBrush` del tema oscuro, que WPF ya
  respeta sin plantilla personalizada.

## Commit recomendado

`git commit` con el resumen "P10: progreso y terminal de ejecución (sin
tocar el ciclo Mount→Execute→Verify→Commit/Discard)" y `git push` a `main`.
