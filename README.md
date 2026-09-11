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
| `MRS.RemovalEngine` | `net8.0` | Ejecuta el RemovalPlan sobre una copia de trabajo (Export/Mount/DISM/Commit-Discard). |
| `MRS.ProfileEngine` | `net8.0` | Definición y aplicación de perfiles (NORMAL/LIGHT/MEDIUM). |
| `MRS.PostInstall` | `net8.0` | Acciones de post-instalación y tweaks. |

`MRS.DismEngine`, `MRS.ImageEngine`, `MRS.ComponentCatalog`,
`MRS.RemovalPlanning` y `MRS.RemovalEngine` ya están implementados: análisis,
inventario real, catálogo, plan de eliminación y ahora también su ejecución
real sobre una copia de trabajo (la ISO original nunca se modifica). El resto
de bibliotecas siguen siendo **stubs**.

---

## Estado actual — P7

Primer `RemovalEngine` real: aplica un `RemovalPlan` confirmado sobre una
**copia de trabajo** de la imagen (la ISO original nunca se toca) (ver
[`prompts/07-resultado.md`](prompts/07-resultado.md)).

**Funciona:**

- Todo lo de P1–P6 (análisis, inventario real, catálogo, `RemovalPlan`,
  pantalla PLAN DE MODIFICACIÓN).
- **Confirmar plan** → aviso obligatorio ("se creará una copia de trabajo...
  el original no será modificado... si falla se descarta") con
  Cancelar/Aplicar cambios; solo tras aceptar se ejecuta algo.
- `WorkingImageFactory`: `DISM /Export-Image` de la edición elegida a un WIM
  de trabajo independiente (el origen solo se lee).
- `RemovalEngine`: monta la copia en **lectura/escritura**, ejecuta las
  acciones del plan en orden fijo (AppX → Features → Capabilities →
  Packages) volviendo a comprobar protección/permiso/compatibilidad antes de
  cada una, y decide con el `ExitCode` de DISM. Primer error → aborta y
  `/Unmount-Wim /Discard`; todo correcto → `/Unmount-Wim /Commit`. Nunca
  `/ResetBase`, `/StartComponentCleanup` ni `/Cleanup-Image`.
- Pantalla **APLICANDO CAMBIOS**: estado, progreso "X / Y", lista con
  ○/⏳/✓/✗/⊘ por componente, cancelación cooperativa.
- Tras un commit, reinventaría la copia de trabajo y compara con
  `RemovalVerifier` (eliminado/todavía presente/cambios inesperados).

**Todavía NO hace:**

- Perfiles Normal/Light/Medium/Ultra/Custom automáticos, `/Remove` de
  features, limpieza de checkpoints/ResetBase, compresión, creación de la
  ISO final, PCPI ni App Packs.

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
  MRS.RemovalEngine/      WorkingImageFactory, RemovalEngine, RemovalVerifier (Export/Mount RW/DISM/Commit-Discard)
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
- [x] **P7** – `MRS.RemovalEngine`: copia de trabajo (Export-Image), montaje ReadWrite, ejecución ordenada con abort/discard transaccional, commit y verificación por reinventario. La ISO original nunca se modifica.
- [ ] **P8** – `MRS.ProfileEngine`: perfiles Normal/Light/Medium/Ultra/Custom (selección automática sobre el catálogo).
- [ ] **P9** – `MRS.PostInstall` + `MRS.ISOEngine`: tweaks, post-instalación y regeneración de la ISO final.
