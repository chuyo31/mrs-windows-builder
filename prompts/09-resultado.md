# Resultado - P09: auditoría y corrección del ciclo de vida workspace/mount

## 1. Causa raíz

**No es un fallo de `/Unmount-Wim` del RemovalEngine.** El log real lo confirma
("Unmount complete", "Wimserv stopped") y el propio `RemovalEngine` nunca
borra su workspace (es el resultado a conservar). El origen está en un
**segundo** ciclo de montaje, ajeno al `RemovalEngine`, disparado por la
verificación posterior:

`MainWindow.VerifyExecutionAsync` → `ImageInventoryService.BuildInventoryAsync(workingImage.WorkingWimPath, workingImage.Index)`

`BuildInventoryAsync` (diseñado para el inventario de solo lectura de las
fases 3/4) **crea SIEMPRE su propio workspace nuevo** con su propio
`mountDir`, monta ahí el WIM recibido (el de la copia de trabajo del
`RemovalEngine`, workspace `<B>`) en modo solo lectura, inventaría, y en su
`finally`:

```csharp
if (mounted) await SafeUnmountAsync(mountDir, ct);   // ExitCode != 0 -> solo se registra, no se propaga
if (!keepWorkspace) workspace.Delete();              // se borra SIEMPRE que no hubo excepción
```

`SafeUnmountAsync` nunca marcaba `keepWorkspace = true` cuando el desmontaje
fallaba: solo registraba el error y volvía. Si esa segunda `/Unmount-Wim
/Discard` (sobre el workspace `<A>` de la verificación) no completaba
realmente el desmontaje, el código **borraba de todos modos el directorio
`<A>\mount`**, que DISM seguía considerando montado — dejando su registro
interno con una entrada huérfana e inconsistente ("Status: Invalid"), cuyo
`Image File` apunta al WIM real (workspace `<B>`, que sigue existiendo) y
cuyo `Mount Dir` apunta a un directorio que ya no existe (workspace `<A>`,
borrado).

## 2. Diagrama real del flujo

```
RemovalEngine (workspace B)
  WorkingImageFactory.CreateAsync
    -> crea workspace B (source/mount/logs/output)
    -> Export-Image  ->  B\source\install.wim
  RemovalEngine.ExecuteAsync
    -> Mount-Wim  B\source\install.wim  en  B\mount   (ReadWrite)
    -> RemoveAppx Clipchamp.Clipchamp
    -> Unmount-Wim /Commit  B\mount        ✅ confirmado por el log real
    -> workspace B NUNCA se borra (contiene el resultado)

MainWindow.VerifyExecutionAsync (tras el commit)
  ImageInventoryService.BuildInventoryAsync(B\source\install.wim, index)
    -> crea workspace A  (¡NUEVO, independiente!)
    -> Mount-Wim  B\source\install.wim  en  A\mount   (ReadOnly)
    -> Get-Packages / Get-Features / ...
    -> Unmount-Wim /Discard  A\mount     ⚠️ si esto no se confirma...
    -> workspace.Delete() (borra A\mount) ⚠️ ...se borra igualmente

Get-MountedWimInfo (tras todo)
    Mount Dir  : ...\workspaces\A\mount     (ya no existe en disco)
    Image File : ...\workspaces\B\source\install.wim  (sigue existiendo)
    Status     : Invalid
```

## 3. Por qué aparecían IDs distintos

`<A>` y `<B>` son, **por diseño**, workspaces distintos: `RemovalEngine`
opera sobre `B` (donde vive la imagen de trabajo real) y la verificación
posterior crea legítimamente su propio workspace `A` para montar en modo
solo lectura sin interferir con `B`. Eso en sí mismo **no es un error**. El
error es que `A` se eliminaba del disco sin haber confirmado que DISM ya no
lo consideraba montado, dejando una referencia cruzada huérfana
(`Mount Dir` de `A`, `Image File` de `B`) con estado "Invalid" en el registro
de DISM.

## 4. Archivos modificados

- `src/MRS.ImageEngine/Inventory/ImageInventoryService.cs` — corrección principal.
- `src/MRS.ImageEngine/Inventory/InventoryWorkspace.cs` — `WorkspaceId`.
- `src/MRS.RemovalEngine/Models/WorkingImage.cs` — `WorkspaceId`, `HasConsistentWorkspace()`.
- `src/MRS.RemovalEngine/RemovalEngine.cs` — guarda de preflight + logging estructurado.
- `src/MRS.RemovalEngine/WorkingImageFactory.cs` — logging estructurado.
- `tests/MRS.ImageEngine.Tests/Fakes/FakeDismRunner.cs`,
  `tests/MRS.ImageEngine.Tests/ImageInventoryServiceTests.cs`.
- `tests/MRS.RemovalEngine.Tests/WorkspaceLifecycleTests.cs` (nuevo).

**No se ha tocado**: `ComponentCatalog`, `RemovalPlanning`, reglas de
protección, acciones de eliminación, perfiles, generación de ISO ni la UI de
progreso (`MainWindow.xaml`/`.xaml.cs` no se han modificado).

## 5. Solución aplicada

### Corrección principal — `ImageInventoryService.BuildInventoryAsync`

`SafeUnmountAsync` ahora **siempre** comprueba `Get-MountedWimInfo` después
del intento de desmontaje (haya tenido `ExitCode` 0 o no) y devuelve si queda
**confirmado** que el montaje ya no existe. El workspace solo se borra si:

```
!keepWorkspace (no hubo excepción)  &&  confirmedUnmounted (Get-MountedWimInfo ya no lo lista)
```

Si el desmontaje no puede confirmarse, el workspace se conserva (igual que
en el resto de fallos) y se registra un error explícito con `WorkspaceId` y
`MountDir`. Esto es exactamente la regla del prompt: *"NO considerar un
`ExitCode=0` de una operación intermedia como éxito global"* aplicada al
desmontaje de la verificación — nunca se borra un directorio que DISM
todavía podría tener registrado.

### Logging estructurado

Se añaden líneas `OperationId` / `WorkspaceId` / `SourceWimPath` /
`WorkingWimPath` / `MountDir`:

- `[WORKSPACE]` en `WorkingImageFactory` (creación, tras Export-Image) y en
  `RemovalEngine` (preflight, tras montar, antes de cada acción, antes/tras
  el commit, en el discard). El `OperationId` es el `WorkspaceId` de la
  imagen de trabajo: se mantiene idéntico en todas estas líneas, por lo que
  cualquier discrepancia futura sería visible de inmediato en el log.
- `[INVENTORY]` en `ImageInventoryService.BuildInventoryAsync` (creación,
  montaje, resultado del desmontaje), con su **propio** `WorkspaceId`,
  distinto del de un `RemovalEngine` en curso. No se usa la etiqueta
  "VERIFY" a secas porque este mismo método también se usa para el
  inventario inicial de las fases 3/4 (no solo para verificar tras aplicar
  cambios); `[INVENTORY]` es precisa en ambos casos y evita etiquetar mal el
  caso principal.

`RemovalVerifier` (el comparador antes/después) no crea ningún workspace: es
una función pura sobre dos `ImageInventory` ya obtenidos, así que no había
nada que corregir ahí; se revisó y se confirma que no está implicado en el
problema.

### Guarda defensiva — `WorkingImage.HasConsistentWorkspace()`

Nueva comprobación: `MountPath` y `WorkingWimPath` deben colgar de
`WorkspacePath`. `RemovalEngine.ExecuteAsync` la ejecuta como primer paso del
preflight; si detecta una combinación cruzada, aborta con
`Phase = PreFlight` **antes de montar nada**. Esto no cambia el
comportamiento de ninguna ejecución real (todas las imágenes de trabajo
creadas por `WorkingImageFactory` son, por construcción, consistentes) pero
convierte el escenario descrito en el prompt en algo detectable en tiempo de
ejecución, no solo en un test.

## 6. Tests añadidos

- `tests/MRS.ImageEngine.Tests/ImageInventoryServiceTests.cs`:
  `BuildInventoryAsync_keeps_workspace_when_unmount_reports_success_but_mount_is_still_registered`
  — reproduce el bug exacto (discard con `ExitCode 0` pero
  `Get-MountedWimInfo` sigue listando el mismo `MountDir` como "Invalid") y
  comprueba que el workspace **no** se borra.
- `tests/MRS.ImageEngine.Tests/Fakes/FakeDismRunner.cs`: `LastMountDir` +
  `SimulateStillMountedAfterUnmount` para poder reproducir ese escenario sin
  depender de una ISO real.
- `tests/MRS.RemovalEngine.Tests/WorkspaceLifecycleTests.cs` (nuevo, 4 tests):
  - Una imagen de trabajo normal tiene un workspace consistente.
  - Se detecta una combinación cruzada (`MountPath` de un workspace,
    `WorkingWimPath` de otro) — el test que exige el prompt: si se reintrodujera
    el bug, `HasConsistentWorkspace()` (y el propio `RemovalEngine`) lo
    detectarían.
  - `RemovalEngine.ExecuteAsync` se niega a montar una `WorkingImage` con
    workspace cruzado (aborta en `PreFlight`, nunca llama a `Mount-Wim`).
  - Una ejecución completa y correcta nunca cambia de workspace: todas las
    llamadas de mount/commit/discard usan el mismo `MountDir`.

Los tests de éxito/error/cancelación/commit/discard/verificador de las fases
7 y 8 (`RemovalEngineTests.cs`, `RemovalVerifierTests.cs`) siguen intactos y
en verde; no fue necesario modificarlos porque el comportamiento del
`RemovalEngine` en sí no cambia (solo se añadió la guarda de preflight, que
no afecta a ninguna imagen de trabajo real/consistente).

## 7. Resultado de `dotnet build` / `dotnet test`

```
dotnet build:
  MRS.ImageEngine, MRS.RemovalEngine y los 4 proyectos de test: 0 errores, 0 advertencias.
  MRS.WindowsBuilder: compila su XAML y C# sin errores (no se ha modificado).
  (El build de la solución completa sigue bloqueado únicamente por el .exe
   de la instancia elevada que el usuario mantiene abierta para sus pruebas
   reales — no es un error de código.)

dotnet test:
  MRS.ImageEngine.Tests       : 78/78  (+1)
  MRS.ComponentCatalog.Tests  : 41/41
  MRS.RemovalPlanning.Tests   : 48/48
  MRS.RemovalEngine.Tests     : 41/41  (+4)
```

Total: **208/208**. Sin regresiones.

## Fuera de alcance (según el propio prompt)

No se ha tocado la barra de progreso ni ningún otro punto de la UI; no se ha
ejecutado ni se ejecutará `/Cleanup-Mountpoints`; no se ha borrado ningún
registro de DISM manualmente durante esta corrección.
