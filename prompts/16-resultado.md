# Resultado - P16: implementación real (boot.wim, LabConfig y OOBE)

## Resumen

P15 diseñó/investigó; P16 implementa de verdad, sobre una **copia** de
`boot.wim` dentro de un workspace propio — nunca la ISO original. Se añaden:
un editor de registro offline (`reg.exe` sobre el hive `SYSTEM` de una copia
montada de `boot.wim`), un aplicador de `LabConfig` (TPM/Secure Boot/CPU/RAM),
un generador determinista de `autounattend.xml` (cuenta local), y el
servicio orquestador `InstallationImageService` que conecta todo siguiendo
exactamente el diagrama del prompt.

**No se pudo ejecutar la prueba real controlada de la sección 17** dentro de
esta sesión: DISM (`Mount-Wim`) exige privilegios elevados y esta sesión no
se ejecuta elevada (confirmado con `Error: 740` al intentar
`dism /Get-MountedImageInfo`). Se documenta esto de forma honesta, sin
fingir una validación que no ocurrió — ver "Resultado de la prueba real" y
"Prueba recomendada" más abajo. Todo el código está, en cambio, **validado
mediante tests con DISM/registro simulados** (nunca con texto de salida como
criterio de éxito), siguiendo el mismo patrón que el resto del proyecto.

## Distinción IMPLEMENTADO / VALIDADO / PENDIENTE / NO IMPLEMENTADO

| Mecanismo | Estado |
|---|---|
| Bypass TPM 2.0 (`LabConfig\BypassTPMCheck`) | **IMPLEMENTADO** y **VALIDADO** (tests con registro offline simulado). Pendiente de confirmación sobre un `boot.wim` real (sin sesión elevada disponible). |
| Bypass Secure Boot (`BypassSecureBootCheck`) | Igual que TPM. |
| Bypass CPU (`BypassCPUCheck`) | Igual que TPM. |
| Bypass RAM (`BypassRAMCheck`) | Igual que TPM. Ver distinción obligatoria sobre "2 GB" más abajo. |
| Cuenta local (`autounattend.xml`) | **IMPLEMENTADO** y **VALIDADO** (XML válido, determinista, sin credenciales hardcoded). |
| OOBE sin conexión (`BypassNRO`) | Mecanismo **IMPLEMENTADO** (`LabConfigApplier.ApplyOfflineOobeBypassAsync`) pero **PENDIENTE de mecanismo fiable confirmado para esta build**: no se invoca automáticamente desde el flujo principal. |
| Bypass de almacenamiento | **NO IMPLEMENTADO**. La UI lo muestra deshabilitado y `InstallationExecutionValidator` bloquea la ejecución si se activara por cualquier otra vía. |
| Ejecución real sobre un `boot.wim` de la ISO objetivo (26H2 build 26300.9278) | **PENDIENTE**: bloqueado por falta de sesión elevada en este entorno (ver "Resultado de la prueba real"). |

## Mecanismo utilizado para LabConfig

`HKLM\SYSTEM\Setup\LabConfig` dentro del hive `SYSTEM` **offline** de la copia
de `boot.wim` del workspace:

1. `DISM /Mount-Wim /WimFile:<workspace>\boot.wim /Index:1 /MountDir:<mount> `
   (sin `/ReadOnly`: es una copia de trabajo, nunca la ISO original).
2. `reg load HKLM\MRS_ISOENGINE_SYSTEM_<sufijo> "<mount>\Windows\System32\config\SYSTEM"`.
3. Por cada opción **activada**: `reg add "HKLM\...\Setup\LabConfig" /v Bypass<X>Check /t REG_DWORD /d 1 /f`.
   Por cada opción **desactivada**: `reg delete "HKLM\...\Setup\LabConfig" /v Bypass<X>Check /f`
   (idempotencia — nunca deja una clave de una ejecución anterior si ahora está desactivada).
4. Verificación: `reg query` de las 4 claves, comparando presencia/ausencia
   contra `InstallationOptions` (no solo `ExitCode 0` de DISM: se relee el
   registro después de escribir).
5. `reg unload HKLM\MRS_ISOENGINE_SYSTEM_<sufijo>` — **siempre**, en un
   `finally`, incluso si un paso anterior lanzó una excepción.
6. Si todo fue bien: `DISM /Unmount-Wim /Commit`. Si algo falló (carga del
   hive, aplicación, verificación, o el propio descargado del hive):
   `DISM /Unmount-Wim /Discard`.

La clave temporal (`MRS_ISOENGINE_SYSTEM_<8 hex aleatorios>`) evita
colisionar con un hive que hubiera quedado cargado de una ejecución anterior
interrumpida — nunca se reutiliza un nombre fijo.

## Cómo se monta el hive / cómo se valida

Ver arriba (pasos 2 y 4). La verificación posterior (sección 12 del prompt)
relee el registro con `reg query` para cada una de las 4 claves de
compatibilidad: si una opción está activada y la clave no aparece, o está
desactivada y la clave sigue presente, se reporta como error y el resultado
es "no exitoso" (lo que fuerza `Discard`, nunca `Commit`). Nunca se valida
solo mirando el `ExitCode` de DISM.

## Mecanismo de cuenta local

`autounattend.xml` (pase `oobeSystem`, componente
`Microsoft-Windows-Shell-Setup`): `<UserAccounts><LocalAccounts><LocalAccount>`
con `Name`/`Group=Administrators`/`DisplayName`, y `<Password>` **solo** si
se proporciona una contraseña en tiempo de ejecución (nunca hay una
constante de contraseña en el código ni en el repositorio — verificado con
un test que analiza los propios archivos fuente). Es el mecanismo
**soportado oficialmente** por Windows Setup para instalación desatendida,
no un hack de una pantalla concreta.

**Ubicación**: en la raíz de la ISO/medio de arranque. Windows Setup busca
`autounattend.xml` automáticamente en la raíz de cualquier unidad extraíble
disponible al arrancar — no es necesario copiarlo dentro de `install.wim` ni
de `boot.wim`. Si el usuario ya aporta su propio archivo de respuesta, ese
tiene prioridad (Setup usa el primero que encuentra según su orden de
búsqueda estándar); MRS nunca debería sobrescribir uno que el usuario ya
haya puesto ahí. Este autounattend controla únicamente el paso
`oobeSystem` (cuenta local, nombre de equipo, qué pantallas de OOBE se
ocultan) — no toca `windowsPE` ni `specialize`.

Generación **determinista**: `AutounattendGenerator.Generate` con la misma
`AutounattendConfiguration` produce siempre el mismo XML byte a byte
(verificado con un test). `InstallationImageService` siempre escribe al
mismo `autounattend.xml` (sobrescribe, nunca duplica) y lo **elimina** si
`AllowLocalAccount` se desactiva en una ejecución posterior (para que no
quede un archivo de una ejecución anterior imponiendo una cuenta local no
deseada).

## Resultado de BypassNRO (OOBE sin conexión)

Sección 7 del prompt exige determinar, antes de implementar, si el
mecanismo sigue siendo válido en Windows 11 26H2 build 26300.9278. **No se
pudo confirmar empíricamente** dentro de esta sesión: hacerlo con
confianza real exigiría aplicarlo sobre un `boot.wim` de esa build concreta
y arrancarlo en una VM hasta la pantalla de red de OOBE — exactamente el
paso bloqueado por la falta de sesión elevada (ver más abajo). Se ha optado
por la vía honesta que el propio prompt permite: **el mecanismo está
implementado** (`LabConfigApplier.ApplyOfflineOobeBypassAsync`, con sus
propios tests) pero **no se invoca automáticamente** desde
`InstallationImageService`/`BootWimModifier` — la opción
`AllowOfflineOobe` sigue generando la parte de `autounattend.xml` que oculta
la configuración de red en OOBE (mecanismo del archivo de respuesta, más
estable entre builds), pero el DWORD `BypassNRO` específico queda
**pendiente de confirmación fiable para esta build**, marcado así en el
planificador (`InstallationActionStatus.PendingReliableMechanism`) y
documentado aquí en vez de aplicado a ciegas.

## Estado del bypass de almacenamiento

Sigue **sin mecanismo fiable confirmado** (igual que P15 concluyó). P16 lo
trata explícitamente como *no implementado*, no como *silenciosamente
ignorado*:

- `InstallationConfigurationPlanner` marca su acción como
  `InstallationActionStatus.NotImplemented`.
- `InstallationExecutionValidator.Validate` **bloquea** la ejecución con el
  mensaje exacto pedido por el prompt: *"El bypass de almacenamiento no
  está implementado/validado para esta build."* si la opción estuviera
  activada por cualquier vía.
- En la UI (pantalla PLAN DE MODIFICACIÓN), la casilla correspondiente está
  **deshabilitada y desmarcada** con una etiqueta roja "⚠ No
  implementado/validado para esta build" — nunca puede aparentar estar
  operativa ni activarse desde la interfaz.

## Bypass de RAM y 2 GB

Se mantiene exactamente la distinción de P15 (ver también
`prompts/15-resultado.md`): el bypass **evita el bloqueo del instalador**,
nunca se ha prometido ni se promete aquí que "Windows 11 funcione
correctamente con 2 GB". No se ha podido validar con una VM de 2 GB dentro
de esta sesión (mismo bloqueo de elevación); queda como parte de la "Prueba
recomendada" más abajo, sin inventar un resultado. El texto de la UI (P15,
sin cambios en P16) ya distingue ambas cosas explícitamente.

## Archivos modificados/creados

**`MRS.ISOEngine`** (nuevo código; referencia ahora a `MRS.DismEngine` y
`MRS.ImageEngine` además de `MRS.InstallationOptions`):
- `Registry/IOfflineRegistryEditor.cs`, `Registry/OfflineRegistryEditor.cs` — abstracción sobre `reg.exe` (cargar/escribir/leer/eliminar/descargar un hive offline).
- `Configuration/LabConfigApplier.cs` — aplica/retira los 4 bypasses; `ApplyOfflineOobeBypassAsync` separado (pendiente); `VerifyAsync`.
- `Configuration/InstallationExecutionValidator.cs` — bloquea si hay alguna opción `NotImplemented` activada.
- `Autounattend/AutounattendGenerator.cs` — genera/valida `autounattend.xml`.
- `BootWim/IBootWimProvisioner.cs`, `BootWim/BootWimProvisioner.cs` — copia `boot.wim` desde la ISO montada (solo lectura) al workspace, idempotente.
- `BootWim/IBootWimModifier.cs`, `BootWim/BootWimModifier.cs` — ciclo Mount→hive→aplicar→verificar→hive→Commit/Discard.
- `IInstallationImageService.cs`, `InstallationImageService.cs` — orquestador principal (diagrama de la sección 10).
- `GenerationWorkspaceFactory.cs` — crea un `GenerationWorkspace` real en disco (mismo patrón que `InventoryWorkspace`).
- `Exceptions/IsoEngineException.cs`.
- `Models/`: `InstallationProgressLevel.cs`, `InstallationProgressInfo.cs`, `InstallationActionStatus.cs`, `AutounattendConfiguration.cs`, `AutounattendValidationResult.cs`, `BootWimModificationResult.cs`, `InstallationImageResult.cs`; `GenerationWorkspace.cs` ampliado con `MountPath`/`WorkspaceId`; `InstallationConfigurationAction.cs` ampliado con `Status`.
- `Configuration/InstallationConfigurationPlanner.cs` (P15) — marca "Offline OOBE" como `PendingReliableMechanism` y "Storage" como `NotImplemented`.
- `MRS.ISOEngine.csproj` — referencias a `MRS.DismEngine`/`MRS.ImageEngine`.

**`MRS.WindowsBuilder`**:
- `MainWindow.xaml` — la casilla de almacenamiento (pantalla PLAN DE MODIFICACIÓN, sección de P15) pasa a `IsEnabled="False"`/desmarcada, con la etiqueta de advertencia exacta.
- `MainWindow.xaml.cs` — `_installationOptions` empieza con `BypassStorage = false` (la UI nunca permite activarlo, aunque el valor por defecto conceptual del modelo siga siendo `true` según P15/P16 sección 2).

**Tests** (`tests/MRS.ISOEngine.Tests/`, +55 sobre los 16 de P15):
`Fakes/FakeDismRunner.cs`, `Fakes/FakeOfflineRegistryEditor.cs`,
`Fakes/FakeIsoMounter.cs`, `Fakes/FakeProcessRunner.cs`,
`OfflineRegistryEditorTests.cs`, `LabConfigApplierTests.cs`,
`AutounattendGeneratorTests.cs`, `InstallationExecutionValidatorTests.cs`,
`BootWimProvisionerTests.cs`, `BootWimModifierTests.cs`,
`InstallationImageServiceTests.cs`.

No se ha modificado `MRS.RemovalEngine`, `MRS.RemovalPlanning`,
`MRS.ComponentCatalog`, `MRS.ProfileEngine`, `SecurityOptions`, el catálogo
de componentes, las reglas de protección, la lógica de dependencias/
eliminación, el ciclo Mount/Execute/Verify/Commit/Discard de
`RemovalEngine`, la telemetría de creación de imagen (P10) ni la lógica de
inventario de P09.

## Decisión de alcance: sin botón de ejecución real en la UI todavía

La sección 15 del prompt pide "no rediseñar" la UI existente y solo
"mostrar claramente" el estado de las opciones — no pide añadir un nuevo
botón que dispare `InstallationImageService.ApplyAsync` contra una ISO real
elegida por el usuario. No existe todavía una pantalla de "generación de
ISO" (esa sigue siendo una fase posterior explícitamente fuera de alcance:
`oscdimg`, PostInstall, PCPI). Por eso `MRS.WindowsBuilder` no referencia
`MRS.ISOEngine`: la UI solo captura `InstallationOptions`, y el servicio
real queda "integrado" en el sentido de estar terminado, probado y listo
para conectarse a un botón real en la fase que sí construya esa pantalla —
tal como permite la sección 15 ("si la arquitectura actual todavía no tiene
generación real, integrar el servicio sin crear todavía todo el ISOEngine
final").

## Arquitectura (idempotencia, seguridad, workspace)

- **Idempotencia** (sección 11): `LabConfigApplier` actualiza claves
  existentes deterministamente (`reg add /f` sobrescribe; `reg delete /f`
  retira sin error si no existía); `InstallationImageService` nunca duplica
  `autounattend.xml` (mismo archivo, sobrescrito); nunca se generan dos
  montajes simultáneos del mismo `boot.wim` (todo el ciclo es secuencial y
  transaccional). Verificado con tests (`Applying_twice_with_the_same_options_is_idempotent`,
  `Running_twice_with_the_same_options_never_duplicates_the_autounattend_file`).
- **Seguridad/workspace** (sección 13, patrón de P09): `GenerationWorkspaceFactory`
  crea un GUID propio bajo `%LOCALAPPDATA%\MRS-Windows-Builder\iso-workspaces\`
  (nunca una letra de unidad fija); `BootWimProvisioner` monta la ISO
  original solo en lectura y libera el montaje siempre (`IAsyncDisposable`,
  incluso ante excepción — verificado con test); `BootWimModifier` nunca
  deja el hive de registro cargado (`finally`, verificado con test); ante un
  fallo, el workspace y la copia de `boot.wim` **no se borran** (verificado
  con test) — nunca se usa `/Cleanup-Mountpoints` ni se borra un directorio
  de montaje a ciegas.
- **Progreso**: `InstallationProgressInfo`/`InstallationProgressLevel`, mismo
  patrón que P10 (Stage/Percent/Message/Level/Timestamp), pero como tipo
  propio de `MRS.ISOEngine` — deliberadamente sin referenciar
  `MRS.RemovalEngine.Models.ProgressInfo`, siguiendo el mismo principio de
  independencia entre motores ya aplicado a `SecurityOptions` (P13/P15).
  Nunca se simula el progreso interno de DISM: solo se reporta antes/después
  de cada paso real (montar, cargar hive, aplicar, verificar, confirmar).

## Tests

**LabConfig** (`LabConfigApplierTests`): TPM activado/desactivado, Secure
Boot activado/desactivado, CPU activado/desactivado, RAM activado/
desactivado, combinaciones (TPM+RAM sin los otros dos), ausencia de claves
no solicitadas, actualización idempotente, retirada de una clave al
desactivar una opción antes activa, verificación post-aplicación, y
comprobación de que `ApplyOfflineOobeBypassAsync` nunca se invoca desde
`ApplyAsync`.

**Autounattend** (`AutounattendGeneratorTests`): XML bien formado, cuenta
local con el nombre configurado, ausencia de `<Password>`/credenciales
cuando no se proporciona ninguna, inclusión solo cuando se proporciona en
tiempo de ejecución, ausencia de constantes de contraseña en el código
fuente (test que analiza los propios `.cs`), generación determinista,
efecto de `AllowOfflineOobe` sobre las opciones de red del XML, y
validación de nombre de cuenta/equipo.

**Validación** (`InstallationExecutionValidatorTests` +
`GenerationWorkspaceValidatorTests` de P15, sin cambios): configuración
válida, bypass de almacenamiento bloqueado con el mensaje exacto,
almacenamiento desactivado no bloquea el resto — más boot.wim/install.wim
inexistentes, workspace igual a la ISO origen, índice inválido y
arquitectura incompatible (heredados de P15, siguen pasando).

**Seguridad** (`BootWimModifierTests`, `BootWimProvisionerTests`,
`InstallationImageServiceTests`): hive descargado tras un fallo de carga o
de descarga, mount de la ISO liberado incluso si falta `boot.wim` dentro,
workspace/copia de `boot.wim` preservados tras un fallo, y la ISO
"original" (carpeta que hace de ISO montada en los tests) nunca se
modifica.

**Registro offline** (`OfflineRegistryEditorTests`): cada método traduce al
comando `reg.exe` exacto esperado, y el éxito se decide solo por
`ExitCode`, nunca por el texto de la salida.

Se ejecutaron también todos los tests existentes (P07-P15).

## Resultado de `dotnet test`

```
MRS.InstallationOptions.Tests : 17/17
MRS.ISOEngine.Tests            : 71/71  (+55 sobre P15)
MRS.ProfileEngine.Tests       : 21/21
MRS.ImageEngine.Tests         : 78/78
MRS.ComponentCatalog.Tests    : 59/59
MRS.RemovalPlanning.Tests     : 48/48
MRS.RemovalEngine.Tests       : 48/48
```

Total: **342/342**. Sin regresiones.

## Resultado de `dotnet build`

`dotnet build MRS-Windows-Builder.sln`: **0 errores, 0 advertencias** en
toda la solución.

## Resultado de la prueba real (sección 17)

**No ejecutada.** Antes de tocar código se comprobó la disponibilidad real:

- Se localizaron dos ISO reales en el escritorio del usuario, una de ellas
  muy próxima a la build objetivo
  (`26300.8697.260612-2005.GE_PRERELEASE_IM_CLIENTMULTI_X64FRE_ES-ES.ISO`,
  build 26300.8697 — no exactamente 26300.9278 — y una ISO ya personalizada
  del propio usuario). Se confirmó que **montar una ISO como unidad**
  (`Mount-DiskImage`, de solo lectura) funciona sin privilegios elevados en
  esta sesión.
- Se confirmó que **DISM exige elevación**: `dism /Get-MountedImageInfo`
  (una operación de solo lectura) devolvió `Error: 740 — Se requieren
  permisos elevados para ejecutar DISM.` Esta sesión no se ejecuta como
  administrador, y no hay manera de aceptar un cuadro de diálogo UAC de
  forma no interactiva desde aquí.
- Por tanto, `Mount-Wim`/`Unmount-Wim` (necesarios para `BootWimModifier`) y
  `reg load`/`reg unload` sobre un hive offline (que también requieren
  privilegios elevados) no se pudieron ejecutar dentro de esta sesión.

No se ha fingido ningún resultado de esta prueba. Todo lo que sí se pudo
verificar (comportamiento correcto de cada pieza, aislado, con DISM/
registro simulados de forma realista) está en la sección de tests, que
**sí** pasa 71/71 sin ningún resultado inventado.

## Limitaciones

- El mecanismo de OOBE sin conexión (`BypassNRO`) está implementado pero no
  activado automáticamente: pendiente de una prueba real en la build
  26300.9278 exacta.
- El bypass de almacenamiento sigue sin mecanismo fiable conocido.
- Ninguno de los mecanismos de compatibilidad (TPM/Secure Boot/CPU/RAM) se
  ha podido confirmar sobre un `boot.wim` real dentro de esta sesión, por
  la falta de una sesión elevada — solo se ha verificado el código con
  fakes deterministas.
- No existe todavía ningún botón/pantalla en la aplicación que dispare
  `InstallationImageService` contra una ISO real elegida por el usuario
  (decisión de alcance explícita, ver arriba).

## Prueba recomendada (en VM, con sesión elevada)

1. Ejecutar la aplicación (o un pequeño programa de prueba) **como
   administrador**, apuntando `InstallationImageService.ApplyAsync` a una
   copia de la ISO real de la build 26300.9278 x64 es-ES Pro, índice 2.
2. Confirmar tras la ejecución: `Get-MountedWimInfo` no muestra ningún
   montaje de MRS pendiente; el hive `MRS_ISOENGINE_SYSTEM_*` no queda
   cargado (`reg query HKLM` no lo lista); la ISO original no cambió
   (hash/tamaño idéntico); el workspace contiene `boot.wim` modificado y
   `autounattend.xml`.
3. Montar offline el `boot.wim` resultante y comprobar con `reg query` que
   `LabConfig\BypassTPMCheck`/`BypassSecureBootCheck`/`BypassCPUCheck`/
   `BypassRAMCheck` valen `1` y que no existe ninguna clave de
   almacenamiento.
4. Regenerar una ISO de prueba (fuera del alcance de P16) y arrancarla en
   una VM sin TPM/Secure Boot virtual y con 2 GB de RAM, para confirmar que
   Setup no bloquea la instalación; completar OOBE y confirmar que aparece
   la opción de cuenta local sin pedir cuenta Microsoft.
5. Documentar el resultado observado (positivo o negativo) sin ocultar
   nada, especialmente para BypassNRO y para el comportamiento real de
   Windows con 2 GB de RAM tras la instalación.

## Commit recomendado

`git commit` con el resumen "P16: implementación real de LabConfig/
autounattend sobre boot.wim (BootWimModifier, LabConfigApplier,
AutounattendGenerator, InstallationImageService) — prueba real pendiente
por falta de sesión elevada" y `git push` a `main`.
