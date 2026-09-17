# Resultado - P22: progreso visible durante el inventariado

## Causa/limitación UX original

Al pulsar "Continuar" (inventariado), `ContinueButton_Click`
(`MainWindow.xaml.cs`) solo cambiaba `StatusText.Text` a "Inventariando
imagen..." y esperaba a `ImageInventoryService.BuildInventoryFromIsoAsync`.
Esa llamada puede tardar varios minutos (monta la imagen y ejecuta 5
operaciones DISM independientes — paquetes, características, capacidades,
aplicaciones provisionadas, drivers), sin ningún indicador salvo el texto
fijo del log ("[DISM] Mount-Wim..."), así que la ventana parecía congelada
aunque el proceso avanzara con normalidad.

## Arquitectura elegida

Mismo patrón que P10 (`MRS.RemovalEngine`/`WorkingImageFactory`): telemetría
de progreso mediante `IProgress<T>` en la capa de dominio, sin ninguna
dependencia de WPF.

- **`MRS.ImageEngine.Inventory.InventoryProgressInfo`** (nuevo, `record`):
  `Stage`, `Percent` (siempre `Math.Clamp(percent, 0, 100)` en `Create`),
  `Message`, `Level`, `Timestamp`.
- **`MRS.ImageEngine.Inventory.InventoryProgressLevel`** (nuevo, `enum`):
  `Info`/`Success`/`Warning`/`Error`.

Deliberadamente **no** se reutiliza `MRS.RemovalEngine.Models.ProgressInfo`:
siguiendo la misma decisión ya tomada en P11/P13/P15/P18/P19 (cada motor
define su propio modelo de progreso para no acoplar proyectos entre sí sin
necesidad), `MRS.ImageEngine` no tiene ninguna referencia a
`MRS.RemovalEngine` y no hacía falta añadir una solo para esto.

`ImageInventoryService.BuildInventoryAsync`/`BuildInventoryFromIsoAsync`
ganan un parámetro opcional `IProgress<InventoryProgressInfo>? progress =
null` al final de la firma (después de `cancellationToken`, con su mismo
valor por defecto): **compatible hacia atrás** — todas las llamadas
existentes (tests, y la reinventario de verificación tras aplicar cambios en
`MainWindow.xaml.cs`) siguen compilando y funcionando exactamente igual sin
pasar ningún argumento nuevo.

## Fases implementadas

Coinciden con el mínimo pedido por el prompt, en este orden, con el
porcentaje asociado a cada una (siempre correspondiente al **inicio o fin de
una fase real**, nunca a un cálculo interno de DISM):

| Fase | % |
|---|---|
| Preparando inventariado | 0 |
| Comprobando montajes | 5 |
| Montando imagen (inicio / éxito) | 10 / 20 |
| Analizando paquetes (inicio / éxito) | 20 / 35 |
| Analizando características (inicio / éxito) | 35 / 48 |
| Analizando capacidades (inicio / éxito) | 48 / 61 |
| Analizando aplicaciones provisionadas (inicio / éxito) | 61 / 74 |
| Analizando controladores (inicio / éxito) | 74 / 87 |
| Construyendo catálogo (inicio / éxito) | 90 / 93 |
| Finalizando (desmontando) | 95 |
| 100 % completado | 100 |

`BuildInventoryFromIsoAsync` reporta además, antes de delegar en
`BuildInventoryAsync`, un primer aviso en la fase "Preparando inventariado"
(0 %) mientras monta la ISO para localizar `sources\install.wim` — es la
única fase que vive fuera de `BuildInventoryAsync`, porque solo existe
cuando se parte de una ISO (no cuando se reinventaría un WIM de trabajo ya
localizado, como hace la verificación posterior a P07).

## Cómo se evita fingir el progreso interno de DISM

Cada valor de `Percent` corresponde a un punto de control ya conocido de
antemano en el código (inicio o fin confirmado de una fase), nunca a tiempo
transcurrido ni a un progreso reportado por el propio proceso DISM (que no
existe de forma fiable para `/Get-Packages`, `/Get-Features`, etc.). Las
diferencias entre fases (paquetes 20→35, características 35→48...) son
simplemente una división fija del rango 20-87 en 5 tramos iguales — una
aproximación declarada, no una medición real de cuánto tarda cada categoría,
y así se documenta aquí para no sugerir lo contrario.

## Cambios realizados

**`MRS.ImageEngine`** (dominio, sin WPF):
- `Inventory/InventoryProgressInfo.cs` (nuevo)
- `Inventory/InventoryProgressLevel.cs` (nuevo)
- `Inventory/ImageInventoryService.cs`: parámetro `progress` opcional en
  ambos métodos públicos; `RunCategoryAsync` y `EnsureDismSucceeded` ganan
  parámetros opcionales de fase/porcentaje para reportar inicio, éxito y
  error de cada operación; `Percent=100/Success` solo se reporta en el
  `finally` y solo si una bandera local `succeeded` quedó en `true` (nunca
  si hubo una excepción, aunque el desmontaje posterior vaya bien). El
  `catch` original que marca `keepWorkspace = true` y relanza la excepción
  se mantiene sin cambios (P09) — no se ha tocado ninguna lógica de
  workspace/mount/commit-discard, solo se han añadido llamadas a `Report(...)`.

**`MRS.WindowsBuilder`** (UI):
- `MainWindow.xaml`: nueva fila en el `Grid` de la barra superior (debajo
  del estado principal) con un `StackPanel` `InventoryProgressPanel`
  (`Visibility="Collapsed"` por defecto) que contiene un `ProgressBar`
  (`DarkProgressBar`, ya existente, reutilizado sin cambios), un porcentaje
  y una línea de fase — igual al mockup del prompt. No se ha rediseñado
  ningún otro panel ni pantalla.
- `MainWindow.xaml.cs`: `ContinueButton_Click` crea un
  `Progress<InventoryProgressInfo>` (mismo patrón que
  `OnExecutionProgress`/P10: el `SynchronizationContext` de la UI capturado
  en el momento de crear el `Progress<T>` garantiza que
  `OnInventoryProgress` se ejecuta en el hilo de la ventana sin bloquearlo),
  muestra el panel al empezar y lo oculta en el `finally` (éxito, error o
  cancelación). El terminal/log (`_logger`/`LogBox`) no se ha tocado: sigue
  recibiendo exactamente las mismas líneas que antes.

**No se ha modificado**: DISM, `RemovalEngine`, `RemovalPlan`,
`ComponentCatalog`, `ProfileEngine`, `InstallationOptions`, `ISOEngine`, ni
la generación de ISO. No se ha duplicado ninguna operación DISM: las mismas
5 llamadas de siempre, en el mismo orden, con el mismo `IDismRunner`.

## CancellationToken

Sigue funcionando exactamente igual: el parámetro nuevo se añadió **después**
de `cancellationToken` en ambas firmas, así que ningún llamador existente
tuvo que cambiar su forma de pasar el token, y `BuildInventoryFromIsoAsync`
sigue propagando el mismo token a `BuildInventoryAsync`.

## Tests

Nuevo archivo `tests/MRS.ImageEngine.Tests/ImageInventoryProgressTests.cs`
(10 tests) + `tests/MRS.ImageEngine.Tests/Fakes/RecordingProgress.cs` (mismo
patrón que `MRS.RemovalEngine.Tests.RecordingProgress` de P10: un
`IProgress<T>` síncrono de prueba, sin ninguna dependencia de WPF):

- Las fases se reportan en el orden exacto de la tabla de arriba.
- El progreso nunca supera 100 y nunca es negativo.
- El progreso no retrocede entre reportes consecutivos.
- El reporte final en un caso de éxito es exactamente 100 %, nivel `Success`,
  y es literalmente el último reporte emitido.
- Un fallo en una categoría o en el montaje **nunca** llega a reportar 100 %,
  y sí reporta un nivel `Error` identificando la fase que falló (paquetes,
  características... con su nombre exacto).
- El parámetro `progress` es opcional: la llamada sin él (como hacían todos
  los tests/llamadores previos a P22) sigue funcionando igual.
- El progreso no altera el orden real de las llamadas DISM (mismo test que
  ya existía en `ImageInventoryServiceTests`, repetido aquí con `progress`
  presente para confirmar que no cambia nada).

No se ha modificado ningún test existente de
`ImageInventoryServiceTests.cs`: todos siguen pasando sin cambios porque el
nuevo parámetro es opcional.

## Resultado de tests

```
dotnet test MRS-Windows-Builder.sln
```

```
MRS.InstallationOptions.Tests : 17/17
MRS.ProfileEngine.Tests       : 21/21
MRS.ImageEngine.Tests         : 88/88   (78 previos + 10 nuevos de P22)
MRS.PostInstall.Tests         : 44/44
MRS.ComponentCatalog.Tests    : 59/59
MRS.RemovalPlanning.Tests     : 48/48
MRS.RemovalEngine.Tests       : 48/48
MRS.ISOEngine.Tests           : 102/102
```

Total: **427/427**, 0 fallos. Sin regresiones.

## Resultado del build

```
dotnet build MRS-Windows-Builder.sln
Compilación correcta. 0 Advertencia(s). 0 Errores.
```

## Verificación manual

Se compiló y ejecutó `MRS.WindowsBuilder.exe` tras el cambio: la ventana
arranca con normalidad (sin el crash de P21) y el nuevo panel de progreso
permanece oculto hasta que se pulsa "Continuar", como se esperaba. No se
dispuso en esta sesión de una ISO real montable con permisos de
administrador para observar el ciclo completo de fases en pantalla (mismo
bloqueo de elevación documentado en P16/P17/P20); el comportamiento del
progreso se verificó de extremo a extremo con los tests automatizados
(DISM simulado), que cubren exactamente la secuencia de fases que se vería
en pantalla.

## Commit recomendado

`git commit` con el resumen "P22: progreso visible del inventariado —
InventoryProgressInfo/IProgress<T> en ImageInventoryService (fases reales,
nunca el % interno de DISM) + barra de progreso en MainWindow; 427/427 sin
regresiones" y `git push` a `main`.
