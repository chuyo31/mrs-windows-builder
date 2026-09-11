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
| `MRS.ProfileEngine` | `net8.0` | Definición y aplicación de perfiles (NORMAL/LIGHT/MEDIUM). |
| `MRS.PostInstall` | `net8.0` | Acciones de post-instalación y tweaks. |

`MRS.DismEngine`, `MRS.ImageEngine` y `MRS.ComponentCatalog` ya están
implementados (análisis, inventario real y catálogo, todo de solo lectura).
El resto de bibliotecas siguen siendo **stubs** y se irán implementando en
pasos posteriores.

---

## Estado actual — P5

Catálogo inteligente de componentes, clasificado y protegido, a partir del
inventario real (ver [`prompts/05-resultado.md`](prompts/05-resultado.md)).

**Funciona:**

- UI dark theme estilo Windows 11 con log `[INFO] [WARN] [ERROR] [DISM]`; `DataGrid`
  y listas con tema oscuro completo (sin el chrome blanco por defecto de WPF).
- **Seleccionar ISO → Analizar imagen → elegir edición → Continuar**: pantalla
  de **INVENTARIO** real (`ImageInventoryService`: monta el índice elegido con
  `/Mount-Wim /ReadOnly`, inventaría paquetes/features/capabilities/apps/drivers
  y **desmonta siempre**, verificando con `/Get-MountedWimInfo`).
- **Continuar** desde el inventario → pantalla **COMPONENTES**:
  `ImageInventory → CatalogClassifier → ProtectionEngine → DependencyResolver`.
  - Clasifica cada paquete/AppX/feature/capability/driver detectado en una
    categoría (Gaming, Application, AI, Communication, Store, Framework,
    Security, WindowsUpdate, Networking, Printing, Media, Language...).
  - Protege con reglas específicas y revisables (Servicing Stack, CBS, SSU,
    LCU, Windows Update, Defender, Microsoft Store, Windows Installer, WinRE,
    Wi-Fi/Ethernet/Bluetooth, USB, audio, impresión, VCLibs, UI.Xaml, .NET,
    paquete base del sistema, idioma, OOBE), cada una con motivo explicado.
  - Primera estructura de dependencias (p. ej. apps AppX → frameworks
    compartidos como VCLibs/UI.Xaml).
  - UI: buscador, filtro por categoría/protección/riesgo, casillas de
    selección (bloqueadas y con 🔒 para los componentes protegidos) y panel de
    detalle. **No modifica el WIM** ni ejecuta DISM: solo clasifica.
  - Reglas externalizadas en `catalog/win11/*.json` (Part 10), con un conjunto
    embebido equivalente usado por defecto.

**Todavía NO hace:**

- Eliminación de componentes, perfiles Normal/Light/Medium/Ultra/Custom, registro offline.
- Limpieza de componentes, compresión, creación de ISO, PCPI ni App Packs.

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
  MRS.ISOEngine/          (stub)
  MRS.ProfileEngine/      (stub)
  MRS.PostInstall/        (stub)
tests/
  MRS.ImageEngine.Tests/      Tests xUnit (parsers, análisis, inventario, workspace)
  MRS.ComponentCatalog.Tests/ Tests xUnit (clasificación, protección, dependencias, búsqueda/filtros)
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
- [ ] **P6** – `MRS.ProfileEngine`: perfiles Normal/Light/Medium/Ultra/Custom y motor de eliminación.
- [ ] **P7** – `MRS.PostInstall` + `MRS.ISOEngine`: tweaks, post-instalación y regeneración de la ISO.
