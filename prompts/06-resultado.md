# Resultado - P6: Motor de selección y RemovalPlan

## Archivos creados / modificados

### MRS.RemovalPlanning (nuevo proyecto)
No referencia `MRS.DismEngine` — ni siquiera de forma transitiva: la
referencia de `MRS.ImageEngine` a `MRS.DismEngine` se marcó
`PrivateAssets="all"`, así que `RemovalPlanBuilder` no puede compilar código
que use `IDismRunner`/`DismRunner` aunque quisiera (comprobado con una prueba
que intenta usarlo y falla, y con un test que inspecciona los ensamblados
referenciados).

`Models/`: `RemovalActionType`, `ComponentCurrentState`, `RemovalWarningSeverity`,
`EstimatedSize` (siempre `Unknown` salvo un futuro `SizeAnalyzer`),
`RemovalWarning`, `RemovalAction`, `ComponentSelection` (por `ComponentId`
estable, nunca por índice de fila), `RemovalPlanItem`, `RemovalPlan`
(`ImageId`, `CreatedAt`, `Components`, `Actions`, `Warnings`, `Errors`,
`TotalSelected/Allowed/Blocked`, `IsValid`, `Profile` preparado para P7),
`PlanValidationResult`.

`IRemovalPlanBuilder` / `RemovalPlanBuilder`: `ImageInventory` +
`ComponentCatalogResult` + `IReadOnlyCollection<ComponentSelection>` →
`RemovalPlan`. `RemovalPlanValidator`: segunda capa de verificación
independiente sobre un plan ya construido. `Serialization/RemovalPlanSerializer`:
JSON con versión de formato (`ToJson`/`FromJson`/`SaveToFile`/`LoadFromFile`).

### MRS.WindowsBuilder
- `MainWindow.xaml`: zona **RESUMEN DE SELECCIÓN** (Seleccionados/Permitidos/
  Bloqueados/⚠ Advertencias) bajo el `DataGrid` de componentes, botón
  **Ver plan**; nueva pantalla **PLAN DE MODIFICACIÓN** (tarjetas ✓ ELIMINAR /
  🔒 BLOQUEADO con categoría·acción o motivo de bloqueo y riesgo) con
  **Volver**, **Cancelar selección** y **Confirmar plan**.
- `MainWindow.xaml.cs`: recalcula el `RemovalPlan` en vivo con cada casilla
  (`ComponentSelectionCheckBox_Changed`); "Confirmar plan" solo guarda el plan
  en memoria (`_confirmedPlan`), nunca modifica el WIM.

### tests/MRS.RemovalPlanning.Tests (nuevo, 48 tests)
Selección, protección, dependencias/dependientes, estados de paquete
(Installed/Superseded/Unknown), AppX (removible/protegida/framework),
features, capabilities (incl. Not Present), plan (acciones correctas,
contadores, `IsValid`, tamaño siempre desconocido), validador (acción
incompatible, componente inexistente/no detectado/protegido, conflicto,
dependiente que se queda, parámetros inválidos) y serialización (round-trip,
versión de formato, acciones/avisos/bloqueos conservados, `SaveToFile`).
Ninguno usa una ISO real.

## Arquitectura

```
Selección (ComponentId estable)
    ↓
Catálogo (MRS.ComponentCatalog, fase 5)
    ↓
Protección          -> bloqueo absoluto, con motivo
    ↓
Estado DISM         -> Superseded/Not Present/Unknown nunca generan acción
    ↓
Dependencias        -> nunca eliminación en cascada silenciosa
    ↓
Dependientes        -> bloquea si algo que se queda lo necesita
    ↓
RemovalPlan (Components + Actions + Warnings + Errors)
    ↓
RemovalPlanValidator (segunda verificación independiente)
    ↓
(futuro) RemovalEngine -> DismRunner
```

`RemovalPlanBuilder` y `RemovalPlanValidator` no ejecutan nada: son puras
funciones sobre datos. La garantía de que no pueden llegar a `DismRunner` es
de compilación, no solo documental.

## Validaciones implementadas

`RemovalPlanValidator` comprueba: `ComponentId` no vacío, componente existente
en el catálogo, componente detectado, ninguna acción permitida sobre un
componente protegido, acción compatible con el `SourceType`
(`RemovePackage`↔Package, `RemoveAppx`↔Appx, `DisableFeature`↔Feature,
`RemoveCapability`↔Capability), estado compatible con una acción permitida
(nunca Unknown/NotPresent), sin conflictos entre componentes permitidos, y sin
dejar un dependiente que se queda sin lo que necesita.

## Protección

Se reutilizan las reglas de la fase 5 (`ComponentDefinition.Protection`).
Nuevo aquí: `Protected` implica siempre `Allowed=false` + `Action=None` +
`BlockReason` (el motivo del catálogo, o uno genérico) + aviso `Critical`.
AppX solo se permite si `Protection == Removable` (ni Optional ni Recommended
ni Unknown), lo que además bloquea automáticamente los frameworks AppX
(normalmente `Recommended`) aunque no exista una regla de protección
explícita para ellos.

## Dependencias

`Dependencies`/`Dependents` del catálogo (fase 5) se consultan por componente.
Seleccionar A nunca marca B para eliminar. Si algún componente que **no** se
elimina (no seleccionado, o seleccionado pero bloqueado) sigue dependiendo de
B, B se bloquea con "El componente es requerido por otros componentes que
permanecerán en la imagen." (punto fijo iterativo, para que un bloqueo en
cadena se propague correctamente). Si A usa una dependencia protegida se añade
un aviso `⚠ A utiliza B` sin bloquear A.

## UI

Ver "MRS.WindowsBuilder" arriba. El resumen y el plan se recalculan en vivo;
"Confirmar plan" guarda el `RemovalPlan` en memoria (variable `_confirmedPlan`
en `MainWindow`) para que una fase posterior (el `RemovalEngine`) pueda
recogerlo; no escribe nada en disco ni toca el WIM.

## Serialización

`RemovalPlanSerializer` envuelve el plan en `{ FormatVersion, Plan }` (JSON con
`System.Text.Json`, enums como texto). Contiene fecha, `ImageId`, todos los
componentes (con motivo de bloqueo cuando aplica), acciones y avisos.
`SaveToFile`/`LoadFromFile` crean el directorio si falta (pensado para
`<workspace>/output/removal-plan.json`, reutilizando la carpeta `output\` ya
prevista en `InventoryWorkspace` desde la fase 3). No hay todavía una UI de
exportación; es un servicio listo para usarse.

## Resultado de `dotnet build`

`MRS.RemovalPlanning` y los 3 proyectos de tests: **0 errores, 0 advertencias**.
`MRS.WindowsBuilder` compila su XAML y C# sin errores; el build de la solución
completa solo falla por el habitual bloqueo de archivo de la instancia
elevada en ejecución (no es un error de código).

## Resultado de `dotnet test`

```
MRS.ImageEngine.Tests       : 77/77
MRS.ComponentCatalog.Tests  : 41/41
MRS.RemovalPlanning.Tests   : 48/48
```

Total: **166/166**. Sin regresiones en análisis de ISO, inventario real,
montaje/desmontaje, catálogo, protección, búsqueda ni filtros.

## Problemas encontrados

- El prompt describe 4 estados DISM para paquetes (Installed/Superseded/Not
  Present/Unknown), pero `DISM /Get-Packages` solo lista paquetes que existen
  en la imagen: un paquete nunca aparece como "Not Present" en la práctica (eso
  sí ocurre con Capabilities, donde sí se prueba explícitamente). Se documenta
  como decisión: Package cubre Installed/Superseded/Unknown; Feature/Capability
  cubren además NotPresent.
- Regenerar `MRS.WindowsBuilder.exe` sigue requiriendo cerrar la instancia
  elevada en ejecución antes de compilar (igual que en fases anteriores).

## Fuera de alcance (siguiente fase)

Sin implementar: `RemovalEngine` real, `Remove-Package`/`Remove-ProvisionedAppxPackage`/
`Disable-Feature`/`Remove-Capability` reales, eliminación de archivos/servicios/
tareas, modificación de registro, Cleanup, ResetBase, compresión, creación de
ISO, perfiles automáticos, App Packs y PCPI.
