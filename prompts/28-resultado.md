# Resultado - P28: corrección real de bypass hardware + OOBE offline

## Causa encontrada

**1. Bypass de hardware sin efecto real.** `InstallationImageService.ApplyAsync`
aplicaba LabConfig **solo al índice 1** de `boot.wim`, con un comentario de
P16 que asumía "índice 1 = Windows Setup / WinPE (índice 2 es Recuperación;
no se toca)". La auditoría real sobre un ISO generado por MRS lo desmiente:

```
Index 1 = Microsoft Windows PE (x64)
Index 2 = Microsoft Windows Setup (x64)
```

Índice 2 es el entorno donde Windows Setup ejecuta de verdad la
comprobación de TPM/Secure Boot/CPU/RAM — aplicar LabConfig solo al índice
1 (WinPE) lo dejaba sin ningún efecto sobre esa comprobación real, aunque
los tests de P16/P25/P26/P27 (que nunca distinguían índices) pasaran.

**2. "Vamos a conectarte a una red" seguía apareciendo.** El autounattend
generado ya ocultaba las pantallas de OOBE (`HideOnlineAccountScreens`,
`HideWirelessSetupInOOBE`), pero esas opciones solo ocultan las pantallas
de *cuenta*, no la pantalla previa de *red* — esa depende de
`HKLM\SYSTEM\Setup\OOBE\BypassNRO=1` en el **sistema ya instalado** (el
mismo valor que deja `OOBE\BYPASSNRO.cmd` al ejecutarse manualmente). Esa
clave no existía en ningún sitio del ISO generado: ni en boot.wim (donde
`LabConfigApplier.ApplyOfflineOobeBypassAsync` ya existía desde P16 pero
nunca se invocaba) ni en el autounattend (que solo tenía el paso
`oobeSystem`, nunca `specialize`, que es el paso que se ejecuta ya sobre el
sistema instalado, antes de que arranque OOBE).

## Índices modificados

| boot.wim | LabConfig (TPM/SecureBoot/CPU/RAM) | BypassNRO |
|---|---|---|
| Índice 1 (WinPE) | Aplicado y verificado (ya lo hacía P16) | No aplica |
| Índice 2 (Windows Setup) | **Nuevo en P28**: aplicado y verificado | **Nuevo en P28**: aplicado y verificado, solo si `AllowOfflineOobe` |

## Archivos modificados

- `src/MRS.ISOEngine/InstallationImageService.cs` — aplica LabConfig a
  ambos índices; nueva sobrecarga de constructor + `ValidateFinalAsync`.
- `src/MRS.ISOEngine/IInstallationImageService.cs` — nuevo método
  `ValidateFinalAsync` en la interfaz.
- `src/MRS.ISOEngine/BootWim/BootWimModifier.cs` /
  `IBootWimModifier.cs` — nuevo parámetro opcional `applyOfflineOobeBypass`
  (aplica y verifica BypassNRO dentro de la misma sesión de hive).
- `src/MRS.ISOEngine/Configuration/LabConfigApplier.cs` — se activa la
  invocación real de `ApplyOfflineOobeBypassAsync` (ya existía, "pendiente
  de confirmación" desde P16); nuevo `VerifyOfflineOobeBypassAsync`.
- `src/MRS.ISOEngine/Autounattend/AutounattendGenerator.cs` — nuevo paso
  `specialize` con el comando de BypassNRO, solo si `allowOfflineOobe`.
- `src/MRS.ISOEngine/Pipeline/IsoGenerationPipeline.cs` — la fase
  "Validación final" invoca `ValidateFinalAsync` además de
  `GenerationWorkspaceValidator`.
- `src/MRS.WindowsBuilder/MainWindow.xaml.cs` — pasa `dismRunner`/
  `registryEditor` (ya existentes) a la nueva sobrecarga de
  `InstallationImageService`.
- Tests: `InstallationImageServiceTests.cs`, `LabConfigApplierTests.cs`,
  `AutounattendGeneratorTests.cs`, `IsoGenerationPipelineTests.cs`,
  `Fakes/FakeInstallationImageService.cs`,
  `Fakes/FakeOfflineRegistryEditor.cs`.

No se ha tocado `RemovalEngine`, `MRS.ImageEngine` (inventario),
`MRS.ComponentCatalog`, `MRS.ProfileEngine` ni ningún componente de
eliminación.

## Cambios realizados

### 1. LabConfig en ambos índices

`InstallationImageService.ApplyAsync` reemplaza la única llamada a
`ApplyLabConfigAsync(index: 1, ...)` por un bucle sobre
`{1, 2}`, abortando en el primer índice que falle (nunca deja boot.wim
modificado a medias entre índices):

```csharp
foreach (var index in BootWimIndicesRequiringLabConfig) // { 1, 2 }
{
    var applyOfflineOobeBypass = index == WindowsSetupBootWimIndex && options.AllowOfflineOobe;
    var indexModification = await _bootWimModifier.ApplyLabConfigAsync(
        workspace.BootWimPath, index, options, workspace.MountPath,
        cancellationToken, progress, applyOfflineOobeBypass);
    ...
    if (!indexModification.Success) { allIndicesSucceeded = false; break; }
}
```

Cada índice sigue el mismo ciclo transaccional de siempre (Mount RW → cargar
hive → aplicar → **verificar leyendo de vuelta** → descargar hive →
Commit/Discard) — nunca se acepta que DISM terminara sin error como prueba
de éxito, sección ya cubierta desde P16/P25 y ahora aplicada dos veces.

### 2. BypassNRO en boot.wim índice 2

`LabConfigApplier.ApplyOfflineOobeBypassAsync` (ya implementado en P16, sin
invocar nunca) se activa ahora dentro de la misma sesión de hive del índice
2, justo después de aplicar LabConfig y antes de descargar el hive — mismo
patrón transaccional, sin montar/cargar el hive una segunda vez. Se añade
`VerifyOfflineOobeBypassAsync` (lee el valor de vuelta, igual que
`VerifyAsync` para LabConfig).

### 3. `specialize` en autounattend.xml (el fix real de "Vamos a conectarte a una red")

`AutounattendGenerator.Generate`, cuando `allowOfflineOobe == true`, añade
un paso `specialize` con el componente `Microsoft-Windows-Deployment` y un
`RunSynchronousCommand` automático (ejecutado por Setup, **nunca por el
usuario**):

```xml
<settings pass="specialize">
  <component name="Microsoft-Windows-Deployment" processorArchitecture="amd64"
             publicKeyToken="31bf3856ad364e35" language="neutral" versionScope="nonSxS">
    <RunSynchronous>
      <RunSynchronousCommand wcm:action="add">
        <Order>1</Order>
        <Path>reg add HKLM\SYSTEM\Setup\OOBE /v BypassNRO /t REG_DWORD /d 1 /f</Path>
        <Description>Bypass network requirement for OOBE</Description>
      </RunSynchronousCommand>
    </RunSynchronous>
  </component>
</settings>
```

**Por qué `specialize` y no `windowsPE`**: `windowsPE` se ejecuta *antes* de
que exista ningún sistema instalado (solo hay disco/partición); OOBE ocurre
*después* de aplicar `install.wim`. `specialize` es el primer paso que ya
corre sobre el sistema instalado, antes de que arranque OOBE — es
literalmente el punto documentado del esquema de unattend para dejar
registro puesto de antemano, sin depender de ningún comando manual durante
OOBE (Shift+F10, `OOBE\BYPASSNRO`, `ms-cxh:localonly`): el comando de
arriba es el **mismo** que ejecuta ese atajo manual, solo que automatizado
por Setup mismo. No se tocó `windowsPE` (confirmado explícitamente con un
test — sección Tests).

### 4. Cuenta local — sin cambios

`AllowLocalAccount`/`Name=Usuario`/`Group=Administrators` siguen viniendo
de `AutounattendConfiguration` tal como ya estaban (P16); no se introdujo
ninguna contraseña fija.

### 5. Validación final del ISO (nueva)

`IInstallationImageService.ValidateFinalAsync` (implementada en
`InstallationImageService`, invocada por `IsoGenerationPipeline` en la fase
"Validación final", después de `GenerationWorkspaceValidator`): vuelve a
**montar boot.wim de solo lectura** para cada índice (1 y 2, un mount/hive
load/unload/discard independiente de los ya comprometidos por
`ApplyAsync`), relee LabConfig/BypassNRO del hive, y valida el contenido
real de `autounattend.xml` (XML bien formado, `LocalAccount` presente,
`HideOnlineAccountScreens=true`, comando de BypassNRO presente si
`AllowOfflineOobe`). Registra exactamente:

```
[OK] boot.wim Index 1 LabConfig
[OK] boot.wim Index 2 LabConfig
[OK] autounattend.xml found
[OK] autounattend.xml valid
[OK] local account configured
[OK] offline OOBE configured
```

Es una re-verificación **independiente** de la que ya hace `ApplyAsync`
(nunca confía en su resultado reportado): usa una sesión de montaje/hive
completamente nueva. Si `InstallationImageService` se construye con el
constructor de 3 argumentos (compatibilidad hacia atrás, sin
`IDismRunner`/`IOfflineRegistryEditor`), `ValidateFinalAsync` devuelve
válido sin comprobar nada — documentado explícitamente en el código y en un
test, no un comportamiento silencioso.

## Estructura final de `autounattend.xml`

```xml
<?xml version="1.0" encoding="utf-8"?>
<unattend xmlns:wcm="http://schemas.microsoft.com/WMIConfig/2002/State" xmlns="urn:schemas-microsoft-com:unattend">
  <settings pass="specialize">        <!-- solo si AllowOfflineOobe -->
    <component name="Microsoft-Windows-Deployment" ...>
      <RunSynchronous>
        <RunSynchronousCommand wcm:action="add">
          <Order>1</Order>
          <Path>reg add HKLM\SYSTEM\Setup\OOBE /v BypassNRO /t REG_DWORD /d 1 /f</Path>
          <Description>Bypass network requirement for OOBE</Description>
        </RunSynchronousCommand>
      </RunSynchronous>
    </component>
  </settings>
  <settings pass="oobeSystem">        <!-- sin cambios respecto a P16 -->
    <component name="Microsoft-Windows-Shell-Setup" ...>
      <ComputerName>MRS-PC</ComputerName>
      <OOBE>
        <HideEULAPage>true</HideEULAPage>
        <HideOEMRegistrationScreen>true</HideOEMRegistrationScreen>
        <HideOnlineAccountScreens>true</HideOnlineAccountScreens>
        <HideWirelessSetupInOOBE>true</HideWirelessSetupInOOBE>
        <ProtectYourPC>3</ProtectYourPC>
        <NetworkLocation>Home</NetworkLocation>   <!-- solo si AllowOfflineOobe -->
      </OOBE>
      <UserAccounts>
        <LocalAccounts>
          <LocalAccount wcm:action="add">
            <Name>Usuario</Name>
            <Group>Administrators</Group>
            <DisplayName>Usuario</DisplayName>
          </LocalAccount>
        </LocalAccounts>
      </UserAccounts>
    </component>
  </settings>
</unattend>
```

`windowsPE` no aparece en ningún caso — se determinó explícitamente que no
hace falta ninguna configuración ahí para cuenta local/OOBE offline.

## Validaciones

Todas las pedidas en la sección 5 del prompt están implementadas en
`ValidateFinalAsync` (ver arriba) y verificadas con tests dedicados (ver
abajo): LabConfig índice 1, LabConfig índice 2, autounattend.xml existe,
XML válido, cuenta local configurada, OOBE offline configurado.

## Tests

**`LabConfigApplierTests.cs`** (3 nuevos): `ApplyOfflineOobeBypassAsync`
pone `BypassNRO=1`; `VerifyOfflineOobeBypassAsync` confirma tras aplicar y
falla si nunca se aplicó (nunca se acepta el éxito de reg.exe como prueba
suficiente).

**`AutounattendGeneratorTests.cs`** (4 nuevos): `specialize` con el comando
de BypassNRO exacto cuando `allowOfflineOobe=true`; **ausencia** total de
`specialize`/`BypassNRO` cuando es `false`; **ausencia** de `windowsPE` en
ambos casos (documentada explícitamente, no solo asumida); `specialize`
aparece antes que `oobeSystem` en el XML.

**`InstallationImageServiceTests.cs`** (7 nuevos, sobre `BootWimModifier`
real con `FakeDismRunner`/`FakeOfflineRegistryEditor`, nunca DISM real):
LabConfig se monta y aplica en índice 1 **y** índice 2; BypassNRO se aplica
solo cuando `AllowOfflineOobe=true` y solo una vez (índice 2); nunca se
aplica si `AllowOfflineOobe=false`; `ValidateFinalAsync` tiene éxito tras un
`ApplyAsync` correcto **volviendo a montar realmente** (dos mounts de solo
lectura adicionales, uno por índice — no solo reutiliza el resultado ya
reportado); `ValidateFinalAsync` detecta una discrepancia releyendo (un
valor que debería estar ausente pero sigue presente); el constructor de 3
argumentos hace que `ValidateFinalAsync` sea un no-op documentado.

**`IsoGenerationPipelineTests.cs`** (2 nuevos): el pipeline invoca
`ValidateFinalAsync` en un run exitoso; un fallo de esa validación aborta
**antes** de invocar `oscdimg`.

Se corrigió además `Fakes/FakeOfflineRegistryEditor.cs`: indexaba sus
valores por `hiveKeyName` (un alias aleatorio nuevo por cada sesión de
montaje), lo que hacía que una relectura en una sesión posterior (como
`ValidateFinalAsync`) nunca encontrara nada aunque DISM real sí lo haría
(el alias es efímero; lo que persiste es el archivo de hive). Se corrigió
para indexar por `subKeyPath\valueName`, reflejando el comportamiento real.

Ningún fixture "fake boot.wim"/"fake install.wim" se trata como un WIM
real: toda la validación de contenido se hace contra `IDismRunner`/
`IOfflineRegistryEditor` simulados o contra el propio XML generado, nunca
interpretando el contenido de archivo como un WIM auténtico.

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
MRS.ISOEngine.Tests           : 130/130  (116 previos + 14 nuevos de P28)
```

Total: **459/459**, 0 fallos. Ningún test existente se rompió ni se
modificó para ocultar un fallo (el único ajuste a un fixture, en
`FakeOfflineRegistryEditor`, corrige un modelo de persistencia
irreal del fake, no relaja ninguna aserción).

## Resultado del build

Se comprobó antes de compilar que no había ninguna instancia de
`MRS.WindowsBuilder.exe` abierta.

```
dotnet build MRS-Windows-Builder.sln
Compilación correcta.
    0 Advertencia(s)
    0 Errores
```

Compilación completa y limpia de los 17 proyectos, incluido
`MRS.WindowsBuilder.exe`.

## Resultado

- LabConfig ahora se aplica y se **verifica dos veces** (índices 1 y 2 de
  boot.wim), corrigiendo la causa confirmada de que el bypass de hardware
  no surtía efecto en Windows Setup.
- El bypass de red offline (BypassNRO) ahora se aplica automáticamente por
  dos vías complementarias: en boot.wim índice 2 (offline, antes de que
  Setup arranque) y en el sistema instalado vía el paso `specialize` del
  autounattend (automático, sin ningún comando manual durante OOBE).
- Se añadió una validación final independiente que vuelve a comprobar todo
  esto tras el commit, con los mensajes `[OK] ...` exactos pedidos.
- 459/459 tests, build completo sin errores.
- **No se ha ejecutado la prueba real en VirtualBox de la sección 8** (no
  hay sesión elevada ni Windows ADK disponibles en este entorno — mismo
  bloqueo ya documentado en P16/P17/P20/P23); queda pendiente para una
  sesión con esas condiciones, siguiendo exactamente los pasos de esa
  sección del prompt.

## Commit recomendado

`git commit` con el resumen "P28: LabConfig se aplica y verifica en boot.wim
índice 1 Y 2 (auditoría real: índice 2 es Windows Setup, no Recuperación);
BypassNRO se activa en boot.wim índice 2 y en un paso specialize automático
del autounattend (fix real de 'Vamos a conectarte a una red', sin depender
de ningún comando manual); nueva validación final independiente
(ValidateFinalAsync) con los mensajes [OK] pedidos; 459/459 sin
regresiones, build completo limpio" y `git push` a `main`.
