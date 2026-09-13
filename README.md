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
| `MRS.ISOEngine` | `net8.0` | Workspace de generación, planificación/validación, y aplicación real de LabConfig/autounattend.xml sobre una copia de `boot.wim` (P16). Todavía no genera la ISO final (`oscdimg`). |
| `MRS.ImageEngine` | `net8.0` | Lectura de metadatos de la imagen (WIM/ESD), ediciones. |
| `MRS.DismEngine` | `net8.0` | Operaciones DISM sobre la imagen montada. |
| `MRS.ComponentCatalog` | `net8.0` | Catálogo de componentes: clasificación, protección y dependencias. |
| `MRS.RemovalPlanning` | `net8.0` | Motor de selección: RemovalPlan verificable, sin ejecutar nada. |
| `MRS.RemovalEngine` | `net8.0` | Ejecuta el RemovalPlan sobre una copia de trabajo (Export/Mount/DISM/Commit-Discard). |
| `MRS.ProfileEngine` | `net8.0` | Perfiles de eliminación en JSON (Mínimo/Ligero/Recomendado/Limpio/Personalizado): producen una selección de ComponentId, nunca ejecutan nada. |
| `MRS.InstallationOptions` | `net8.0` | Configuración del instalador/OOBE de la ISO final (cuenta local, OOBE sin conexión, bypass de hardware). Independiente del catálogo de componentes. |
| `MRS.PostInstall` | `net8.0` | Empaqueta .NET Desktop Runtime + PCPI y genera `SetupComplete.cmd` (P18). No genera la ISO ni se integra todavía con ISOEngine. |

`MRS.DismEngine`, `MRS.ImageEngine`, `MRS.ComponentCatalog`,
`MRS.RemovalPlanning`, `MRS.RemovalEngine`, `MRS.ProfileEngine`,
`MRS.InstallationOptions` y `MRS.PostInstall` ya están implementados:
análisis, inventario real, catálogo, plan de eliminación, ejecución real
sobre una copia de trabajo (la ISO original nunca se modifica), perfiles
predefinidos, configuración de instalación/OOBE, y empaquetado de PostInstall
(.NET Desktop Runtime + PCPI vía `SetupComplete.cmd`). `MRS.ISOEngine`
aplica ya de verdad LabConfig/autounattend.xml sobre una copia de `boot.wim`
(P16), pero todavía no genera ninguna ISO final ni copia el paquete de P18
al workspace (ver P15/P16/P18).

---

## Estado actual — P7 (+ correcciones P08/P09/P12/P14, telemetría P10, perfiles P11/P13, instalación P15/P16, validación P17 bloqueada, PostInstall P18)

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

**P12** — corrección: la pantalla inicial todavía mostraba un widget de
perfiles anterior a P11 (`NORMAL`/`LIGHT`/`MEDIUM`, sin conexión real con
`ProfileService`). Sustituido por los 5 perfiles reales, compartiendo la
misma lógica de aplicación (`ApplyProfile`) con la barra de COMPONENTES:
elegir un perfil ahí antes de analizar la ISO queda como preselección y se
aplica en cuanto se genera el catálogo real (ver
[`prompts/12-resultado.md`](prompts/12-resultado.md)).

**P13** — `SecurityOptions` por perfil (ver
[`prompts/13-resultado.md`](prompts/13-resultado.md)): decide si Microsoft
Defender y Windows Update se mantienen protegidos. Mínimo, Ligero y
Recomendado los protegen siempre (`ProfileDefinition.EffectiveSecurityOptions`
ignora cualquier otro valor para esos tres perfiles); Limpio y Personalizado
muestran una sección "OPCIONES AVANZADAS" en COMPONENTES con dos casillas
(ambas empiezan marcadas, con advertencia al desmarcarlas). Cambiar estas
casillas recalcula el catálogo en memoria (mismo `_inventory`, sin DISM) y
el `RemovalPlan` refleja el cambio de protección — pero "ya no protegido"
no equivale a "se elimina": la implementación real de desactivación queda
para una fase posterior e independiente.

**P14** — corrección: "OPCIONES AVANZADAS" solo aparecía en COMPONENTES; al
elegir Limpio/Personalizado en la pantalla inicial no pasaba nada visible
hasta llegar a COMPONENTES. Ahora el mismo panel (mismas casillas,
`_currentSecurityOptions` como única fuente de verdad) aparece también en
la pantalla inicial en cuanto se elige Limpio o Personalizado, y desaparece
al volver a Mínimo/Ligero/Recomendado. La configuración elegida antes de
pulsar "Continuar" se conserva al llegar a COMPONENTES (nunca se reinicia a
los valores por defecto solo por cambiar de pantalla); cambiar a un perfil
seguro sí restaura siempre la protección, y entrar en Limpio/Personalizado
desde un perfil seguro parte de valores seguros por defecto — nunca se
hereda una configuración insegura entre pantallas ni entre perfiles (ver
[`prompts/14-resultado.md`](prompts/14-resultado.md)).

**P15** — `MRS.InstallationOptions` + preparación de `MRS.ISOEngine` (ver
[`prompts/15-resultado.md`](prompts/15-resultado.md)): configuración del
comportamiento del instalador/OOBE de la ISO final (cuenta local, OOBE sin
conexión, y bypass independiente de TPM/Secure Boot/CPU/RAM/almacenamiento),
deliberadamente separada de `SecurityOptions`/`ProfileDefinition`/
`RemovalPlan`. `MRS.ISOEngine` gana un modelo de workspace de generación,
un planificador de modificaciones (`InstallationConfigurationPlanner`) y un
validador previo a generar la ISO (`GenerationWorkspaceValidator`) — pero
**todavía no monta ni modifica ningún `boot.wim` real**: esta fase deja
preparado el motor (modelo + planificación + validación), documentando qué
mecanismos son fiables (LabConfig para TPM/Secure Boot/CPU/RAM,
`autounattend.xml` para cuenta local) y cuáles quedan pendientes de
confirmación empírica (OOBE sin conexión, bypass de almacenamiento). El
bypass de RAM nunca se presenta como "Windows 11 funcionando bien con 2 GB":
solo evita el bloqueo del instalador.

**P16** — implementación real de P15 sobre una **copia** de `boot.wim` (ver
[`prompts/16-resultado.md`](prompts/16-resultado.md)): `OfflineRegistryEditor`
(hive `SYSTEM` offline vía `reg.exe`), `LabConfigApplier` (aplica/retira los
4 bypasses de compatibilidad de forma idempotente), `AutounattendGenerator`
(XML determinista, sin credenciales hardcoded) y `BootWimModifier`/
`InstallationImageService` (ciclo Mount→hive→aplicar→verificar→Commit/Discard,
mismo patrón transaccional que `RemovalEngine`, nunca sobre la ISO original).
El bypass de almacenamiento sigue **sin implementar** (la UI lo deshabilita
explícitamente) y OOBE sin conexión (`BypassNRO`) está **implementado pero
no activado automáticamente**: esta sesión no pudo confirmarlo en la build
26300.9278 real por falta de privilegios elevados (DISM los exige). Validado
con 71 tests (DISM/registro simulados, nunca por texto de salida); la prueba
real sobre un `boot.wim` de la ISO objetivo queda pendiente y documentada
como tal, sin resultados inventados.

**P17** — intento de validar P16 con una ISO real (ver
[`prompts/17-resultado.md`](prompts/17-resultado.md)): **bloqueado por
completo**. DISM exige privilegios elevados incluso para una consulta de
solo lectura (`Get-WimInfo`/`Get-WindowsImage`, no solo `Mount-Wim`), y esta
sesión no está elevada. Siguiendo la instrucción explícita del prompt de no
interpretar ese error como un fallo de implementación, se abortó antes de
tocar nada y **no se modificó ningún código**. Sí se pudo, sin DISM ni
elevación, montar una ISO real de solo lectura (`Mount-DiskImage`) y
registrar el hash SHA256 de su `boot.wim` para una verificación de
integridad futura, y confirmar (con la suite de tests, sin necesitar
elevación) que el bloqueo del bypass de almacenamiento sigue funcionando.
Deja documentados los pasos exactos que faltan para completar la
validación real desde una sesión elevada.

**P18** — `MRS.PostInstall` real: .NET Desktop Runtime 8.0.26 x64 + PCPI
(ver [`prompts/18-resultado.md`](prompts/18-resultado.md)). `PostInstallPackageBuilder`
empaqueta ambos instaladores junto a un `SetupComplete.cmd` generado de
forma determinista, en la estructura `$OEM$\$$\Setup\Scripts\` que Windows
Setup copia automáticamente a `%WinDir%\Setup\Scripts\` y ejecuta una sola
vez, en contexto SYSTEM, antes de que exista ninguna sesión de usuario —
mecanismo elegido explícitamente por no depender de ningún logon/usuario/SID
(a diferencia de FirstLogonCommands/RunOnce), comparado técnicamente en el
resultado. .NET se instala en silencio (`/install /quiet /norestart`, éxito
decidido solo por `ExitCode`) y PCPI solo se lanza si esa instalación
termina con éxito. Ningún instalador real con el nombre/versión exactos
pedidos está disponible en este entorno, así que la prueba con archivos
reales queda pendiente; todo lo demás (empaquetado, orden, determinismo,
ausencia de rutas del desarrollador) está validado con 44 tests. No genera
ninguna ISO ni se integra todavía con `MRS.ISOEngine` (solo se deja la API).

**Todavía NO hace:**

- Ejecutar de verdad la desactivación de Defender/Windows Update (P13 es
  solo configuración + planificación + UI + protección), confirmar P16
  sobre un `boot.wim` real de la build objetivo (bloqueado por falta de
  sesión elevada), activar automáticamente el bypass de OOBE sin conexión,
  implementar el bypass de almacenamiento, `/Remove` de features, limpieza
  de checkpoints/ResetBase, compresión, creación de la ISO final, copiar el
  paquete PostInstall de P18 al workspace de generación, ni App Packs.

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
  MRS.InstallationOptions/ Configuración de instalador/OOBE de la ISO final (cuenta local, OOBE offline, bypass de hardware)
  MRS.ISOEngine/          Workspace de generación, planificador/validación, LabConfigApplier/AutounattendGenerator/BootWimModifier (P16)
  MRS.PostInstall/        PostInstallPackageBuilder: empaqueta .NET Desktop Runtime + PCPI + SetupComplete.cmd (P18)
tests/
  MRS.ImageEngine.Tests/          Tests xUnit (parsers, análisis, inventario, workspace)
  MRS.ComponentCatalog.Tests/     Tests xUnit (clasificación, protección, dependencias, búsqueda/filtros)
  MRS.RemovalPlanning.Tests/      Tests xUnit (selección, protección, dependencias, plan, validación, JSON)
  MRS.RemovalEngine.Tests/        Tests xUnit (ejecución transaccional, workspace, telemetría de progreso)
  MRS.ProfileEngine.Tests/        Tests xUnit (carga de JSON, validación, selección de ComponentId)
  MRS.InstallationOptions.Tests/  Tests xUnit (defaults, independencia de bypasses, serialización)
  MRS.ISOEngine.Tests/            Tests xUnit (planificador, validación, LabConfig/autounattend/boot.wim con DISM/registro simulados)
  MRS.PostInstall.Tests/          Tests xUnit (configuración, empaquetado, ejecución de comandos, orden .NET->PCPI, seguridad)
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
- [x] **P12** – corrección: sustituidos los botones de perfil legados (NORMAL/LIGHT/MEDIUM, anteriores a P11) por los 5 perfiles reales, reutilizando la misma lógica en la pantalla inicial (preselección) y en COMPONENTES.
- [x] **P13** – opciones de seguridad por perfil (`SecurityOptions`: mantener Defender/Windows Update). Mínimo/Ligero/Recomendado los protegen siempre; Limpio/Personalizado permiten decidirlo desde "OPCIONES AVANZADAS". Solo configuración/planificación — sin ejecutar ninguna desactivación real (ver `prompts/13-resultado.md`).
- [x] **P14** – corrección: "OPCIONES AVANZADAS" ahora también aparece en la pantalla inicial al elegir Limpio/Personalizado (antes solo vivía en COMPONENTES); una única fuente de verdad (`_currentSecurityOptions`) sincroniza ambas pantallas, y la configuración se conserva al pulsar "Continuar" (ver `prompts/14-resultado.md`).
- [x] **P15** – `MRS.InstallationOptions` (cuenta local/OOBE sin conexión/bypass de TPM-SecureBoot-CPU-RAM-almacenamiento) + preparación de `MRS.ISOEngine` (workspace de generación, planificador, validación). Investigación de mecanismos documentada; sin ejecución real sobre `boot.wim` todavía (ver `prompts/15-resultado.md`).
- [x] **P16** – implementación real de P15 sobre una copia de `boot.wim`: `LabConfigApplier`, `AutounattendGenerator`, `BootWimModifier`/`InstallationImageService` (ciclo Mount→hive→aplicar→verificar→Commit/Discard). Bypass de almacenamiento sigue sin implementar; OOBE sin conexión implementado pero no confirmado en la build real (sin sesión elevada disponible). Ver `prompts/16-resultado.md`.
- [ ] **P17** – intento de validación real de P16 sobre una ISO real: bloqueado por completo (DISM exige elevación incluso para consultas de solo lectura; esta sesión no está elevada). Sin cambios de código; documentado con transparencia junto con los pasos exactos que faltan para completarla. Ver `prompts/17-resultado.md`.
- [x] **P18** – `MRS.PostInstall`: `PostInstallPackageBuilder` empaqueta .NET Desktop Runtime 8.0.26 x64 + PCPI junto a un `SetupComplete.cmd` determinista (`$OEM$\$$\Setup\Scripts\`, contexto SYSTEM, una sola ejecución). Sin archivos reales exactos disponibles para probar; sin integración con ISOEngine todavía (solo la API). Ver `prompts/18-resultado.md`.
- [ ] **P19** – confirmación real de P16/P17 sobre la ISO objetivo (con sesión elevada), integrar el paquete de P18 en el workspace de ISOEngine, y regeneración de la ISO final (`oscdimg`).
