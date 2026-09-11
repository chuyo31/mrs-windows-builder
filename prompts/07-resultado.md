# Resultado - P7: RemovalEngine seguro (primera fase que modifica una imagen)

## Archivos creados / modificados

### MRS.DismEngine
`Dism/IDismRunner.cs` / `Dism/DismRunner.cs`: nuevas operaciones `/English`
para la fase de modificación: `ExportImageAsync` (`/Export-Image`),
`UnmountWimCommitAsync` (`/Unmount-Wim /Commit`),
`RemoveProvisionedAppxPackageAsync`, `DisableFeatureAsync`,
`RemoveCapabilityAsync`, `RemovePackageAsync`. `FakeDismRunner` de
`MRS.ImageEngine.Tests` actualizado para seguir implementando la interfaz.

### MRS.RemovalEngine (nuevo proyecto)
Depende de `MRS.DismEngine` + `MRS.ImageEngine` + `MRS.RemovalPlanning` (nunca
al revés: `MRS.RemovalPlanning` no conoce este proyecto).

- `Models/WorkingImage.cs`, `RemovalExecutionPhase`, `RemovalExecutionItem`,
  `RemovalExecutionResult`, `RemovalVerificationResult`.
- `RemovalEngineException.cs`.
- `MountedWimInfo.cs`: utilidades sobre `/Get-MountedWimInfo` (extraer "Mount
  Dir", comprobar pertenencia a nuestro árbol de workspaces).
- `IWorkingImageFactory` / `WorkingImageFactory.cs`: crea la copia de trabajo
  con `DISM /Export-Image` (exporta solo el índice elegido; el origen se abre
  siempre en lectura).
- `IRemovalEngine` / `RemovalEngine.cs`: VALIDAR → MONTAR (ReadWrite) →
  EJECUTAR → VALIDAR → COMMIT/DISCARD.
- `RemovalVerifier.cs`: compara inventario antes/después.

### tests/MRS.RemovalEngine.Tests (nuevo, 35 tests)
`Fakes/FakeDismRunner.cs`, `PlanFactory.cs`,
`WorkingImageFactoryTests.cs` (6), `RemovalEngineTests.cs` (20),
`RemovalVerifierTests.cs` (9). Ninguno usa una ISO real; los fallos se
simulan con `FakeDismRunner` (ExitCode ≠ 0, cancelación a mitad de ejecución).

### MRS.WindowsBuilder
- `MainWindow.xaml`: pantalla **APLICANDO CAMBIOS** (estado, progreso "X / Y",
  lista de componentes con ○/⏳/✓/✗/⊘, botones Cancelar/Cerrar).
- `MainWindow.xaml.cs`: "Confirmar plan" muestra el aviso obligatorio
  ("se creará una copia de trabajo... el original no será modificado... si
  falla se descarta") con **Cancelar/Aplicar cambios**; solo tras aceptar se
  monta la ISO, se localiza `install.wim/esd`, se crea la imagen de trabajo,
  se ejecuta el `RemovalEngine` y —si hay commit— se reinventaría la imagen de
  trabajo y se compara con `RemovalVerifier`.

## Arquitectura

```
ISO original (solo lectura)
    ↓ (Export-Image de la edición elegida)
WorkingImage (copia independiente)
    ↓ Mount-Wim (ReadWrite)
RemovalPlan (ya validado, MRS.RemovalPlanning)
    ↓
RemovalEngine  ->  DismRunner  ->  DISM.exe
    ↓
Validación (ExitCode, bloqueo de seguridad por acción)
    ↓
TODO OK → Unmount-Wim /Commit        CUALQUIER ERROR/CANCEL → Unmount-Wim /Discard
```

`MRS.RemovalPlanning` sigue sin referenciar `MRS.DismEngine` (garantía de
compilación de la fase 6, intacta). `RemovalEngine` no clasifica ni decide
protección: solo ejecuta lo que el plan ya autorizó, y antes de cada acción
vuelve a comprobar componente/permitido/protección/compatibilidad/target.

## Working image

`WorkingImageFactory.CreateAsync`: comprueba que el origen existe, comprueba
espacio libre (tamaño origen + margen de 200 MB), crea un `InventoryWorkspace`
único (`source/mount/logs/output`, sin letras de unidad fijas) y exporta
**solo la edición elegida** a `workspace\source\install.wim`. Tras exportar,
el índice de trabajo es siempre `1` (el WIM de destino contiene una única
imagen). El origen nunca se abre en escritura.

## Ejecución DISM

Orden fijo: AppX → Features → Capabilities → Packages (nunca reordenado por
dependencias: el plan ya las validó). Comandos: `/Remove-ProvisionedAppxPackage
/PackageName:`, `/Disable-Feature /FeatureName:` (nunca `/Remove`),
`/Remove-Capability /CapabilityName:`, `/Remove-Package /PackageName:`. El
`Target` siempre viene del catálogo (no se reconstruye heurísticamente).
Nunca `/ResetBase`, `/StartComponentCleanup` ni `/Cleanup-Image`. Criterio de
éxito: `ExitCode == 0` exclusivamente (nunca se busca texto en stdout/stderr).

## Política de abort/discard

Antes de cada acción se repite el bloqueo de seguridad (componente en el
plan, `Allowed`, no protegido, acción compatible con el `SourceType`, target
no vacío); si falla, se aborta sin ejecutar nada. Ante el primer `ExitCode ≠
0` o una cancelación, se detiene inmediatamente (no se ejecutan acciones
posteriores), se hace `/Unmount-Wim /Discard` y se verifica con
`/Get-MountedWimInfo` que no queda el montaje. El workspace **se conserva**
en error o cancelación (diagnóstico). También recupera montajes huérfanos
propios de una ejecución anterior interrumpida, sin tocar montajes de otras
aplicaciones.

## Commit

Solo si todas las acciones tuvieron éxito: `/Unmount-Wim /Commit` +
verificación de `/Get-MountedWimInfo`. Si el propio commit falla, se marca
`Failed`/`Committed=false` y se intenta un discard de seguridad. El workspace
**no se borra tras un éxito**: contiene la imagen de trabajo, que es el
resultado real de esta fase.

## Validación / verificación

`RemovalVerifier.Verify` reinventaría (vía `ImageInventoryService`, reutilizando
la fase 3/4) la imagen de trabajo ya confirmada y compara contra el
inventario "antes": `Removed`, `StillPresent` (con aviso si lo eliminado
sigue apareciendo), `Failed`, `UnexpectedChanges` (deltas de recuento que no
coinciden con lo ejecutado), `Warnings`.

## UI

Ver "MRS.WindowsBuilder" arriba. Progreso en vivo mediante los mismos
mensajes de log `[REMOVAL] Inicio: ...` / `... eliminado correctamente.` que
emite el motor (sin acoplar la UI al motor más allá del log ya existente);
al terminar, la lista se reconcilia con el resultado real (`ActionsExecuted`/
`ActionsFailed`) para que el estado final sea siempre fiel a lo ocurrido.

## Resultado de `dotnet build`

`MRS.RemovalEngine` y los 4 proyectos de tests: **0 errores, 0 advertencias**.
`MRS.WindowsBuilder` compila su XAML y C# sin errores; el build de la
solución completa sigue bloqueado únicamente por el `.exe` de la instancia
elevada en ejecución (no es un error de código).

## Resultado de `dotnet test`

```
MRS.ImageEngine.Tests       : 77/77
MRS.ComponentCatalog.Tests  : 41/41
MRS.RemovalPlanning.Tests   : 48/48
MRS.RemovalEngine.Tests     : 35/35
```

Total: **201/201**. Sin regresiones en análisis, inventario, catálogo ni plan.

## Prueba real

**No ejecutada en este entorno**: requiere Windows elevado con DISM real y la
ISO física de Windows 11 Pro 26H2 (26300.9278), que este entorno de
desarrollo no tiene. El código está listo y cubierto por 35 tests con
`FakeDismRunner` que simulan exactamente el flujo real (export, mount
ReadWrite, ejecución ordenada, abort/discard, commit, verificación de
montajes). Para ejecutarla:

1. `dotnet build MRS-Windows-Builder.sln` y ejecutar la app **elevada**.
2. Analizar la ISO real, elegir **Windows 11 Pro**, generar el catálogo.
3. Marcar únicamente un componente `Protection = Removable`, `Risk = Low`
   (p. ej. **Clipchamp**, si el catálogo lo marca así; si no, otra AppX que sí
   cumpla ambas condiciones).
4. Ver plan → Confirmar plan → aceptar el aviso → esperar "Cambios aplicados".

Pendiente de rellenar tras esa ejecución real:
- Componente realmente eliminado: _pendiente_.
- Resultado del reinventario (Removed/StillPresent/UnexpectedChanges): _pendiente_.
- Comprobación de la ISO original (misma ruta/tamaño): _pendiente_.
- `Get-MountedWimInfo` final: _pendiente_ (debe ser "No mounted images found").

## Problemas encontrados

- La recuperación de montajes huérfanos usa como raíz el directorio **padre**
  del workspace de la imagen actual (no una ruta fija), para no asumir
  `%LOCALAPPDATA%` en los tests; en producción coincide con
  `InventoryWorkspace.WorkspacesRoot()`.
- El progreso en vivo de la UI se basa en el texto exacto de los mensajes
  `[REMOVAL]` que emite el propio motor; al terminar la ejecución se
  reconcilia siempre con el resultado real para no depender de ese parseo
  para la corrección final.
- Regenerar `MRS.WindowsBuilder.exe` sigue requiriendo cerrar la instancia
  elevada en ejecución antes de compilar (igual que en fases anteriores).

## Fuera de alcance (siguiente fase)

Perfiles automáticos Normal/Light/Medium/Ultra/Custom, `/Remove` de features,
limpieza de checkpoints/ResetBase, compresión, creación de ISO final,
integración PCPI y App Packs.
