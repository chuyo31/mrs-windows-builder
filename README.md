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
| `MRS.ComponentCatalog` | `net8.0` | Catálogo de componentes/apps eliminables. |
| `MRS.ProfileEngine` | `net8.0` | Definición y aplicación de perfiles (NORMAL/LIGHT/MEDIUM). |
| `MRS.PostInstall` | `net8.0` | Acciones de post-instalación y tweaks. |

`MRS.DismEngine` e `MRS.ImageEngine` ya están implementados para las fases de
análisis e inventario (solo lectura). El resto de bibliotecas siguen siendo
**stubs** y se irán implementando en pasos posteriores.

---

## Estado actual — P4

Inventario **real** de SOLO LECTURA de la imagen seleccionada: montar → leer →
desmontar (ver [`prompts/04-resultado.md`](prompts/04-resultado.md)).

**Funciona:**

- UI dark theme estilo Windows 11 con log `[INFO] [WARN] [ERROR] [DISM]`.
- **Seleccionar ISO** → monta la ISO (solo lectura), localiza
  `sources\install.wim` o `install.esd` y desmonta.
- **Analizar imagen** → `DISM /English /Get-WimInfo`: sistema, versión comercial,
  versión/build, arquitectura, idioma, tipo WIM/ESD y todas las ediciones.
- **Continuar** → pantalla de **INVENTARIO** de la edición elegida:
  `MainWindow → ImageInventoryService → DismRunner → DISM.exe`.
  - Workspace temporal único (`%LOCALAPPDATA%\MRS-Windows-Builder\workspaces\<GUID>\`).
  - Comprueba montajes previos (`/Get-MountedWimInfo`) y recupera solo los
    huérfanos propios.
  - Monta el índice elegido con `DISM /Mount-Wim ... /ReadOnly`, inventaría y
    **desmonta siempre** (`/Unmount-Wim /Discard`), luego verifica con
    `/Get-MountedWimInfo` que no queda ningún montaje.
  - Inventaría: paquetes, características, capacidades, apps provisionadas y
    drivers. Contadores + tabla por categoría (Nombre / Estado / Detalles).
  - Si algo falla: se intenta desmontar, se conserva el workspace de diagnóstico
    y el log indica la fase y el código DISM.
  - No ejecuta ningún `cleanup` (`/StartComponentCleanup`, `/ResetBase`,
    `/Cleanup-Mountpoints`).

**Todavía NO hace:**

- Eliminación de componentes, perfiles Normal/Light/Medium/Ultra, registro offline.
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
  MRS.ISOEngine/          (stub)
  MRS.ComponentCatalog/   (stub)
  MRS.ProfileEngine/      (stub)
  MRS.PostInstall/        (stub)
tests/
  MRS.ImageEngine.Tests/  Tests xUnit (parsers, análisis, inventario, workspace)
prompts/                  Prompts de desarrollo y resultados por paso
app-packs/  catalog/  docs/  profiles/   (reservados, vacíos)
```

---

## Roadmap

- [x] **P1** – Primera interfaz funcional (selección de ISO, perfiles, log).
- [x] **P2** – `MRS.DismEngine` + `MRS.ImageEngine`: análisis real de la imagen (solo lectura) y listado de ediciones.
- [x] **P3** – Inventario de SOLO LECTURA (montar → inspeccionar → desmontar): paquetes, features, capabilities, apps provisionadas y drivers.
- [x] **P4** – Inventario **real** del WIM: `/Mount-Wim` del índice elegido, las 5 categorías vía DISM, `/Unmount-Wim /Discard` garantizado y verificación de que no quedan montajes.
- [ ] **P5** – `MRS.ProfileEngine` + `MRS.ComponentCatalog`: aplicación de perfiles.
- [ ] **P6** – `MRS.PostInstall` + `MRS.ISOEngine`: tweaks, post-instalación y regeneración de la ISO.
