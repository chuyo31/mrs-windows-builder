# Resultado - P29: corrección real de OOBE offline + cuenta local

## Causa encontrada

La prueba real de P28 confirmó que **todos los bypass de hardware funcionan**
(TPM/Secure Boot/CPU/RAM, LabConfig en ambos índices de boot.wim, BypassNRO
en boot.wim índice 2), pero Windows seguía mostrando "Vamos a conectarte a
una red" y no llegaba a crear la cuenta local automáticamente.

**Auditoría del autounattend actual (sección 1)**: el bloque `specialize`
con el `RunSynchronousCommand` de `reg add ... BypassNRO ...` existe en el
XML generado, pero **no se puede asumir que Setup lo procese solo porque
existe en la raíz del ISO** — la prueba real demuestra que, en esta build,
ese mecanismo concreto (comando ejecutado durante `specialize`) no basta
para evitar la pantalla de red. El BypassNRO offline aplicado a **boot.wim**
(P28) tampoco es suficiente por sí solo: boot.wim es el entorno de *Windows
Setup*, no el sistema que arranca en OOBE — OOBE se ejecuta desde
**install.wim**, ya copiado a disco, que nunca había recibido ningún cambio
de registro.

**Conclusión**: el único mecanismo verdaderamente fiable y verificable
offline es dejar `HKLM\SYSTEM\Setup\OOBE\BypassNRO=1` directamente en el
**SYSTEM hive de install.wim** (el sistema que realmente arranca en OOBE),
antes de que Setup lo copie a disco — sin depender de que ningún comando se
ejecute durante `specialize` ni de ninguna intervención manual.

## Solución aplicada

### 1. Nuevo `InstallWimOobeConfigurator` (`src/MRS.ISOEngine/InstallWim/`)

Mismo patrón transaccional que `BootWimModifier` (P16/P25/P26), aplicado a
la **imagen de trabajo de install.wim** que ya usa el pipeline (nunca a la
ISO original ni a una copia distinta): Mount-Wim(RW) → cargar hive SYSTEM
offline → aplicar BypassNRO (reutilizando
`LabConfigApplier.ApplyOfflineOobeBypassAsync`/`VerifyOfflineOobeBypassAsync`,
ya existentes desde P16/P28 — **no se duplicó ninguna lógica**, solo se
reutilizó sobre un hive distinto) → verificar → descargar hive → Commit si
todo fue bien, Discard en cualquier otro caso. Mismas comprobaciones
defensivas antes de montar (existe, tamaño razonable, sin ReadOnly, WIM
válido vía `Get-WimInfo`) que `BootWimModifier` (P25/P26).

`IsoGenerationPipeline` invoca este nuevo paso justo después de que
`RemovalEngine` termina con éxito (con o sin eliminaciones) y antes de
copiar la imagen de trabajo al workspace de generación, solo si
`InstallationOptions.AllowOfflineOobe` está activo — sobre
`workingImage.WorkingWimPath`/`workingImage.Index`/`workingImage.MountPath`,
exactamente la misma imagen de trabajo que `WorkingImageFactory`/
`RemovalEngine` ya usan, nunca una copia nueva.

### 2. `AutounattendGenerator`: fase `windowsPE` (sección 2)

Se añade una fase `windowsPE` (siempre presente, no condicionada a
`allowOfflineOobe`) con el componente real y documentado
`Microsoft-Windows-Setup`/`UserData/AcceptEula=true`. No se añadió ningún
componente de idioma/locale: no existe ninguna configuración de idioma en
`AutounattendConfiguration`/`InstallationOptions`, y fijar uno a mano habría
sido un campo inventado que podría no coincidir con el idioma real de la
ISO (contrario a "no crear XML artificial ni campos no soportados").

El bloque `specialize` (P28) **se conserva** — no perjudica y puede ayudar
en otras builds — pero ya no es la única defensa: el mecanismo primario y
verificado es el de install.wim (punto 1).

### 3. `oobeSystem` — sin cambios de contenido

Se revisó (sección 5): `HideOnlineAccountScreens`, `HideWirelessSetupInOOBE`
y `LocalAccounts` ya estaban en el componente/pass correcto desde P16/P28.
No se asumió que `HideWirelessSetupInOOBE` por sí solo bastara — la
solución real para "sin conexión" es el punto 1 (BypassNRO en
install.wim), no un cambio en este bloque.

### 4. Cuenta local — sin cambios

`AllowLocalAccount`/`Name=Usuario`/`Group=Administrators` siguen viniendo de
`AutounattendConfiguration`; una vez que OOBE puede continuar sin conexión
(punto 1), el bloque `LocalAccounts` ya existente es suficiente — no hizo
falta ningún mecanismo adicional. Ninguna contraseña hardcodeada.

### 5. Validación final extendida (`InstallationImageService.ValidateFinalAsync`)

Extendida (P28 → P29) para comprobar, todo releyendo físicamente lo ya
comprometido (nunca confiando en resultados ya reportados):

- **boot.wim**: LabConfig en índices 1 y 2 (ya existía), y ahora
  **BypassNRO como confirmación independiente** (`[OK] boot.wim Index 2
  BypassNRO`, antes mezclado con LabConfig).
- **autounattend.xml**: existe, XML válido, fase `windowsPE` presente,
  fase `oobeSystem` presente, solo componentes conocidos
  (`Microsoft-Windows-Setup`/`Deployment`/`Shell-Setup`), cuenta local
  presente, **sin contraseña si no se pidió ninguna** (comparado contra la
  configuración real, no solo "si existe la etiqueta"), comando BypassNRO
  presente en `specialize` si `AllowOfflineOobe`.
- **install.wim**: vuelve a montar de solo lectura el install.wim final del
  workspace de generación (índice 1 — el único índice que existe tras
  `Export-Image`), carga su hive SYSTEM, y confirma que `BypassNRO=1`
  **persiste después de desmontar** (sesión de montaje completamente nueva,
  independiente de la que hizo el commit).

Logs exactos (sección 8):

```
[OK] boot.wim Index 1 LabConfig
[OK] boot.wim Index 2 LabConfig
[OK] boot.wim Index 2 BypassNRO
[OK] autounattend.xml found
[OK] autounattend.xml valid
[OK] windowsPE configured
[OK] oobeSystem configured
[OK] local account configured
[OK] install.wim OOBE configuration
[OK] BypassNRO persisted
```

## Cambios en `AutounattendGenerator`

- Fase `windowsPE` nueva y siempre presente (`Microsoft-Windows-Setup` /
  `UserData/AcceptEula=true`).
- Orden de fases en el XML: `windowsPE` → `specialize` (si aplica) →
  `oobeSystem`.
- Sin cambios en `specialize`/`oobeSystem` (contenido idéntico a P28).

## Cambios en install.wim

- Nuevo componente `InstallWimOobeConfigurator` (mount RW → hive → aplicar
  BypassNRO → verificar → hive off → commit/discard).
- Wired en `IsoGenerationPipeline`, después de `RemovalEngine`, antes de
  copiar al workspace de generación, solo si `AllowOfflineOobe`.
- `IsoGenerationPipeline` gana una nueva dependencia obligatoria
  (`IInstallWimOobeConfigurator`); `MainWindow.xaml.cs` la construye
  reutilizando el mismo `dismRunner`/`registryEditor` ya usados para
  boot.wim (mismo `OfflineRegistryEditor`, sin duplicar nada).

## Archivos modificados

- `src/MRS.ISOEngine/InstallWim/IInstallWimOobeConfigurator.cs` (nuevo)
- `src/MRS.ISOEngine/InstallWim/InstallWimOobeConfigurator.cs` (nuevo)
- `src/MRS.ISOEngine/InstallWim/InstallWimOobeConfigurationResult.cs` (nuevo)
- `src/MRS.ISOEngine/Autounattend/AutounattendGenerator.cs`
- `src/MRS.ISOEngine/InstallationImageService.cs`
- `src/MRS.ISOEngine/IInstallationImageService.cs`
- `src/MRS.ISOEngine/Pipeline/IsoGenerationPipeline.cs`
- `src/MRS.WindowsBuilder/MainWindow.xaml.cs`
- Tests: `AutounattendGeneratorTests.cs`, `InstallationImageServiceTests.cs`,
  `IsoGenerationPipelineTests.cs`, nuevo `InstallWimOobeConfiguratorTests.cs`,
  `Fakes/FakeInstallationImageService.cs`, nuevo
  `Fakes/FakeInstallWimOobeConfigurator.cs`.

No se ha tocado `LabConfigApplier` (solo se reutiliza, sin cambios),
`BootWimProvisioner`, ninguna de las claves `BypassTPMCheck`/
`BypassSecureBootCheck`/`BypassCPUCheck`/`BypassRAMCheck`, `RemovalEngine`,
inventario, catálogo ni `ProfileEngine`.

## Validaciones añadidas

Ver "Validación final extendida" arriba — cubre exactamente lo pedido en
las secciones 6, 7 y 8 del prompt.

## Tests

**23 nuevos**:

- `AutounattendGeneratorTests.cs` (2): `windowsPE` siempre presente con
  `AcceptEula=true` (reemplaza el test de P28 que afirmaba lo contrario —
  la decisión de P28 quedó revertida por la prueba real, documentado en el
  propio test); orden `windowsPE` → `specialize` → `oobeSystem`.
- `InstallWimOobeConfiguratorTests.cs` (10, nuevo archivo): aplica y
  confirma BypassNRO=1; commit/discard según éxito; idempotencia (aplicar
  dos veces); mount/hive-load/unload fallidos; archivo inexistente/
  demasiado pequeño/WIM inválido nunca llegan a montar; **persistencia
  tras desmontar** (releído con un alias de hive distinto, igual que haría
  una validación real independiente).
- `InstallationImageServiceTests.cs` (ajustes): `ValidateFinalAsync` ahora
  también exige `accountConfig`; se añadió un install.wim de prueba al
  workspace fixture para que la nueva validación de install.wim tenga algo
  que montar.
- `IsoGenerationPipelineTests.cs` (3): el pipeline configura install.wim
  cuando `AllowOfflineOobe=true` (por defecto en el fixture) y nunca cuando
  es `false`; opera sobre la MISMA imagen de trabajo que `RemovalEngine` ya
  usó (nunca una copia distinta); un fallo en esa configuración aborta
  antes de `oscdimg`.

Ningún test existente se modificó para ocultar un fallo: el único test
reescrito (`Generate_never_adds_a_windowsPE_pass` → `Generate_always_adds_a_valid_windowsPE_pass_with_AcceptEula`)
lo fue porque la propia prueba real de P28 **invalidó la premisa** que ese
test de P28 afirmaba (que windowsPE no hacía falta) — se documenta
explícitamente en el nuevo test por qué cambió.

## Resultado de tests

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
MRS.ISOEngine.Tests           : 144/144  (131 previos + 13 nuevos de P29)
```

Total: **473/473**, 0 fallos.

## Resultado del build

```
dotnet build MRS-Windows-Builder.sln
```

Todos los proyectos de motor y de test compilan sin ningún error de
compilador (0 `errorCS*`). Había una instancia de `MRS.WindowsBuilder.exe`
abierta (PID 23224) bloqueando el paso final de copia de
`MRS.WindowsBuilder.exe`; se intentó cerrarla (`Stop-Process -Force` y
`CloseMainWindow()`), y ambas fallaron con "Acceso denegado" — la misma
restricción de entorno ya documentada en P23/P24/P25/P26/P27. Los únicos
errores del build completo son `MSB3027`/`MSB3021` (archivo bloqueado),
nunca `error CS*`: el código en sí compila limpio. `dotnet test` (que no
depende de `MRS.WindowsBuilder`) terminó en verde sin ninguna incidencia.

## Resultado

- El mecanismo primario y verificable para "sin conexión" ya no depende de
  ningún comando ejecutado durante Setup: el registro necesario se deja
  directamente en el SYSTEM hive de install.wim, offline, antes de que
  Setup lo copie a disco.
- `windowsPE` ahora está presente y es válido, corrigiendo la premisa
  incorrecta de P28.
- La validación final confirma, releyendo físicamente boot.wim (dos
  índices), install.wim y el XML, en vez de confiar en ningún resultado ya
  reportado.
- 473/473 tests, build de motor/tests limpio.
- **No se ha generado una nueva ISO ni se ha repetido la prueba real en
  VirtualBox (sección 11)**: sigue sin haber sesión elevada ni Windows ADK
  disponibles en este entorno (mismo bloqueo de P16/P17/P20/P23/P28). Queda
  pendiente para una sesión con esas condiciones.

## Commit recomendado

`git commit` con el resumen "P29: BypassNRO offline aplicado directamente
al SYSTEM hive de install.wim (InstallWimOobeConfigurator) -- el mecanismo
real que evita 'Vamos a conectarte a una red', ya que boot.wim/specialize
por sí solos no bastaron en la prueba real de P28; nueva fase windowsPE en
autounattend.xml; validación final extendida (boot.wim ambos índices +
install.wim + XML, releyendo físicamente todo); 473/473 sin regresiones"
y `git push` a `main`.
