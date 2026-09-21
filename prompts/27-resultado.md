# Resultado - P27: install.wim — "Access to the path ... is denied." tras Export-Image

## Causa exacta

No era una confusión de rutas: el pipeline ya usa correctamente
`workspaces\<guid>\source\install.wim` (la imagen de trabajo de
`WorkingImageFactory`/`RemovalEngine`, P07) para montar/modificar, y
`iso-workspaces\<guid>\sources\install.wim` (el workspace de generación de
P19) como destino final. La causa es el **mismo patrón que P25/P26, aplicado
a un archivo distinto**:

1. `IsoGenerationPipeline` ejecuta primero `IsoTreeCopier.CopyAsync`
   (línea ~107), que copia el árbol **completo** de la ISO montada en solo
   lectura al workspace de generación — incluido `sources\install.wim`, que
   hereda el atributo **ReadOnly** (igual que `boot.wim` en P25).
2. Más adelante (línea ~174, antes de esta corrección), el pipeline sustituye
   ese archivo por la imagen de trabajo ya terminada con
   `File.Copy(workingImage.WorkingWimPath, workspace.InstallWimPath,
   overwrite: true)`.
3. `File.Copy` con `overwrite: true` **no quita el atributo ReadOnly del
   destino existente**: si el destino ya tiene ReadOnly, la operación falla
   con `UnauthorizedAccessException` — exactamente el mensaje real
   reportado: `"Access to the path
   '...\iso-workspaces\...\sources\install.wim' is denied."`

Los datos de la prueba manual lo confirman: `Export-Image` terminó
correctamente (la imagen de trabajo en `workspaces\...\source\install.wim`
existe, 7 347 592 424 bytes, **Attributes: Archive**, sin ReadOnly) — el
fallo ocurre **después**, exactamente en el paso de sustitución final sobre
`iso-workspaces\...\sources\install.wim` (7 955 102 491 bytes,
**ReadOnly, Archive**, heredado de la ISO original de solo lectura).

## Flujo (ya era arquitectónicamente correcto; solo faltaba esto)

```
ISO original (solo lectura)
  sources\install.wim (ReadOnly)
        │
        │ IsoTreeCopier.CopyAsync (copia el árbol completo)
        ▼
iso-workspaces\<guid>\sources\install.wim  ← hereda ReadOnly (bug: sin preparar)
        │
        │ (en paralelo) _isoMounter.MountAsync + WorkingImageFactory.CreateAsync
        │               (Export-Image, índice elegido)
        ▼
workspaces\<guid>\source\install.wim  ← imagen de trabajo, SIEMPRE writable
        │
        │ RemovalEngine.ExecuteAsync (0 o más eliminaciones)
        ▼
imagen de trabajo terminada (Success == true)
        │
        │ File.Copy(..., overwrite: true)  ← fallaba aquí: destino ReadOnly
        ▼
iso-workspaces\<guid>\sources\install.wim  (sustitución final)
```

La sustitución solo ocurre después de confirmar `removalResult.Success`
(ya era así desde P19); no había que rediseñar nada, solo preparar el
destino antes de esa única línea.

## Archivos modificados

- `src/MRS.ISOEngine/Pipeline/IsoGenerationPipeline.cs` — la corrección
  real.
- `tests/MRS.ISOEngine.Tests/IsoGenerationPipelineTests.cs` — tests nuevos.

No se ha tocado `InstallationImageService`, `WorkingImageFactory`,
`RemovalEngine`, `boot.wim`, `PostInstall`, `oscdimg`,
`InstallationOptions`, perfiles ni catálogo.

## Solución aplicada

En `IsoGenerationPipeline.GenerateAsync`, justo antes de la sustitución
final (y solo ahí — nunca antes de que `removalResult.Success` sea
`true`):

```csharp
_logger.Info($"[INSTALL] Source install.wim (ISO original, montada de solo lectura): '{sourceInstallWimPath}'");
_logger.Info($"[INSTALL] Working install.wim (WorkingImageFactory + RemovalEngine): '{workingImage.WorkingWimPath}'");
_logger.Info($"[INSTALL] Final ISO install.wim (workspace de generación): '{workspace.InstallWimPath}'");

EnsureInstallWimDestinationWritable(workspace.InstallWimPath);
File.Copy(workingImage.WorkingWimPath, workspace.InstallWimPath, overwrite: true);
```

```csharp
private void EnsureInstallWimDestinationWritable(string path)
{
    if (!File.Exists(path))
        return;

    var attributes = File.GetAttributes(path);
    if ((attributes & FileAttributes.ReadOnly) == 0)
        return;

    _logger.Info("Eliminando atributo ReadOnly de install.wim en el workspace de generación...");
    File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
}
```

Mismo idioma ya establecido en P25/P26 (API de .NET, `FileAttributes`, sin
`attrib.exe`), aplicado en el punto exacto donde hacía falta: la
**sustitución final**, no un permiso genérico. Nunca toca:
- la ISO montada (`sourceInstallWimPath`, solo se lee y se registra en el
  log);
- la imagen de trabajo de `WorkingImageFactory`
  (`workingImage.WorkingWimPath`, solo se lee como origen del `File.Copy`).

Solo actúa sobre `workspace.InstallWimPath`, la copia propia del workspace
de generación — no el original del ISO workspace en el sentido de "fuente
inmutable de la ISO", sino el destino donde ese workspace va a alojar la
imagen final antes de invocar `oscdimg`.

## Tests añadidos

En `tests/MRS.ISOEngine.Tests/IsoGenerationPipelineTests.cs` (3 nuevos), la
infraestructura de fakes ya existente permite reproducir el escenario real
con fidelidad: `_treeCopier.OnCopy` ejecuta la misma
`DirectoryCopyHelper.CopyAll` (con `File.Copy` real) que usaría el
`IsoTreeCopier` real, así que marcar el `install.wim` "montado" como
ReadOnly reproduce exactamente la herencia de atributos del bug real, sin
necesitar DISM ni una ISO real.

- `A_ReadOnly_install_wim_inherited_from_the_ISO_tree_copy_with_zero_eliminations_still_completes`
  — **caso con 0 eliminaciones** (`RemovalPlan` vacío): confirma que la
  ejecución completa sin lanzar `UnauthorizedAccessException` y que el
  `install.wim` final en el workspace de generación es exactamente el de
  la imagen de trabajo.
- `A_ReadOnly_install_wim_inherited_from_the_ISO_tree_copy_with_eliminations_still_completes`
  — **caso con eliminaciones** (`RemovalPlan` con un componente
  seleccionado y permitido): mismo escenario, confirma que el bug es
  independiente de si hubo acciones de `RemovalEngine` o no.
- `The_mounted_ISO_install_wim_is_never_written_to_during_the_final_substitution`
  — confirma que el `install.wim` "original" (dentro de la ISO montada
  simulada) conserva su contenido **y** su atributo ReadOnly intactos tras
  la ejecución completa: la preparación nunca toca la fuente.

Ninguno de estos tests trata "fake install.wim" como un WIM real: solo se
manipulan atributos de archivo y contenido de texto de prueba, igual que en
P25/P26; la validez del WIM en sí sigue probada por separado en
`BootWimModifierTests`/`InstallationExecutionValidatorTests` con
`IDismRunner` simulado.

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
MRS.ISOEngine.Tests           : 116/116  (113 previos + 3 nuevos de P27)
```

Total: **445/445**, 0 fallos. Ningún test existente se modificó.

## Resultado del build

Se comprobó antes de compilar que no había ninguna instancia de
`MRS.WindowsBuilder.exe` abierta (`Get-Process MRS.WindowsBuilder` sin
resultados).

```
dotnet build MRS-Windows-Builder.sln
Compilación correcta.
    0 Advertencia(s)
    0 Errores
```

Compilación completa y limpia de los 17 proyectos, incluido
`MRS.WindowsBuilder.exe`.

## Commit recomendado

`git commit` con el resumen "P27: install.wim en el workspace de generación
heredaba ReadOnly de IsoTreeCopier (mismo patrón que boot.wim en P25/P26),
y File.Copy(overwrite:true) no lo quita al sustituirlo por la imagen de
trabajo ya terminada -- 'Access to the path ... is denied.' tras un
Export-Image por lo demás correcto. Se prepara el destino justo antes de la
sustitución final, nunca antes ni sobre la ISO/imagen de trabajo original;
logging Source/Working/Final install.wim añadido. 445/445 sin
regresiones, build completo limpio" y `git push` a `main`.
