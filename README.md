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

Las bibliotecas de motor son **stubs** por ahora (`Class1.cs` vacío); se irán
implementando en pasos posteriores.

---

## Estado actual — P1

Primera interfaz funcional de `MRS.WindowsBuilder`
(ver [`prompts/02-resultado.md`](prompts/02-resultado.md)).

**Funciona:**

- UI dark theme estilo Windows 11 (azul eléctrico, bordes redondeados, sombras
  suaves, Segoe UI, ventana ~1200×720).
- Barra superior con logo, título y estado.
- Panel **Origen**: botón "Seleccionar ISO" con `OpenFileDialog` limitado a
  `.iso`, ruta en caja de solo lectura, botón "Analizar imagen" (se habilita al
  elegir ISO) y ComboBox "Edición" (deshabilitado).
- Tarjeta **Información de la imagen**: campos SO, Versión, Build, Arquitectura,
  Idioma y Tipo de imagen, todos en `--`.
- Tarjeta **Perfiles**: NORMAL / LIGHT / MEDIUM con selección visual única.
- Área de **log** tipo consola con mensajes de inicio.
- Botón **Continuar** (inferior derecha), se habilita cuando hay ISO + perfil.

**Todavía NO hace:**

- Análisis real de la imagen — "Analizar imagen" solo escribe en el log
  `[INFO] Función pendiente de conectar con MRS.ImageEngine`.
- Montaje DISM ni modificación de imágenes.
- Generación de la ISO final.

---

## Compilar y ejecutar

Requisitos: **.NET SDK 8.0** en Windows.

```powershell
# Compilar toda la solución
dotnet build MRS-Windows-Builder.sln

# Ejecutar la aplicación
dotnet run --project src/MRS.WindowsBuilder/MRS.WindowsBuilder.csproj
```

---

## Estructura del repositorio

```
MRS-Windows-Builder.sln
src/
  MRS.WindowsBuilder/     App WPF (MainWindow.xaml / .xaml.cs)
  MRS.ISOEngine/          (stub)
  MRS.ImageEngine/        (stub)
  MRS.DismEngine/         (stub)
  MRS.ComponentCatalog/   (stub)
  MRS.ProfileEngine/      (stub)
  MRS.PostInstall/        (stub)
prompts/                  Prompts de desarrollo y resultados por paso
app-packs/  catalog/  docs/  profiles/   (reservados, vacíos)
```

---

## Roadmap

- [x] **P1** – Primera interfaz funcional (selección de ISO, perfiles, log).
- [ ] **P2** – `MRS.ImageEngine`: análisis real de la imagen y listado de ediciones.
- [ ] **P3** – `MRS.ISOEngine` / `MRS.DismEngine`: montaje y modificación.
- [ ] **P4** – `MRS.ProfileEngine` + `MRS.ComponentCatalog`: aplicación de perfiles.
- [ ] **P5** – `MRS.PostInstall`: tweaks y post-instalación.
- [ ] **P6** – Regeneración de la ISO final.
