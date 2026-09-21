# Resultado - P26: corrige el orden real de preparación de boot.wim (P25 incompleto)

## Causa exacta encontrada

P25 quitaba el atributo ReadOnly de la copia de trabajo de `boot.wim`, pero
**solo en la rama de `BootWimProvisioner.EnsureBootWimCopyAsync` que copia
el archivo ella misma**. En el flujo real, esa rama casi nunca se ejecuta:
`IsoGenerationPipeline` invoca primero `IsoTreeCopier.CopyAsync`, que copia
el **árbol completo** de la ISO montada (incluido `sources\boot.wim`) al
workspace, y **solo después** llama a `InstallationImageService.ApplyAsync`
→ `BootWimProvisioner.EnsureBootWimCopyAsync`. Para cuando este último se
ejecuta, `sources\boot.wim` **ya existe** en el workspace (copiado por
`IsoTreeCopier`, que también usa `File.Copy` y por tanto también hereda
ReadOnly del archivo leído desde la ISO montada en solo lectura). El método
entra entonces por su rama idempotente:

```csharp
if (File.Exists(destinationBootWimPath))
{
    _logger.Info("boot.wim ya existe en el workspace; no se vuelve a copiar (idempotente).");
    return false;   // <- P25: nunca llamaba a EnsureWritable aquí
}
```

Esta rama devolvía sin preparar nada, así que el archivo llegaba a
`BootWimModifier.ApplyLabConfigAsync` todavía ReadOnly, y la validación de
P25 (correcta en sí misma) lo rechazaba con el mensaje exacto reportado:
`"boot.wim en el workspace todavía tiene el atributo ReadOnly ... No se
intentará montar."` — en la etapa "Preparando boot.wim" (20%, fase 5/10),
justo después de los mensajes `"Comprobando copia de boot.wim en el
workspace..."` / `"OK boot.wim listo en el workspace..."` que emite
`InstallationImageService.ApplyAsync` alrededor de esa llamada.

Los 440 tests de P25 pasaban porque ninguno de ellos pre-poblaba el destino
antes de invocar `EnsureBootWimCopyAsync`/`ApplyAsync` — todos ejercitaban
únicamente la rama de "copio yo mismo", nunca la rama idempotente que el
pipeline real usa casi siempre.

## Flujo anterior (P25, incompleto)

```
IsoTreeCopier copia sources\boot.wim (hereda ReadOnly)
→ BootWimProvisioner.EnsureBootWimCopyAsync: File.Exists == true
  → return SIN preparar (bug)
→ BootWimModifier.ApplyLabConfigAsync
  → ValidateBeforeMountAsync: detecta ReadOnly → ABORTA
```

## Flujo corregido

```
IsoTreeCopier copia sources\boot.wim (hereda ReadOnly, sin cambios)
→ BootWimProvisioner.EnsureBootWimCopyAsync
  → exista ya o se copie aquí mismo, SIEMPRE llama a
    PrepareWorkingCopyForMount(destino) antes de devolver:
      "Preparando boot.wim de trabajo..."
      si ReadOnly: "Eliminando atributo ReadOnly de boot.wim..." → lo quita
                   → "OK boot.wim preparado para escritura."
      si no:       "OK boot.wim ya era writable."
→ BootWimModifier.ApplyLabConfigAsync
  → ValidateBeforeMountAsync (P25, sin cambios de lógica):
      existe → tamaño razonable → sin ReadOnly (ya lo quitó el paso anterior)
      → "Validando boot.wim..." → Get-WimInfo → "OK boot.wim válido y
        preparado para montaje."
  → Mount-Wim ("DISM: Mount-Wim de boot.wim iniciado")
```

## Archivos modificados

- `src/MRS.ISOEngine/BootWim/BootWimProvisioner.cs` — la corrección real:
  `EnsureWritable` (P25) se convierte en `PrepareWorkingCopyForMount`,
  llamada desde **ambas** ramas (idempotente y recién copiada), nunca solo
  una.
- `src/MRS.ISOEngine/BootWim/BootWimModifier.cs` — solo ajuste de texto de
  log (`"Validando boot.wim..."` / `"OK boot.wim válido y preparado para
  montaje."`) y de un comentario que ya no era exacto; la lógica de
  `ValidateBeforeMountAsync` (P25) no cambia.
- `tests/MRS.ISOEngine.Tests/BootWimProvisionerTests.cs` — nuevo test que
  reproduce el bug exacto (destino pre-existente y ReadOnly).
- `tests/MRS.ISOEngine.Tests/InstallationImageServiceTests.cs` — nuevo test
  de extremo a extremo que simula lo que hace `IsoTreeCopier` antes de
  llamar a `ApplyAsync` (el escenario real de producción).

No se ha tocado `IsoTreeCopier`, `IsoGenerationPipeline`, `install.wim`,
`RemovalEngine`, `PostInstall`, `oscdimg`, `InstallationOptions`, perfiles
ni catálogo.

## Por qué se prefirió esta solución (no "otro attrib -R en cualquier sitio")

Se localizó el único punto que decide si `boot.wim` está "listo para
montar" — `BootWimProvisioner`, el único componente responsable de
provisionar esa copia de trabajo — y se corrigió ahí, de modo que **ningún
camino de ejecución pueda devolver sin haber preparado el archivo**, en vez
de añadir una comprobación puntual en otro sitio del pipeline. Se mantiene
la API de .NET (`File.GetAttributes`/`File.SetAttributes`) en vez de
invocar `attrib.exe`, igual que P25 — no había ninguna razón arquitectónica
para cambiarlo.

## Tests

**Nuevos (2)**:
- `BootWimProvisionerTests.ReadOnly_is_removed_even_when_the_destination_already_exists_idempotent_path`
  — reproduce el bug exacto a nivel unitario: un destino pre-existente y
  ReadOnly (como lo deja `IsoTreeCopier`) queda escribible tras
  `EnsureBootWimCopyAsync`, sin volver a copiarse.
- `InstallationImageServiceTests.A_boot_wim_already_placed_in_the_workspace_by_IsoTreeCopier_with_ReadOnly_still_mounts_successfully`
  — reproduce el bug de extremo a extremo: copia manualmente `boot.wim` al
  destino con ReadOnly (igual que haría `IsoTreeCopier` antes de que
  `ApplyAsync` se invoque) y confirma que `Mount-Wim` **sí** se ejecuta y
  el resultado es un éxito completo. **Este test habría fallado con el
  código de P25** (habría lanzado `IsoEngineException` por ReadOnly) y es
  el que demuestra que la validación de ReadOnly ya no ocurre antes de la
  preparación en el camino real.

Los fixtures "fake boot.wim" existentes no se convirtieron en WIM reales ni
se relajó ninguna protección: los nuevos tests usan bytes de relleno
(`new byte[8192]`) o el mismo texto repetido ya usado en P25 para superar el
umbral de tamaño, y siguen validando la respuesta de `IDismRunner`
simulado, nunca contenido real de WIM.

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
MRS.ISOEngine.Tests           : 113/113  (111 previos + 2 nuevos de P26)
```

Total: **442/442**, 0 fallos. Ningún test existente se modificó ni se
rompió.

## Resultado del build

Se comprobó antes de compilar que no había ninguna instancia de
`MRS.WindowsBuilder.exe` abierta (`Get-Process MRS.WindowsBuilder` sin
resultados) — no hizo falta cerrar nada.

```
dotnet build MRS-Windows-Builder.sln
Compilación correcta.
    0 Advertencia(s)
    0 Errores
```

Compilación completa y limpia de los 17 proyectos, incluido
`MRS.WindowsBuilder.exe`.

## Commit recomendado

`git commit` con el resumen "P26: BootWimProvisioner preparaba boot.wim
(quitaba ReadOnly) solo en la rama que copia el archivo, pero
IsoTreeCopier ya lo deja en el workspace antes -- el flujo real entraba
siempre por la rama idempotente, que nunca preparaba nada, y la validación
de P25 abortaba por ReadOnly en producción pese a que los 440 tests
pasaban. Se prepara ahora en ambas ramas; 442/442 sin regresiones, build
completo limpio" y `git push` a `main`.
