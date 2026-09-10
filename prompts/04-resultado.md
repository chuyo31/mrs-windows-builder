# Resultado - P4: Inventario REAL del WIM (montar → leer → desmontar)

## Archivos creados / modificados

- `src/MRS.DismEngine/Dism/IDismRunner.cs` / `DismRunner.cs` — operaciones alineadas
  con el prompt: `GetMountedWimInfoAsync` (`/Get-MountedWimInfo`),
  `MountWimAsync` (`/Mount-Wim /WimFile:.. /Index:N /MountDir:.. /ReadOnly`),
  `UnmountWimDiscardAsync` (`/Unmount-Wim /Discard`). El índice se pasa como
  parámetro, nunca codificado.
- `src/MRS.ImageEngine/Inventory/ImageInventoryService.cs` — usa las nuevas
  operaciones `*-Wim`; log de error más completo (fase + comando + `ExitCode` +
  extracto de stdout/stderr con `LogDismDiagnostics`); en caso de error solo se
  marca el workspace como conservado y el `finally` registra
  `Workspace conservado: <ruta>`; tras desmontar se consulta
  `/Get-MountedWimInfo` y se confirma "No quedan montajes de MRS";
  mensaje final `Imagen inventariada correctamente.`
- `src/MRS.WindowsBuilder/MainWindow.xaml` — indicador visible
  "Imagen inventariada correctamente" en la cabecera de la pantalla de inventario.
- `tests/MRS.ImageEngine.Tests/Fakes/FakeDismRunner.cs` — adaptado a la interfaz
  `*-Wim`.
- `tests/MRS.ImageEngine.Tests/ImageInventoryServiceTests.cs` — +3 tests:
  orden exacto de operaciones (mount → packages → features → capabilities →
  apps → drivers → unmount), workspace conservado si falla el montaje, y
  verificación de `/Get-MountedWimInfo` después de desmontar.

Modelos y parsers de la fase 3 reutilizados sin cambios
(`ImagePackage`, `ImageFeature`, `ImageCapability`, `ProvisionedApp`,
`ImageDriver`, `ImageInventory` con contadores calculados; parsers
`/English`, independientes de idioma/orden/campos opcionales).

## Arquitectura

```
MainWindow
   ↓
ImageInventoryService     (workspace único · try/finally · recuperación de huérfanos)
   ↓
DismRunner
   ↓
DISM.exe
```

Flujo real: seleccionar ISO → Analizar → elegir edición → **Continuar** →
`%LOCALAPPDATA%\MRS-Windows-Builder\workspaces\<GUID>\` (`source/ mount/ logs/
output/`) · **no se copia la ISO**, se usa la ruta de `install.wim` de la ISO
montada de solo lectura.

## Comandos DISM utilizados

| Fase | Comando |
|------|---------|
| Comprobar montajes | `DISM /English /Get-MountedWimInfo` |
| Montar índice N | `DISM /English /Mount-Wim /WimFile:"<wim>" /Index:N /MountDir:"<ws>\mount" /ReadOnly` |
| Paquetes | `DISM /English /Image:"<mount>" /Get-Packages` |
| Features | `DISM /English /Image:"<mount>" /Get-Features` |
| Capabilities | `DISM /English /Image:"<mount>" /Get-Capabilities` |
| Appx provisionadas | `DISM /English /Image:"<mount>" /Get-ProvisionedAppxPackages` |
| Drivers | `DISM /English /Image:"<mount>" /Get-Drivers` |
| Desmontar | `DISM /English /Unmount-Wim /MountDir:"<ws>\mount" /Discard` |
| Verificar | `DISM /English /Get-MountedWimInfo` → *No mounted images found* |

Sin `/Cleanup-Image`, `/StartComponentCleanup`, `/ResetBase` ni
`/Cleanup-Mountpoints`.

## Resultado de `dotnet build`

Bibliotecas de motor + proyecto de tests: **0 errores, 0 advertencias**.

> El `dotnet build` de la solución completa no pudo regenerar los binarios de
> `MRS.WindowsBuilder` porque había una instancia **elevada** de la app en
> ejecución bloqueando los DLL de salida (MSB3021/MSB3027). Es un bloqueo de
> archivo, no un error de código: XAML y C# compilan sin errores. Cerrar la app
> y repetir `dotnet build` regenera el `.exe`.

## Resultado de `dotnet test`

```
Correctas! - Con error: 0, Superado: 77, Omitido: 0, Total: 77
```

Cobertura P4: orden de operaciones, montaje `/ReadOnly`, desmontaje al terminar
bien, desmontaje + workspace conservado al fallar una categoría, workspace
conservado al fallar el montaje, verificación post-desmontaje, propagación de
`ExitCode != 0`, múltiples paquetes/features/capabilities/appx/drivers, parser
con campos opcionales y en distinto orden. Ningún test usa una ISO real.

## Resultado de la prueba REAL

_(A completar ejecutando la app elevada con la ISO real de Windows 11 26H2 y
seleccionando "Windows 11 Pro" → Continuar.)_

| Comprobación | Resultado |
|---|---|
| Montaje real del índice (Pro) | _pendiente_ |
| `Get-Packages` real | _pendiente_ |
| `Get-Features` real | _pendiente_ |
| `Get-Capabilities` real | _pendiente_ |
| `Get-ProvisionedAppxPackages` real | _pendiente_ |
| `Get-Drivers` real | _pendiente_ |
| Desmontaje real (`/Unmount-Wim /Discard`) | _pendiente_ |
| `Get-MountedWimInfo` final → *No mounted images found* | _pendiente_ |

Números reales:

| Categoría | Nº |
|---|---|
| Paquetes | _pendiente_ |
| Aplicaciones | _pendiente_ |
| Features | _pendiente_ |
| Capabilities | _pendiente_ |
| Drivers | _pendiente_ |

Resultado del desmontaje: _pendiente_.

## Problemas encontrados

- La regeneración del ejecutable requiere cerrar la instancia elevada de la app
  (bloqueo de DLL de salida durante el build).
- El montaje del WIM y la prueba real necesitan Windows con privilegios de
  administrador; no se cubren con tests unitarios (el prompt prohíbe usar una
  ISO real). Toda la lógica de montaje/desmontaje/recuperación sí está cubierta
  con `FakeDismRunner`.
- Las imágenes ESD no se pueden montar con `/Mount-Wim`; el servicio lo detecta
  y devuelve un error claro (conversión a WIM en una fase posterior).

## Fuera de alcance

Sin implementar: eliminación, perfiles, catálogo de componentes, dependencias,
protección, limpieza, compresión, creación de ISO, App Packs y PCPI.
