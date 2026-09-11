# Resultado - P5: Catálogo inteligente, clasificación y protección

## Archivos creados / modificados

### Parte 1 — Corrección visual del inventario
- `src/MRS.WindowsBuilder/MainWindow.xaml` — estilos nuevos `DarkDataGrid` /
  `DarkDataGridHeader` / `DarkDataGridCell` / `DarkDataGridRow` (fondo oscuro,
  encabezados visibles, selección con el azul de la app, hover diferenciado,
  texto claro; se elimina el chrome blanco por defecto de WPF) y
  `DarkListBoxItem` / `CategoryList` para la lista de categorías. Aplicados a
  `InventoryGrid` y a la nueva lista/tabla de componentes.

### Parte 2/3 — MRS.ComponentCatalog (antes stub)
`Models/`: `ComponentCategory`, `ComponentRisk`, `ComponentProtection`,
`RemovalMode` (descriptivo), `ComponentSourceType`, `DependencyType`,
`RemovalProfile` (preparado para P6), `ComponentDependency`,
`ComponentDefinition`, `ComponentCatalogResult` (con contadores y
`DependenciesOf`/`DependentsOf`). No depende de la UI ni de DISM.

### Parte 4 — Identificación
`Rules/ClassificationRule.cs`, `Rules/DefaultCatalogRules.cs` (≈50 reglas por
coincidencia de texto, sin distinguir mayúsculas/minúsculas, cubriendo los
ejemplos del prompt: Xbox/Solitaire → Gaming, Clipchamp → Application,
Copilot → AI, Bing* → Application, Outlook/Teams/Skype/PhoneLink →
Communication, Store → Store, VCLibs/.NET Native/UI.Xaml/WindowsAppRuntime →
Framework, Defender/SecurityHealth → Security, ServicingStack/CBS/SSU/LCU →
WindowsUpdate, Wi-Fi/Bluetooth/Ethernet → Networking, Print/Spooler/XPS →
Printing, MediaFoundation/MediaPlayer/codec → Media, etc.).
`Classification/CatalogClassifier.cs`: `ImageInventory → ComponentDefinition[]`;
un paquete/AppX/feature/capability/driver por elemento real del inventario.

### Parte 5/6/8 — Protección
`Rules/ProtectionRule.cs`, reglas específicas en `DefaultCatalogRules`
(25 reglas revisables, NO "todo Microsoft-Windows = protegido"): Servicing
Stack, CBS, SSU, LCU, Windows Update, Defender, Security Health, Microsoft
Store (base + compras), Windows Installer, WinRE, Wi-Fi, Ethernet, Bluetooth,
USB, Audio, impresión, .NET/NetFx, Windows App Runtime, VCLibs, UI.Xaml,
paquete base del sistema, paquete de idioma, OOBE.
`Classification/ProtectionEngine.cs`: aplica las reglas DESPUÉS de clasificar;
solo puede reforzar protección/riesgo (nunca debilitar), añade
`ProtectionReason`. `Classification/CategoryDefaults.cs`: riesgo/protección de
referencia por categoría (conservador: ninguna categoría es "Protected" solo
por existir).

### Parte 7 — Dependencias
`Classification/DependencyResolver.cs`: primera estructura de dependencias
(`ComponentDependency` con `SourceComponentId`/`TargetComponentId`/
`DependencyType`). Cada componente AppX no-framework obtiene una arista
`Framework` hacia cada framework compartido detectado en el inventario
(ej. Microsoft.Paint → VCLibs, Microsoft.Paint → UI.Xaml), para que el
catálogo nunca pueda "concluir" que un framework compartido no se usa.

### Parte 9/10 — Catálogo dinámico + almacenamiento externo
`ComponentCatalogService.cs`: fachada `ImageInventory → ComponentCatalogResult`
(clasifica → protege → resuelve dependencias). El catálogo sale siempre de lo
detectado; una ISO distinta produce un catálogo distinto (con inventario
vacío, catálogo vacío — probado). `Rules/JsonCatalogRuleLoader.cs`: lee
`components.json`/`protection-rules.json` de un directorio si existen.
`catalog/win11/components.json`, `catalog/win11/protection-rules.json`
(equivalentes a `DefaultCatalogRules`, usados por defecto sin depender de
rutas de archivo), `catalog/win10/`, `catalog/shared/` (reservados),
`catalog/README.md`.

### Parte 13/14 — Búsqueda y filtros
`Query/CatalogQuery.cs` (Id/Name/DisplayName/categoría/tags, sin distinguir
mayúsculas/minúsculas) y `Query/CatalogFilterKind.cs` (Todos, Protegidos,
Removibles, Opcionales, Críticos, Aplicaciones, Features, Capabilities,
Frameworks, Gaming, Communication, AI, Telemetry).

### Parte 11/12 — UI del catálogo
`MainWindow.xaml` / `MainWindow.xaml.cs`: nueva pantalla **COMPONENTES**
(overlay tras "Continuar" en el inventario): buscador, ComboBox de filtro,
`DataGrid` con casilla de selección + Nombre + Categoría + Estado
(🟢 Removible / 🟡 Opcional / 🔵 Recomendado / 🔒 Protegido / ⚪ Desconocido);
las filas protegidas muestran la casilla bloqueada (`IsEnabled=False`) con el
candado 🔒. Panel de detalle: Categoría, Estado (Installed/Superseded),
Riesgo, Protección, Origen, Descripción, Motivo de protección, Dependencias,
Utilizado por. Botones **Volver** (a Inventario) y **Continuar** (registra la
selección en el log; no modifica nada).

### tests/MRS.ComponentCatalog.Tests (nuevo, xUnit)
`FakeInventory.cs` + `CatalogClassifierTests.cs`, `ProtectionEngineTests.cs`,
`ComponentCatalogServiceTests.cs`, `JsonCatalogRuleLoaderTests.cs`: 41 tests.
Ninguno usa una ISO real.

## Arquitectura del catálogo

```
ImageInventory
    ↓
CatalogClassifier   (clasifica: categoría + riesgo/protección de referencia)
    ↓
ProtectionEngine    (refuerza protección/riesgo con reglas específicas)
    ↓
DependencyResolver  (aristas hacia frameworks compartidos)
    ↓
ComponentCatalogResult   ← ComponentCatalogService.BuildCatalog(inventory)
```

El catálogo **no ejecuta DISM** ni toca el WIM; solo transforma el
`ImageInventory` ya obtenido en la fase 3/4.

## Reglas de protección implementadas

Servicing Stack · CBS · SSU · LCU · Windows Update · Defender · Security
Health · Microsoft Store (base y compras) · Windows Installer · WinRE ·
Wi-Fi · Ethernet · Bluetooth · USB · Audio · impresión (Print) · .NET/NetFx ·
Windows App Runtime · VCLibs · UI.Xaml · paquete base del sistema
(Foundation-Package) · paquete de idioma (LanguageFeatures-Basic) · OOBE.
Cada una con `Reason` explicando por qué está protegido.

## Categorías implementadas

System, Security, WindowsUpdate, Store, Application, Gaming, Communication,
AI, Telemetry, Media, Networking, Printing, Accessibility, Development,
Language, Driver, Feature, Capability, Framework, Unknown.

## Dependencias implementadas

`ComponentDependency(SourceComponentId, TargetComponentId, DependencyType)`.
Regla actual (conservadora, ampliable): toda app AppX no-framework depende
(`Framework`) de cada framework compartido presente en el inventario. No se
resuelve todavía el grafo completo por manifiesto; la estructura y las
consultas (`DependenciesOf` / `DependentsOf`) ya están preparadas.

## Cambios de UI

Ver Parte 1 y Parte 11/12 arriba: `DataGrid` de inventario con tema oscuro
completo, lista de categorías con selección en azul, y pantalla COMPONENTES
nueva con búsqueda, filtro, selección con candado para protegidos y panel de
detalle.

## Resultado de `dotnet build`

Todas las bibliotecas (`MRS.DismEngine`, `MRS.ImageEngine`,
`MRS.ComponentCatalog`) y ambos proyectos de tests: **0 errores, 0
advertencias**. El proyecto `MRS.WindowsBuilder` compila su XAML y C# sin
errores; el único fallo del build de la solución completa es el habitual
bloqueo de archivo (`MSB3021/MSB3027`) por tener una instancia **elevada** de
la app en ejecución, que impide copiar el `.exe`. No es un error de código.

## Resultado de `dotnet test`

```
MRS.ImageEngine.Tests      : Correctas! Superado: 77, Total: 77
MRS.ComponentCatalog.Tests : Correctas! Superado: 41, Total: 41
```

Total: **118/118**.

## Problemas encontrados

- Igual que en fases anteriores, regenerar `MRS.WindowsBuilder.exe` requiere
  cerrar la instancia elevada de la app antes de compilar.
- No existe un desglose de dependencias AppX por manifiesto en DISM; la
  resolución de dependencias de esta fase es deliberadamente conservadora
  (Parte 7 del prompt no lo exige todavía).
- El `TargetType="Development"` para WSL/OpenSSH puede solaparse con
  `Security`/`Networking` en imágenes reales; al ser reglas por texto y no
  exclusivas, la primera coincidencia en la lista decide (documentado y
  cubierto por tests de orden en `DismWimInfoParser`/clasificador).
