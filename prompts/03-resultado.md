# Resultado - P3: Inventario de la imagen Windows (SOLO LECTURA)

## Archivos creados / modificados

### MRS.DismEngine
Modificados:
- `Dism/IDismRunner.cs` / `Dism/DismRunner.cs` — nuevas operaciones (todas con
  `/English`): `GetMountedImageInfoAsync`, `MountImageAsync` (`/Mount-Image` +
  `/ReadOnly`), `UnmountImageDiscardAsync` (`/Unmount-Image /Discard`),
  `GetPackagesAsync`, `GetFeaturesAsync`, `GetCapabilitiesAsync`,
  `GetProvisionedAppxPackagesAsync`, `GetDriversAsync`. Timeouts diferenciados
  (montaje 20 min, desmontaje 15 min, consultas 5 min). No hay ningún comando de
  limpieza (`/StartComponentCleanup`, `/ResetBase`, `/Cleanup-Mountpoints`).

### MRS.ImageEngine
Creados:
- `Models/ImagePackage.cs` — PackageIdentity, State, ReleaseType, InstallTime, Description.
- `Models/ImageFeature.cs` — Name, State.
- `Models/ImageCapability.cs` — Identity, State.
- `Models/ProvisionedApp.cs` — DisplayName, PackageName, Version, Architecture, ResourceId, PublisherId.
- `Models/ImageDriver.cs` — PublishedName, OriginalFileName, Provider, Class, Version, Date, BootCritical.
- `Models/ImageInventory.cs` — Packages, Features, Capabilities, ProvisionedApps, Drivers + contadores. Independiente de la interfaz.
- `Parsing/DismBlockReader.cs` — lector genérico `clave : valor` de la salida `/English`: tolerante a orden distinto, campos opcionales, líneas vacías, múltiples elementos y continuaciones de línea; descarta el banner de DISM.
- `Parsing/DismInventoryParsers.cs` — `DismPackageParser`, `DismFeatureParser`, `DismCapabilityParser`, `DismProvisionedAppParser` (deriva `PublisherId` del `PackageName` si falta), `DismDriverParser`.
- `Inventory/InventoryWorkspace.cs` — workspace temporal único por operación en `%LOCALAPPDATA%\MRS-Windows-Builder\workspaces\<GUID>\` con `source\ mount\ logs\ output\`. Sin letras de unidad fijas. Comprueba que está libre; se puede eliminar entero.
- `Inventory/InventoryResult.cs` — inventario + ruta del workspace + si se conservó.
- `Inventory/ImageInventoryService.cs` — coordinador del inventario.

### MRS.WindowsBuilder
Modificados:
- `MainWindow.xaml` — pantalla de **INVENTARIO** superpuesta: contadores
  (Paquetes / Apps / Features / Capabilities / Drivers), lista de categorías,
  `DataGrid` (Nombre / Estado / Detalles) y botones **Volver** / **Continuar**.
- `MainWindow.xaml.cs` — "Continuar" llama a `ImageInventoryService.BuildInventoryFromIsoAsync`;
  rellena la pantalla de inventario y permite cambiar de categoría. "Continuar"
  dentro del inventario NO modifica nada (solo registra en el log). Sin lógica
  DISM en la ventana.

### tests/MRS.ImageEngine.Tests
- `Fakes/FakeDismRunner.cs` — ampliado a toda la interfaz `IDismRunner`.
- `Data/DismOutputs.cs` — salidas `/English` reproducidas de `/Get-Packages`,
  `/Get-Features`, `/Get-Capabilities`, `/Get-ProvisionedAppxPackages`,
  `/Get-Drivers` y `/Get-MountedImageInfo`.
- `InventoryParsersTests.cs`, `ImageInventoryServiceTests.cs`, `InventoryWorkspaceTests.cs`.

## Arquitectura implementada

```
MainWindow
   ↓
ImageInventoryService      (coordina)
   ↓
DismRunner                 (ejecuta DISM)
   ↓
DISM.exe
```

Flujo de `BuildInventoryAsync` con patrón `try / finally`:

1. `[INFO] Creando workspace...` — workspace único con GUID + subdirectorios.
2. Comprobación de montajes DISM previos; recuperación **controlada** solo de
   montajes huérfanos que cuelguen de nuestra carpeta de workspaces (nunca se
   tocan montajes de otros programas).
3. `[INFO] Montando imagen índice X...` → `DISM /Mount-Image ... /ReadOnly`.
4. Inventario secuencial: `[INFO] Inventariando paquetes/características/capacidades/aplicaciones/drivers...`.
5. `finally`: `[INFO] Desmontando imagen...` → `/Unmount-Image /Discard`, y se
   **verifica** con `/Get-MountedImageInfo` que ya no aparece montada.
6. Si todo fue bien, el workspace se elimina. Si hubo error, se conserva como
   workspace de diagnóstico y se informa de su ruta en el log
   (`[ERROR] ... Workspace de diagnóstico conservado en: ...`).

Errores de DISM: `[ERROR] Inventario de <fase> fallido.` + `[ERROR] Código DISM: XXXXX`
(el código no se oculta; el usuario sabe qué fase falló).

## Qué información se puede inventariar

| Categoría   | Origen DISM                     | Campos |
|-------------|----------------------------------|--------|
| Paquetes    | `/Get-Packages`                  | Package Identity, State, Release Type, Install Time, Description (si está) |
| Features    | `/Get-Features`                  | Feature Name, State |
| Capabilities| `/Get-Capabilities`             | Capability Identity, State |
| Apps prov.  | `/Get-ProvisionedAppxPackages`  | DisplayName, PackageName, Version, Architecture, ResourceId, PublisherId |
| Drivers     | `/Get-Drivers`                  | Published Name, Original File Name, Provider, Class, Version, Date, Boot Critical |

Servicios y tareas programadas: **no** se inventarían en esta fase (requerirían
cargar hives de registro o modificar la imagen). Quedan preparados para más
adelante.

## Resultado de `dotnet build`

```
dotnet build MRS-Windows-Builder.sln
Compilación correcta.
    0 Advertencia(s)
    0 Errores
```

(La aplicación se ejecuta correctamente.)

## Resultado de `dotnet test`

```
dotnet test MRS-Windows-Builder.sln
Correctas! - Con error: 0, Superado: 74, Omitido: 0, Total: 74 - MRS.ImageEngine.Tests.dll (net8.0)
```

Nuevas pruebas (24) para: modelos de inventario y contadores; parseo de
paquetes, features, capabilities, apps (con `PublisherId` derivado) y drivers
(incl. `Boot Critical`); salida vacía / sin banner; tolerancia a orden distinto y
campos opcionales; workspace (creación de subdirectorios, unicidad, borrado);
`ImageInventoryService` (inventario completo, montaje `/ReadOnly` + desmontaje en
orden, propagación del `ExitCode` de DISM al fallar el montaje, conservación del
workspace y desmontaje cuando falla una categoría, recuperación solo de montajes
huérfanos propios). Ningún test usa una ISO real.

## Problemas encontrados

- Como en fases anteriores, el primer `dotnet build` puede fallar si queda una
  instancia de la app abierta bloqueando el `.exe`; se resuelve cerrándola.
- `/Get-ProvisionedAppxPackages` no siempre emite `PublisherId`; cuando falta se
  deriva del último segmento del `PackageName` (`Nombre_Version_Arch_ResourceId_PublisherId`).
- El montaje de la imagen requiere Windows real y privilegios de administrador;
  no se cubre con tests unitarios (el prompt prohíbe usar una ISO real). La lógica
  de montaje/desmontaje/recuperación sí está cubierta mediante `FakeDismRunner`.
- Las imágenes ESD no pueden montarse con `DISM /Mount-Image`; el servicio lo
  detecta y devuelve un error claro (conversión a WIM en una fase posterior).

## Fuera de alcance (siguiente fase)

Sin implementar: eliminación de componentes, perfiles Normal/Light/Medium/Ultra,
modificaciones de registro, limpieza de componentes, compresión, creación de ISO,
integración de PCPI y App Packs.
