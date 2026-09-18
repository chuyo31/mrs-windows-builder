# Resultado - P24: conectar la UI con el pipeline real de generación ISO

## 1. Auditoría del cableado existente (obligatoria antes de cambiar nada)

Se inspeccionó `MainWindow.xaml.cs`, `MainWindow.xaml`, `MRS.ISOEngine` completo
y el flujo de `PlanConfirmButton_Click` antes de tocar nada. Hallazgos:

- **`MRS.WindowsBuilder.csproj` no referenciaba `MRS.ISOEngine` ni
  `MRS.PostInstall`.** Ningún archivo del repositorio (fuera de los propios
  tests con fakes) construye `IsoGenerationPipeline`,
  `InstallationImageService`, `BootWimProvisioner`, `BootWimModifier`,
  `IsoTreeCopier`, `OscdimgRunner` ni `PostInstallPackageBuilder`: era
  terreno completamente sin cablear.
- **`PlanConfirmButton_Click` no generaba ninguna ISO.** Tras el bloqueo de
  P23, llamaba a `RunExecutionAsync`, que era una implementación **parcial y
  paralela**: montaba la ISO a mano, exportaba la edición con
  `WorkingImageFactory` y aplicaba el `RemovalPlan` con `RemovalEngine`
  directamente -- exactamente las dos piezas más antiguas (P07), pero
  **sin** workspace de generación, sin copiar el árbol completo de la ISO,
  sin tocar `boot.wim` (LabConfig/autounattend), sin integrar PostInstall,
  sin validación final y sin `oscdimg`. Era, en efecto, una implementación
  duplicada y desactualizada del primer tercio de `IsoGenerationPipeline`
  (P19), nunca reescrita para usarlo.
- **`IsoGenerationPipeline` (P19/P20/P23) ya contenía toda la lógica
  necesaria**, sin que hiciera falta reescribir nada de ella:
  - Ya comprueba entorno (elevación + oscdimg, P20) y valida la solicitud
    completa (ISO/edición/ruta de salida/InstallationOptions -- incluido el
    rechazo de `BypassStorage`, P15/P16 -- y PostInstall, P18) **antes** de
    tocar cualquier archivo.
  - Ya crea el workspace, copia el árbol completo de la ISO, modifica
    `boot.wim`, exporta la edición Pro, aplica el `RemovalPlan` (con el
    atajo de P23 para `TotalSelected == 0`, sin ningún cambio adicional
    aquí), integra PostInstall si está activado, valida el resultado y
    ejecuta `oscdimg` -- todo ya implementado y probado (102 tests antes de
    esta fase).
  - Su único punto de entrada, `IIsoGenerationPipeline.GenerateAsync`, ya
    expone exactamente lo que la UI necesita: recibe un
    `IsoGenerationRequest` y reporta progreso vía
    `IProgress<InstallationProgressInfo>` (mismo patrón de P10/P22, sin
    WPF).
- **Todos los constructores necesarios ya existen** en las clases reales
  (no fakes): `IsoTreeCopier(IIsoMounter, IAppLogger?)`,
  `BootWimProvisioner(IIsoMounter, IAppLogger?)`,
  `BootWimModifier(IDismRunner, IOfflineRegistryEditor, IAppLogger?)`,
  `OfflineRegistryEditor(IProcessRunner, string?)`,
  `InstallationImageService(IBootWimProvisioner, IBootWimModifier,
  IAppLogger?)`, `PostInstallPackageBuilder(IAppLogger?)`,
  `OscdimgRunner(IProcessRunner, string?)` (autodescubre el Windows ADK si
  no se le da una ruta). `MainWindow` ya construye, en su constructor,
  exactamente los objetos que estas piezas necesitan reutilizar:
  `processRunner`, `dismRunner`, `_isoMounter`, `_workingImageFactory`,
  `_removalEngine`, `_logger` -- nada de esto se duplicó.

**Conclusión de la auditoría**: el punto de integración correcto es
construir un único `IsoGenerationPipeline` en el constructor de
`MainWindow` (reutilizando los objetos ya existentes) y hacer que
`PlanConfirmButton_Click` invoque `_isoGenerationPipeline.GenerateAsync(...)`
en vez de la implementación parcial de P07. No hacía falta reescribir nada
dentro de `MRS.ISOEngine`.

## 2. Cambios realizados

**`src/MRS.WindowsBuilder/MRS.WindowsBuilder.csproj`**: añadidas las
referencias a `MRS.PostInstall` y `MRS.ISOEngine` (antes ausentes).

**`MainWindow.xaml.cs`**, constructor: se añade la construcción de
`_isoGenerationPipeline` (campo nuevo, tipo `IIsoGenerationPipeline`)
encadenando exactamente las piezas de la sección 1, sin ninguna lógica
nueva:

```csharp
var registryEditor = new OfflineRegistryEditor(processRunner);
var bootWimProvisioner = new BootWimProvisioner(_isoMounter, _logger);
var bootWimModifier = new BootWimModifier(dismRunner, registryEditor, _logger);
var installationImageService = new InstallationImageService(bootWimProvisioner, bootWimModifier, _logger);
var treeCopier = new IsoTreeCopier(_isoMounter, _logger);
var postInstallPackageBuilder = new PostInstallPackageBuilder(_logger);
var oscdimgRunner = new OscdimgRunner(processRunner);
_isoGenerationPipeline = new IsoGenerationPipeline(
    treeCopier, _isoMounter, installationImageService, _workingImageFactory,
    _removalEngine, postInstallPackageBuilder, oscdimgRunner, logger: _logger);
```

**`PlanConfirmButton_Click`**: sin cambios en la lógica de bloqueo de P23
(0 seleccionados continúa; seleccionados-pero-bloqueados sigue bloqueando);
solo cambia la llamada final: `RunExecutionAsync(...)` →
`RunIsoGenerationAsync(...)`.

**Sustituida la implementación parcial de P07 por la real**: `RunExecutionAsync`,
`ShowExecutionScreen`, `OnExecutionProgress`, `OnExecutionLogEntry`,
`ReconcileExecutionRows`, `UpdateExecutionProgress`, `VerifyExecutionAsync` y
el helper `LocateInstallImage` (montaba la ISO a mano solo para localizar
`install.wim`, algo que el pipeline ya hace internamente) se sustituyen por:
`RunIsoGenerationAsync`, `ShowIsoGenerationScreen`, `OnIsoGenerationProgress`,
`MapStageToPhaseLabel`, `AdvancePhaseRows`, `MarkAllPhaseRowsDone`,
`MarkCurrentPhaseRow`, `UpdatePhaseProgressText`, `BuildOutputIsoPath`. Se
elimina `RemovalVerifier _removalVerifier` (la reinventario de verificación
post-cambio de P10 ya no aplica: `GenerationWorkspaceValidator`, dentro del
propio pipeline, es ahora quien valida el resultado). La pantalla
**reutiliza literalmente** el overlay `ExecutionOverlay` (mismos controles
XAML: `ExecutionStageText`, `ExecutionPercentText`, `ExecutionProgressBar`,
`ExecutionStatusText`, `ExecutionProgressText`, `ExecutionList`,
`ExecutionTerminal`, `ExecutionCancelButton`, `ExecutionCloseButton`),
cambiando el título a "GENERANDO ISO" y su contenido para reflejar fases del
pipeline en vez de componentes de un `RemovalPlan`. `ExecutionRow` (fila por
componente) se sustituye por `IsoPhaseRow` (fila por fase del pipeline),
reutilizando el mismo `enum ExecutionRowStatus`/mismos iconos (○/⏳/✓/✗/⊘).

**No se ha modificado**: `IsoGenerationPipeline`, `InstallationOptions`
(salvo su integración, tal como permite la sección "NO TOCAR"),
`ComponentCatalog`, `ProtectionEngine`, `ProfileEngine`, `SecurityOptions`,
`RemovalPlanning`, `LabConfigApplier`, `AutounattendGenerator`,
`PostInstall`, `IsoTreeCopier`, `OscdimgRunner`. `RemovalEngine` no se ha
tocado en esta fase (el bypass de 0 seleccionados ya se añadió en P23).

## 3. Flujo con 0 eliminaciones

```
ISO original → EditionCombo.Index → IsoGenerationRequest { RemovalPlan = plan (TotalSelected=0) }
→ IsoGenerationPipeline.GenerateAsync
    → Comprobación de entorno / Validación (P20)
    → Workspace de generación (P19)
    → Copia del árbol completo de la ISO (P19)
    → boot.wim: LabConfig + autounattend según InstallationOptions (P16)
    → install.wim: WorkingImageFactory exporta la edición Pro (P07)
      → RemovalEngine.ExecuteAsync con plan.TotalSelected==0
        → NINGÚN Mount-Wim ni commit (bypass de P23) -- 0 llamadas DISM
    → PostInstall: omitido (Enabled=false, ver limitación en sección 8)
    → Validación final del workspace
    → oscdimg → ISO final
```

`[REMOVAL] Sin eliminaciones de componentes.` se registra explícitamente en
el log (`_logger.Info`) antes de invocar el pipeline, tal como exige el
prompt, y la fase "Aplicando eliminaciones" **no aparece** en la lista de
fases visibles cuando `TotalSelected == 0` (nunca se muestra como ejecutada
si no aplica).

## 4. Flujo con eliminaciones

Idéntico, salvo que `plan.TotalSelected > 0` hace que `RemovalEngine`
**sí** monte, ejecute las acciones permitidas (AppX → Features →
Capabilities → Packages, orden fijo de P07) y haga commit -- el
comportamiento exacto que ya existía, sin ningún cambio de esta fase. La
fase "Aplicando eliminaciones" aparece en la lista y se marca en progreso
mientras `RemovalEngine` ejecuta sus acciones (detectado por el pipeline
alcanzando ≥60% dentro de la fase "Modificando install.wim" -- el punto en
el que `WorkingImageFactory` ya terminó de exportar, según sus propios
tramos de progreso documentados en P19).

Un plan con seleccionados pero todos bloqueados (`TotalSelected > 0 &&
TotalAllowed == 0`) sigue bloqueado por la misma condición ya introducida en
P23 dentro de `PlanConfirmButton_Click`, sin llegar nunca a
`RunIsoGenerationAsync` ni al pipeline.

## 5. Progreso de generación

Se reutiliza el progreso que ya reporta `IsoGenerationPipeline`
(`InstallationProgressInfo`/`InstallationProgressLevel`, P19/P20) tal cual:
**no se ha añadido ninguna abstracción de progreso nueva** porque el
pipeline ya reporta exactamente lo necesario (Stage/Percent/Message/Level).
El código de la UI solo traduce el nombre de fase real del pipeline a una
etiqueta visible de la lista de fases (`MapStageToPhaseLabel`), sin fingir
ningún porcentaje interno de DISM: el `Percent` que se muestra en pantalla
es siempre el mismo que reporta el pipeline, nunca uno inventado en la UI.

`System.Progress<T>` reenvía cada `Report()` al `SynchronizationContext` de
la ventana (capturado al construirlo), así que la barra/lista/terminal se
actualizan siempre en el hilo de la UI sin bloquearla -- mismo patrón que
P10/P22.

## 6. Fases visibles

| Fase mostrada | Se muestra cuando |
|---|---|
| Preparando generación | Siempre (fase de la propia UI, antes de invocar el pipeline) |
| Validando entorno | Siempre |
| Preparando workspace | Siempre |
| Copiando árbol ISO | Siempre |
| Preparando boot.wim | Siempre |
| Preparando install.wim | Siempre |
| Aplicando eliminaciones | Solo si `plan.TotalSelected > 0` |
| Preparando PostInstall | Solo si `PostInstallConfiguration.Enabled` (ver sección 8: por ahora nunca) |
| Validación final | Siempre |
| Creando ISO con oscdimg | Siempre |
| Finalizando | Siempre |
| 100 % completado | Solo al finalizar con éxito |

## 7. Validaciones

Todas las exigidas por el prompt ya las cubre `IsoGenerationPipeline` /
`IsoGenerationRequestValidator` (P19/P20), reutilizadas sin cambios: ISO
origen existe, índice de edición válido, ruta de salida no vacía y distinta
del origen, `InstallationOptions` (incluido el rechazo explícito de
`BypassStorage=true`, P15/P16), PostInstall (coherencia + archivos reales si
está activado, P18), entorno (elevación + oscdimg, P20), y validación final
del workspace (boot.wim/install.wim presentes, P19) antes de invocar
`oscdimg`. Si cualquiera falla, el pipeline no crea ningún workspace ni
toca ningún archivo -- confirmado por los tests ya existentes
(`Invalid_request_aborts_before_creating_any_workspace`) y por el nuevo
`Storage_bypass_is_rejected_before_creating_any_workspace` (sección 9).

## 8. Limitación conocida: PostInstall queda desactivado en esta integración

No existe en la UI ningún mecanismo para seleccionar los instaladores reales
de .NET Desktop Runtime 8.0.26/PCPI (`PostInstallSourceFiles` no tiene
ningún campo poblable desde ninguna pantalla). Activar
`PostInstallConfiguration.Enabled = true` sin eso produciría una solicitud
que la validación de P18 rechazaría siempre (archivos inexistentes),
bloqueando **toda** generación de ISO por defecto -- incluido el caso
principal de esta fase (0 eliminaciones). Añadir un selector de archivos
para esto sería una capacidad nueva fuera del alcance explícito de este
puente ("No añadir nuevas capacidades fuera de este puente UI ->
IsoGenerationPipeline"), así que `RunIsoGenerationAsync` construye la
solicitud con `PostInstallConfiguration.Enabled = false` de forma explícita
y documentada (no silenciosa). `MRS.PostInstall` en sí no se ha tocado:
sigue empaquetando exactamente `.NET Desktop Runtime 8.0.26 x64`,
`PCPI-Retro-Minimals-Portable-0.0.5.exe` y `SetupComplete.cmd` cuando se le
invoca con archivos reales (P18), y el pipeline seguirá integrándolo sin
cambios el día que exista esa UI.

## 9. Tests

**`tests/MRS.ISOEngine.Tests/IsoGenerationPipelineTests.cs`** (3 nuevos):
- `InstallationOptions_are_forwarded_to_the_boot_wim_stage_unmodified` --
  confirma que el objeto `InstallationOptions` que recibe
  `InstallationImageService` es exactamente (`Equals`) el que se puso en la
  solicitud, sin ninguna alteración en el camino.
- `Storage_bypass_is_rejected_before_creating_any_workspace` -- confirma que
  `BypassStorage=true` rechaza la generación antes de crear cualquier
  workspace (`result.Workspace == null`) y sin llamar al copiador del árbol
  de la ISO.
- `A_cancellation_during_the_ISO_tree_copy_propagates_instead_of_reporting_false_success`
  -- confirma que una `OperationCanceledException` durante la copia del
  árbol se propaga (no se atrapa junto a fallos inesperados) y nunca llega a
  invocar `oscdimg`.

**`tests/MRS.ISOEngine.Tests/Fakes/FakeInstallationImageService.cs`**: nuevo
campo `LastOptions` para poder verificar el punto anterior.

**Trazabilidad de los 9 puntos de test pedidos por el prompt**:

1. "Plan vacío → no ejecuta RemovalEngine" -- `RemovalEngineTests.Zero_selected_components_never_calls_DISM_at_all`
   (P23, ya existente): prueba directamente sobre `RemovalEngine` real que
   0 seleccionados no monta ni comprometa nada.
2. "Plan vacío → continúa hacia generación ISO" -- `IsoGenerationPipelineTests.An_explicit_RemovalPlan_with_zero_selected_components_still_reaches_oscdimg`
   (P23, ya existente).
3. "Plan con eliminaciones → conserva el flujo de eliminación" -- cubierto
   por los tests ya existentes de `RemovalEngineTests` (eliminación real de
   AppX/Feature/Capability/Package) y por `IsoGenerationPipelineTests.A_full_successful_run_calls_every_stage_in_order_and_produces_the_final_ISO`;
   ninguno se modificó porque el comportamiento no cambió.
4. "Plan con seleccionados bloqueados → no genera" -- lógica de UI pura en
   `PlanConfirmButton_Click` (ya probada indirectamente vía
   `RemovalPlan.TotalSelected`/`TotalAllowed`, exhaustivamente cubiertos en
   `RemovalPlanBuilderTests`); no hay proyecto de test para
   `MRS.WindowsBuilder` (decisión documentada desde P17), así que no se creó
   uno nuevo solo para esta condición de una línea -- verificable por
   lectura de código (idéntica a la de P23, sin cambios en esta fase).
5. "Storage bypass → generación rechazada" -- nuevo test de esta fase,
   sección 9 arriba.
6. "InstallationOptions se transmite sin alteraciones" -- nuevo test de esta
   fase, sección 9 arriba.
7. "La UI no crea un segundo pipeline" -- verificado por revisión de código:
   `MainWindow` construye exactamente **una** instancia de
   `IsoGenerationPipeline` (en el constructor) y `RunIsoGenerationAsync` es
   el único método que la invoca (`_isoGenerationPipeline.GenerateAsync`);
   no queda ningún otro camino que reimplemente workspace/árbol/boot.wim/
   install.wim/PostInstall/validación/oscdimg por su cuenta (la
   implementación parcial de P07 se eliminó, no se dejó en paralelo).
8. "Fallo de prevalidación no inicia generación" -- `IsoGenerationPipelineTests.Invalid_request_aborts_before_creating_any_workspace`
   (P19, ya existente) + el nuevo test de Storage bypass (punto 5).
9. "Cancelación/fallo no informa éxito falso" -- nuevo test de esta fase
   (cancelación, sección 9 arriba) + los tests ya existentes que confirman
   `Success=false`/`OutputIsoPath=null` ante cualquier fallo de fase
   (`A_boot_wim_failure_aborts...`, `An_install_wim_failure_aborts...`,
   `Oscdimg_failure_is_reported...`, `The_final_ISO_is_never_produced_if_any_stage_fails`).

## 10. Resultado de tests

```
dotnet test MRS-Windows-Builder.sln
```

```
MRS.InstallationOptions.Tests : 17/17
MRS.ProfileEngine.Tests       : 21/21
MRS.ImageEngine.Tests         : 88/88
MRS.PostInstall.Tests         : 44/44
MRS.ComponentCatalog.Tests    : 59/59
MRS.RemovalPlanning.Tests     : 48/48
MRS.RemovalEngine.Tests       : 52/52
MRS.ISOEngine.Tests           : 106/106  (103 previos + 3 nuevos de P24)
```

Total: **435/435**, 0 fallos. Ninguno de los tests existentes se modificó.

## 11. Resultado del build

```
dotnet build src/MRS.WindowsBuilder/MRS.WindowsBuilder.csproj
```

`MainWindow.xaml`/`MainWindow.xaml.cs` **compilan sin ningún error de
compilador** (0 `errorCS*`) tras añadir las referencias a `MRS.ISOEngine`/
`MRS.PostInstall` y reescribir el puente UI -> pipeline. El único fallo que
queda es de nuevo (igual que en P23) un `MSB3027`/`MSB3021` al copiar
`apphost.exe`/las DLL de dependencias al directorio de salida, porque una
instancia de `MRS.WindowsBuilder.exe` sigue abierta en esta sesión (esta vez
con un PID distinto al de P23 -- parece reaparecer entre sesiones) y las
tres formas de cerrarla desde este entorno (`Stop-Process -Force`, `taskkill
/F`, `CloseMainWindow()`) devuelven "Acceso denegado", igual que en P23.
`dotnet build MRS-Windows-Builder.sln` (todos los proyectos) y
`dotnet test MRS-Windows-Builder.sln` (que no depende de
`MRS.WindowsBuilder`) sí terminan limpios.

Para confirmar una compilación completa de `MRS.WindowsBuilder.exe`: cerrar
manualmente cualquier ventana de esa aplicación que quede abierta y repetir
`dotnet build`.

## Commit recomendado

`git commit` con el resumen "P24: conecta MainWindow con
IsoGenerationPipeline real -- sustituye la ejecución parcial de P07 (solo
RemovalEngine) por la generación completa de ISO (workspace, árbol,
boot.wim, install.wim, PostInstall, validación, oscdimg); pantalla
GENERANDO ISO reutiliza el estilo de CREANDO IMAGEN con fases del pipeline;
435/435 sin regresiones (build de MRS.WindowsBuilder bloqueado solo por un
proceso previo de esta sesión, no por un error de código)" y `git push` a
`main`.

## ¿Queda lista la generación real para probarse?

**Sí, con una limitación clara.** El puente UI → `IsoGenerationPipeline` ya
está completo y probado (435/435, incluida la ruta de 0 eliminaciones que es
el caso principal de esta fase). Para que un usuario genere de verdad una
ISO desde la ventana:

1. **Necesita sesión elevada y Windows ADK instalado** (bloqueos ya
   documentados en P16/P17/P19/P20 -- sin cambios en esta fase; el pipeline
   los comprueba y aborta con mensajes claros si faltan, no antes de
   intentar generar nada).
2. **PostInstall no se incluirá** en ninguna generación desde la UI hasta
   que exista un selector de instaladores reales (sección 8) -- toda ISO
   generada desde `MainWindow` se genera hoy sin `$OEM$`/`SetupComplete.cmd`,
   documentado explícitamente, nunca simulado.
3. No se ha probado la generación real de extremo a extremo en esta sesión
   (mismo motivo: sin sesión elevada ni ADK disponibles aquí, ver P20/P23) --
   la integración se ha verificado con la suite automatizada (435/435, DISM/
   oscdimg simulados), no con una ISO real generada.
