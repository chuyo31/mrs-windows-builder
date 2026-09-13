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
| `MRS.ProfileEngine` | `net8.0` | Perfiles de eliminación en JSON (Mínimo/Ligero/Recomendado/Limpio/Personalizado): producen una selección de ComponentId, nunca ejecutan nada. |
| `MRS.PostInstall` | `net8.0` | Acciones de post-instalación y tweaks. |

`MRS.DismEngine`, `MRS.ImageEngine`, `MRS.ComponentCatalog`,
`MRS.RemovalPlanning`, `MRS.RemovalEngine` y `MRS.ProfileEngine` ya están
implementados: análisis, inventario real, catálogo, plan de eliminación,
ejecución real sobre una copia de trabajo (la ISO original nunca se
modifica) y perfiles predefinidos. El resto de bibliotecas siguen siendo
**stubs**.

---

## Estado actual — P7 (+ correcciones P08/P09, telemetría P10, perfiles P11)

Primer `RemovalEngine` real, **probado con éxito sobre Windows 11 26H2 Pro**
(Clipchamp eliminado, commit y desmontaje confirmados): aplica un
`RemovalPlan` confirmado sobre una **copia de trabajo** de la imagen (la ISO
original nunca se toca) (ver [`prompts/07-resultado.md`](prompts/07-resultado.md),
la corrección de finalización de UI en [`prompts/08-resultado.md`](prompts/08-resultado.md)
y la auditoría del ciclo de vida workspace/mount en
[`prompts/09-resultado.md`](prompts/09-resultado.md)).

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
  `RemovalVerifier` (eliminado/todavía presente/cambios inesperados) — este
  reinventario corre **después** de desbloquear la pantalla, nunca antes
  (corrección P08: ver más abajo).
- Limpieza idempotente: un `ExitCode != 0` en Commit/Discard no se trata como
  fallo si `Get-MountedWimInfo` confirma que el montaje ya no existe.

**Corrección P08** — tras la prueba real, "APLICANDO CAMBIOS" quedaba
bloqueada después del aviso de éxito: "Cerrar" y "Cancelar" no respondían
porque el desbloqueo de la UI esperaba a un reinventario adicional (fuera de
la operación transaccional del motor). Solucionado: la UI se desbloquea en
cuanto el `RemovalEngine` termina (Commit + Unmount + su verificación
interna incluidos), antes del aviso y del reinventario.

**Corrección P09** — `Get-MountedWimInfo` seguía mostrando, tras una
eliminación exitosa, una entrada `Status: Invalid` que combinaba el
`Mount Dir` del workspace de la verificación posterior con el `Image File`
del workspace del `RemovalEngine`. Causa: el reinventario de verificación
crea su propio workspace de solo lectura y lo borraba del disco sin
confirmar primero (vía `Get-MountedWimInfo`) que DISM ya no lo consideraba
montado. Solucionado: el workspace de un inventario solo se borra cuando el
desmontaje queda confirmado explícitamente; si no, se conserva para
diagnóstico. Se añadió además logging estructurado
(OperationId/WorkspaceId/SourceWimPath/WorkingWimPath/MountDir) y una guarda
en `RemovalEngine` que aborta si una imagen de trabajo mezclara rutas de dos
workspaces distintos.

**P10** — telemetría de progreso y terminal de ejecución (ver
[`prompts/10-resultado.md`](prompts/10-resultado.md)). `WorkingImageFactory`
y `RemovalEngine` reportan ahora un `ProgressInfo` (Stage/Percent/Message/
Level/Timestamp) a través de un `IProgress<ProgressInfo>` opcional, sin
ninguna dependencia de WPF (probado con un `IProgress<T>` de test, sin UI).
La pantalla de ejecución (renombrada "CREANDO IMAGEN") muestra ahora barra
de progreso, etapa y porcentaje actuales, y un terminal en tiempo real
(`RichTextBox` seleccionable/copiable) con una línea por evento, coloreada
por nivel. No se modificó el ciclo Mount→Execute→Verify→Commit/Discard ni
el desbloqueo idempotente de P08/P09.

**P11** — `MRS.ProfileEngine` (ver
[`prompts/11-resultado.md`](prompts/11-resultado.md)): perfiles de
eliminación definidos en `profiles/*.json` (Mínimo/Ligero/Recomendado/Limpio/
Personalizado). Un perfil solo produce una lista de ComponentId candidatos;
la protección y las dependencias reales siguen decidiéndose siempre en
`MRS.ComponentCatalog`/`MRS.RemovalPlanning`. En la pantalla COMPONENTES,
aplicar un perfil solo marca/desmarca casillas (nunca ejecuta nada), y un
componente protegido no se marca aunque el perfil lo pidiera. Los 4 perfiles
predefinidos se han dejado estructuralmente válidos pero con `componentIds`
vacío: el catálogo real depende del inventario DISM de una ISO concreta y no
existe un catálogo estático de referencia en el repositorio para rellenarlos
sin inventar IDs (documentado en el resultado de P11). "Personalizado" no
impone lista fija: representa la selección manual del usuario.

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
  MRS.RemovalEngine/      WorkingImageFactory, RemovalEngine, RemovalVerifier (Export/Mount RW/DISM/Commit-Discard), telemetría ProgressInfo
  MRS.ProfileEngine/      Perfiles JSON (Mínimo/Ligero/Recomendado/Limpio/Personalizado): solo producen una selección de ComponentId
  MRS.ISOEngine/          (stub)
  MRS.PostInstall/        (stub)
tests/
  MRS.ImageEngine.Tests/        Tests xUnit (parsers, análisis, inventario, workspace)
  MRS.ComponentCatalog.Tests/   Tests xUnit (clasificación, protección, dependencias, búsqueda/filtros)
  MRS.RemovalPlanning.Tests/    Tests xUnit (selección, protección, dependencias, plan, validación, JSON)
  MRS.RemovalEngine.Tests/      Tests xUnit (ejecución transaccional, workspace, telemetría de progreso)
  MRS.ProfileEngine.Tests/      Tests xUnit (carga de JSON, validación, selección de ComponentId)
prompts/                  Prompts de desarrollo y resultados por paso
catalog/                  Reglas de clasificación/protección externas (win11/win10/shared)
profiles/                 Perfiles de eliminación en JSON (minimal/light/recommended/clean/custom)
app-packs/  docs/         (reservados, vacíos)
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
- [x] **P08** – corrección: la UI de ejecución se desbloquea de forma idempotente en cuanto el motor termina, sin esperar al reinventario posterior.
- [x] **P09** – corrección: auditoría y saneado del ciclo de vida workspace/mount (no se borra un workspace sin confirmar el desmontaje).
- [x] **P10** – telemetría de progreso (`ProgressInfo`/`IProgress<T>`, sin WPF) + pantalla "CREANDO IMAGEN" con barra de progreso y terminal en tiempo real.
- [x] **P11** – `MRS.ProfileEngine`: perfiles Mínimo/Ligero/Recomendado/Limpio/Personalizado definidos en JSON; solo producen una selección de ComponentId, la protección real sigue en ProtectionEngine/RemovalPlanning. Perfiles predefinidos pendientes de ComponentId reales (requieren un inventario de referencia; ver `prompts/11-resultado.md`).
- [ ] **P12** – `MRS.PostInstall` + `MRS.ISOEngine`: tweaks, post-instalación y regeneración de la ISO final.
