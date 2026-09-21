# Resultado - P25: fallo real de Mount-Wim en boot.wim (ReadOnly)

## Causa encontrada

`BootWimProvisioner.EnsureBootWimCopyAsync` copia `sources\boot.wim` desde
la ISO montada en solo lectura con `File.Copy(sourceBootWim,
destinationBootWimPath, overwrite: false)`. En Windows, `File.Copy`
conserva los atributos del archivo origen; como el archivo se lee desde una
ISO montada con `Mount-DiskImage` (solo lectura), la copia resultante en el
workspace hereda el atributo **ReadOnly**. `DISM /Mount-Wim` con
`readOnly:false` (necesario para poder aplicar LabConfig) no puede abrir en
escritura un archivo marcado ReadOnly, y falla exactamente con el error
observado en la generación real:

```
Fail to flush file buffers
HRESULT=0x80070006
"WIM open failed with access denied."
hr=0xc1510111
```

El diagnóstico de campo (`attrib -R` manual seguido de un `Mount-Wim`
manual exitoso) confirma esta causa de forma directa: nada en el propio
`boot.wim` estaba corrupto, únicamente el atributo del archivo.

## Archivos modificados

- `src/MRS.ISOEngine/BootWim/BootWimProvisioner.cs`
- `src/MRS.ISOEngine/BootWim/BootWimModifier.cs`
- `tests/MRS.ISOEngine.Tests/BootWimProvisionerTests.cs`
- `tests/MRS.ISOEngine.Tests/BootWimModifierTests.cs`
- `tests/MRS.ISOEngine.Tests/InstallationImageServiceTests.cs`
- `tests/MRS.ISOEngine.Tests/Fakes/FakeDismRunner.cs`

Ningún otro archivo del pipeline (`install.wim`, `PostInstall`, `oscdimg`,
`RemovalEngine`, `InstallationOptions`) se ha tocado.

## Solución aplicada

**1. `BootWimProvisioner.EnsureBootWimCopyAsync`** (el punto correcto: aquí
es donde se crea la copia de trabajo, justo después de `File.Copy`):

```csharp
File.Copy(sourceBootWim, destinationBootWimPath, overwrite: false);
EnsureWritable(destinationBootWimPath);
```

```csharp
private void EnsureWritable(string path)
{
    var attributes = File.GetAttributes(path);
    if ((attributes & FileAttributes.ReadOnly) == 0)
        return;

    _logger.Info("Eliminando atributo ReadOnly de boot.wim...");
    File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
}
```

Idempotente por construcción (comprueba el atributo antes de tocarlo; si ya
es escribible, no hace nada) y **nunca** toca el archivo origen: solo
recibe la ruta de la copia ya creada en el workspace, nunca la ruta dentro
de la ISO montada. Se añadió el logging pedido:
`"Preparando boot.wim de trabajo..."` (antes de copiar),
`"Eliminando atributo ReadOnly de boot.wim..."` (solo si hacía falta),
`"boot.wim preparado para montaje."` (al terminar).

**2. `BootWimModifier.ApplyLabConfigAsync`**: se añade `ValidateBeforeMountAsync`
como defensa en profundidad, justo antes de `Mount-Wim`, con comprobaciones
en orden de coste creciente (E/S local primero, DISM al final y solo si lo
anterior ya pasó):

1. El archivo existe (ya existía esta comprobación; se mantiene igual).
2. Su tamaño no es sospechosamente pequeño (`< 4096` bytes — un umbral de
   cordura para descartar un archivo vacío/truncado, no un tamaño real de
   boot.wim, que ronda cientos de MB).
3. No tiene el atributo ReadOnly (no debería ocurrir nunca, ya que el punto
   1 lo evita; si ocurriera por algún otro origen del workspace, falla aquí
   con un mensaje claro en vez de dejar que DISM lo intente y falle con
   "access denied").
4. Es un WIM válido, reutilizando `IDismRunner.GetWimInfoAsync(imagePath,
   cancellationToken)` (`/Get-WimInfo`, sin montar) — la misma abstracción
   ya usada en otros motores del proyecto (p. ej. el preflight de
   `RemovalEngine`), sin duplicar ninguna comprobación DISM propia.

Si cualquiera falla, se lanza `IsoEngineException` **antes** de llamar a
`_dism.MountWimAsync`, así que nunca se intenta montar un archivo que ya se
sabe problemático.

## Por qué esto cumple los requisitos del prompt

- El boot.wim **original** (dentro de la ISO montada) nunca se toca:
  `EnsureWritable` solo actúa sobre `destinationBootWimPath` (la copia ya
  creada en el workspace), nunca sobre la ruta de origen.
- La operación es idempotente (requisito 4): comprobada explícitamente con
  tests (`ReadOnly` → se quita; ya escribible → no falla).
- No depende de que el usuario ejecute `attrib -R` manualmente (requisito
  5): se hace automáticamente, siempre, como parte de
  `EnsureBootWimCopyAsync`.
- No se duplica ninguna operación DISM costosa (requisito 7): se reutiliza
  `GetWimInfoAsync`, ya existente en `IDismRunner`.
- El flujo Preparación → Mount-Wim → modificación → validación →
  Commit/Discard no cambia de forma (requisito 8): solo se añade una
  comprobación previa al mismo Mount-Wim de siempre.
- `install.wim`, `PostInstall`, `oscdimg`, `RemovalEngine`,
  `InstallationOptions` no se han tocado (requisitos 9-13).

## Tests

**Nuevos (5)**:
- `BootWimProvisionerTests.ReadOnly_attribute_is_removed_from_the_workspace_copy_but_never_from_the_source`
  — un origen marcado ReadOnly produce una copia de trabajo escribible,
  mientras el origen sigue ReadOnly (nunca se modifica).
- `BootWimProvisionerTests.A_writable_source_copies_without_error_ReadOnly_removal_is_idempotent`
  — un origen ya escribible no produce ningún error.
- `BootWimModifierTests.A_suspiciously_small_boot_wim_throws_before_mounting_anything`
  — un archivo de 10 bytes lanza `IsoEngineException` sin que `Mount-Wim`
  llegue a invocarse (`dism.Calls` vacío).
- `BootWimModifierTests.A_boot_wim_still_marked_ReadOnly_throws_before_mounting_anything`
  — defensa en profundidad: si el archivo llegara ReadOnly hasta aquí
  (no debería, dado el punto 1), falla con mensaje claro sin montar.
- `BootWimModifierTests.DISM_reporting_an_invalid_WIM_throws_before_mounting_anything`
  — `Get-WimInfo` con ExitCode≠0 aborta antes de `Mount-Wim` (`dism.Calls`
  contiene `"list:..."` pero nunca `"mount:..."`).

Ninguno de estos tests trata "fake boot.wim" como un WIM real: los archivos
de prueba son solo bytes de relleno o texto repetido para superar el umbral
de tamaño; la validación de "WIM válido" se prueba controlando la respuesta
de `IDismRunner` simulado (`FakeDismRunner.ListExitCode`), nunca ejecutando
DISM real ni interpretando contenido de archivo como un WIM auténtico.

**Fixtures existentes ajustadas** (sin cambiar ninguna aserción, solo para
que sigan pasando el nuevo umbral de tamaño mínimo):
- `BootWimModifierTests`: el `boot.wim` de prueba pasa de 14 bytes de texto
  a 8192 bytes.
- `InstallationImageServiceTests`: el `boot.wim` "original" (dentro de la
  ISO montada simulada) pasa de 22 a >4096 bytes, repitiendo el mismo texto
  (se sigue comparando con `File.ReadAllText` para confirmar que no se
  modifica).
- `FakeDismRunner` (en `MRS.ISOEngine.Tests.Fakes`): `GetWimInfoAsync(string,
  CancellationToken)` (sin índice) ahora registra su llamada en `Calls`
  (`"list:<ruta>"`) y admite un `ListExitCode` configurable — antes siempre
  devolvía éxito sin dejar rastro, así que no había forma de probar el
  camino de "WIM inválido".

## Tests ejecutados y resultado

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
MRS.ISOEngine.Tests           : 111/111  (106 previos + 5 nuevos de P25)
```

Total: **440/440**, 0 fallos. Ningún test existente se rompió (solo se
ajustó el tamaño de dos fixtures de `boot.wim`, sin tocar ninguna
aserción).

## Build ejecutado y resultado

```
dotnet build MRS-Windows-Builder.sln
Compilación correcta.
    0 Advertencia(s)
    0 Errores
```

Compilación completa de los 17 proyectos, incluido `MRS.WindowsBuilder.exe`
(a diferencia de P23/P24, en esta sesión no quedaba ninguna instancia de la
aplicación abierta bloqueando el directorio de salida, así que no hizo
falta cerrar nada manualmente).

## Incidencias

Ninguna. No se necesitó ningún cambio adicional fuera del alcance descrito.
No se ha generado ninguna ISO nueva en esta fase (explícitamente fuera de
alcance, según el prompt).

## Commit recomendado

`git commit` con el resumen "P25: boot.wim de trabajo conservaba ReadOnly
tras copiarlo desde la ISO montada, y DISM no podía montarlo en escritura
('WIM open failed with access denied.') -- BootWimProvisioner quita el
atributo automáticamente de la copia (nunca del original) de forma
idempotente, y BootWimModifier valida existencia/tamaño/ReadOnly/validez
DISM antes de Mount-Wim; 440/440 sin regresiones, build completo sin
errores" y `git push` a `main`.
