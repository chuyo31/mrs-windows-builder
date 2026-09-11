# MRS Windows Builder

Herramienta de escritorio para **construir imágenes personalizadas de Windows 11**
a partir de una ISO oficial: análisis de la imagen, selección de edición,
aplicación de perfiles de optimización y generación de una ISO final.

> ⚠️ **Estado: en desarrollo temprano.** Actualmente solo existe la primera
> interfaz funcional (paso P1). El motor de análisis y el montaje/modificación
> de imágenes (DISM) todavía **no** están implementados.

---

## Objetivo del proyecto

Dada una ISO de Windows 11, la aplicación permitirá:

1. Seleccionar la ISO de origen y analizar su contenido (SO, versión, build,
   arquitectura, idioma, tipo de imagen WIM/ESD, ediciones disponibles).
2. Elegir la edición a procesar.
3. Aplicar un **perfil** de configuración:
   - **NORMAL** – cambios mínimos, sistema completo.
   - **LIGHT** – eliminación agresiva de componentes y apps.
   - **MEDIUM** – punto intermedio.
4. Ejecutar la transformación sobre la imagen (montaje DISM, quita/añade
   componentes, aplica tweaks y post-instalación).
5. Regenerar una ISO booteable con el resultado.

---

## Arquitectura

Solución .NET 8 (`MRS-Windows-Builder.sln`) dividida en la aplicación de UI y
varias bibliotecas de motor:

| Proyecto | Framework | Rol |
|---|---|---|
| `MRS.WindowsBuilder` | `net8.0-windows` (WPF) | Aplicación de escritorio y UI. |
| `MRS.ISOEngine` | `net8.0` | Montaje/extracción y regeneración de la ISO. |
| `MRS.ImageEngine` | `net8.0` | Lectura de metadatos de la imagen (WIM/ESD), ediciones. |
| `MRS.DismEngine` | `net8.0` | Operaciones DISM sobre la imagen montada. |
| `MRS.ComponentCatalog` | `net8.0` | Catálogo de componentes: clasificación, protección y dependencias. |
| `MRS.RemovalPlanning` | `net8.0` | Motor de selección: RemovalPlan verificable, sin ejecutar nada. |
| `MRS.ProfileEngine` | `net8.0` | Definición y aplicación de perfiles (NORMAL/LIGHT/MEDIUM). |
| `MRS.PostInstall` | `net8.0` | Acciones de post-instalación y tweaks. |

`MRS.DismEngine`, `MRS.ImageEngine`, `MRS.ComponentCatalog` y
`MRS.RemovalPlanning` ya están implementados (análisis, inventario real,
catálogo y plan de eliminación, todo sin modificar el WIM). El resto de
bibliotecas siguen siendo **stubs** y se irán implementando en pasos
posteriores.

---

## Estado actual — P6

Motor de selección: transforma lo marcado en la pantalla COMPONENTES en un
`RemovalPlan` verificable, sin tocar el WIM (ver
[`prompts/06-resultado.md`](prompts/06-resultado.md)).

**Funciona:**

- UI dark theme estilo Windows 11 con log `[INFO] [WARN] [ERROR] [DISM]`; `DataGrid`
  y listas con tema oscuro completo (sin el chrome blanco por defecto de WPF).
- **Seleccionar ISO → Analizar imagen → elegir edición → Continuar**: pantalla
  de **INVENTARIO** real (monta con `/Mount-Wim /ReadOnly`, inventaría y
  **desmonta siempre**, verificando con `/Get-MountedWimInfo`).
- **Continuar** → pantalla **COMPONENTES**: clasificación + protección +
  dependencias (fase 5), con buscador, filtros y casillas bloqueadas (🔒) en
  los componentes protegidos.
- Cada casilla recalcula en vivo un **RemovalPlan**
  (`Selección → Catálogo → Protección → Dependencias → RemovalPlan`):
  resumen "Seleccionados / Permitidos / Bloqueados / ⚠ Advertencias" y botón
  **Ver plan** → pantalla **PLAN DE MODIFICACIÓN** (tarjetas ✓ ELIMINAR /
  🔒 BLOQUEADO con motivo y riesgo). **Confirmar plan** solo lo guarda en
  memoria; **nunca modifica el WIM**.
- Protección absoluta (bloqueo + motivo), estados DISM
  (Installed/Superseded/NotPresent/Unknown: solo Installed genera acción),
  dependientes que impiden eliminar lo que otros componentes que se quedan
  necesitan, y nunca eliminación en cascada silenciosa.
- `RemovalPlanValidator` (segunda verificación independiente) y
  `RemovalPlanSerializer` (JSON con versión de formato, listo para
  `output/removal-plan.json`).
- Garantía de compilación: `MRS.RemovalPlanning` no puede referenciar
  `MRS.DismEngine` (ni transitivamente); no ejecuta DISM.

**Todavía NO hace:**

- `RemovalEngine` real ni ninguna eliminación/deshabilitación real
  (Remove-Package, Remove-ProvisionedAppxPackage, Disable-Feature,
  Remove-Capability, archivos, registro, servicios, tareas).
- Perfiles Normal/Light/Medium/Ultra/Custom automáticos, limpieza, compresión,
  creación de ISO, PCPI ni App Packs.

---

## Compilar y ejecutar

Requisitos: **.NET SDK 8.0** en Windows.

```powershell
# Compilar toda la solución
dotnet build MRS-Windows-Builder.sln

# Ejecutar los tests
dotnet test MRS-Windows-Builder.sln

# Ejecutar la aplicación
dotnet run --project src/MRS.WindowsBuilder/MRS.WindowsBuilder.csproj
```

---

## Estructura del repositorio

```
MRS-Windows-Builder.sln
src/
  MRS.WindowsBuilder/     App WPF (MainWindow.xaml / .xaml.cs)
  MRS.DismEngine/         Procesos externos, logging e invocación de DISM
  MRS.ImageEngine/        Modelos, parsers de DISM, montaje de ISO, ImageService, ImageInventoryService
  MRS.ComponentCatalog/   Clasificación, protección, dependencias, búsqueda/filtros del catálogo
  MRS.RemovalPlanning/    RemovalPlanBuilder/Validator/Serializer (sin referenciar MRS.DismEngine)
  MRS.ISOEngine/          (stub)
  MRS.ProfileEngine/      (stub)
  MRS.PostInstall/        (stub)
tests/
  MRS.ImageEngine.Tests/       Tests xUnit (parsers, análisis, inventario, workspace)
  MRS.ComponentCatalog.Tests/  Tests xUnit (clasificación, protección, dependencias, búsqueda/filtros)
  MRS.RemovalPlanning.Tests/   Tests xUnit (selección, protección, dependencias, plan, validación, JSON)
prompts/                  Prompts de desarrollo y resultados por paso
catalog/                  Reglas de clasificación/protección externas (win11/win10/shared)
app-packs/  docs/  profiles/   (reservados, vacíos)
```

---

## Roadmap

- [x] **P1** – Primera interfaz funcional (selección de ISO, perfiles, log).
- [x] **P2** – `MRS.DismEngine` + `MRS.ImageEngine`: análisis real de la imagen (solo lectura) y listado de ediciones.
- [x] **P3** – Inventario de SOLO LECTURA (montar → inspeccionar → desmontar): paquetes, features, capabilities, apps provisionadas y drivers.
- [x] **P4** – Inventario **real** del WIM: `/Mount-Wim` del índice elegido, las 5 categorías vía DISM, `/Unmount-Wim /Discard` garantizado y verificación de que no quedan montajes.
- [x] **P5** – `MRS.ComponentCatalog`: clasificación por categorías, protección de componentes críticos con motivo, dependencias, búsqueda/filtros y pantalla COMPONENTES. Sin eliminar nada todavía.
- [x] **P6** – `MRS.RemovalPlanning`: RemovalPlan verificable (protección, estados, dependencias/dependientes, validador, serialización JSON) y pantalla PLAN DE MODIFICACIÓN. Sigue sin modificar el WIM.
- [ ] **P7** – `RemovalEngine` real + `MRS.ProfileEngine`: ejecución de las acciones del plan y perfiles Normal/Light/Medium/Ultra/Custom.
- [ ] **P8** – `MRS.PostInstall` + `MRS.ISOEngine`: tweaks, post-instalación y regeneración de la ISO.
