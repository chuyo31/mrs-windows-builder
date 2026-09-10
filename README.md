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

`MRS.DismEngine` e `MRS.ImageEngine` ya están implementados para la fase de
análisis (solo lectura). El resto de bibliotecas siguen siendo **stubs** y se
irán implementando en pasos posteriores.

---

## Estado actual — P2

Análisis **real** de la ISO, 100% lectura
(ver [`prompts/03-resultado.md`](prompts/03-resultado.md)).

**Funciona:**

- UI dark theme estilo Windows 11 (azul eléctrico, bordes redondeados, sombras
  suaves, Segoe UI, ventana ~1200×720) con log `[INFO] [WARN] [ERROR] [DISM]`.
- **Seleccionar ISO**: solo `.iso`, comprueba que existe, monta la ISO como
  unidad de solo lectura, localiza `sources\install.wim` o `install.esd` y
  desmonta. Error claro si no hay imagen.
- **Analizar imagen**: `MainWindow → ImageService → DismRunner → DISM.exe`.
  Ejecuta `DISM /English /Get-WimInfo` (listado + un `/Index:N` por edición) y
  obtiene sistema, versión comercial, versión/build, arquitectura, idioma, tipo
  WIM/ESD y todas las ediciones (índice + nombre + descripción). Los valores
  salen de DISM, no están quemados.
- Tarjeta **Información de la imagen** rellenada tras el análisis; `ComboBox`
  "Edición" poblado; al elegir una edición se habilita **Continuar**.
- Manejo de errores de DISM sin ocultar el código de salida.
- Ejecutor de procesos externos robusto y sistema de logging reutilizable.
- Modelos y parser preparados para ISO/WIM/ESD, Windows 10/11 y x86/x64/ARM64.

**Todavía NO hace:**

- Montaje del WIM, eliminación de componentes, perfiles, registro offline.
- Compresión ni generación de la ISO final.

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
  MRS.ImageEngine/        Modelos, parser de DISM, montaje de ISO, ImageService
  MRS.ISOEngine/          (stub)
  MRS.ComponentCatalog/   (stub)
  MRS.ProfileEngine/      (stub)
  MRS.PostInstall/        (stub)
tests/
  MRS.ImageEngine.Tests/  Tests xUnit (parser, formato, versión, ImageService)
prompts/                  Prompts de desarrollo y resultados por paso
app-packs/  catalog/  docs/  profiles/   (reservados, vacíos)
```

---

## Roadmap

- [x] **P1** – Primera interfaz funcional (selección de ISO, perfiles, log).
- [x] **P2** – `MRS.DismEngine` + `MRS.ImageEngine`: análisis real de la imagen (solo lectura) y listado de ediciones.
- [ ] **P3** – `MRS.ISOEngine` / `MRS.DismEngine`: montaje y modificación.
- [ ] **P4** – `MRS.ProfileEngine` + `MRS.ComponentCatalog`: aplicación de perfiles.
- [ ] **P5** – `MRS.PostInstall`: tweaks y post-instalación.
- [ ] **P6** – Regeneración de la ISO final.
