# Resultado - P15: OOBE local, cuenta Microsoft y bypass de requisitos

## Resumen

Se crea `MRS.InstallationOptions` (configuración: cuenta local, OOBE sin
conexión, y los 5 bypasses de compatibilidad de hardware) y se prepara
`MRS.ISOEngine` (modelo de workspace de generación, planificador de
modificaciones, validador previo a generar la ISO). Es deliberadamente
**independiente** de `SecurityOptions` (Defender/Windows Update, P13),
`ProfileDefinition`, `ComponentDefinition` y `RemovalPlan`: no elimina
componentes, no modifica el catálogo, no toca `RemovalEngine`.

Esta fase **no aplica todavía ningún cambio real** sobre un `boot.wim`: deja
preparado el modelo, la planificación y la validación, con la investigación
de mecanismos documentada abajo, tal como pide el prompt ("esta fase debe
dejar preparado el motor de instalación/compatibilidad", sección 13 excluye
explícitamente `oscdimg`, compresión final, PostInstall y PCPI).

## Investigación previa (sección 3 del prompt)

**Aviso de método**: esta investigación se basa en el estado del arte
ampliamente documentado por la comunidad de Windows Setup/WinPE (el mismo
tipo de técnicas que usan herramientas conocidas como Rufus para instalar
Windows 11 en equipos no soportados), no en la inspección directa de un
`boot.wim` real de la build 26300.9278 dentro de esta sesión (no se dispone
de una ISO de esa build para desmontar y comprobar). Se recomienda
**verificar empíricamente contra la build objetivo antes de depender de
esto en producción** — ver "Prueba recomendada en VM" al final.

### Dónde vive cada cosa

- `boot.wim` (WinPE + Setup) y `install.wim` (la imagen real de Windows) están
  ambos en `sources\` dentro de la ISO. `boot.wim` tiene normalmente 2
  índices: 1 = Windows Setup, 2 = Herramientas de recuperación de Windows.
  Setup.exe se ejecuta arrancando desde `boot.wim` (índice 1), en WinPE.
- Las comprobaciones de compatibilidad (TPM/Secure Boot/CPU/RAM/almacenamiento)
  las realiza **Setup.exe dentro de WinPE**, antes de copiar ningún archivo y
  antes de que exista ningún registro de la instalación destino. Esto
  significa que cualquier bypass tiene que aplicarse **antes** de ese punto:
  o bien en el propio `boot.wim` (registro offline de WinPE), o mediante un
  archivo de respuesta (`autounattend.xml`) que WinPE lee al arrancar, o
  mediante una combinación de ambos.
- El componente que hace la comprobación es `appraiserres.dll` (Hardware
  Support App / "PC Health Check" embebido), invocado por `setuphost.exe`/
  `Windows Setup Compatibility` dentro de WinPE.
- OOBE (cuenta Microsoft, conexión, teclado, etc.) se ejecuta **después**,
  ya arrancado desde el `install.wim` final, orquestado por
  `Windows-Setup\OOBE` (componente `Microsoft-Windows-Shell-Setup`).

### Mecanismos evaluados

| Objetivo | Mecanismo | Dónde se aplicaría | Fiabilidad estimada en 26H2 |
|---|---|---|---|
| Cuenta local (sin MSA) | `autounattend.xml` (`Microsoft-Windows-Shell-Setup\OOBE`/`UserAccounts\LocalAccounts`) | Raíz de la ISO/USB, leído por Setup | **Alta** — mecanismo soportado oficialmente por Windows Setup para instalación desatendida; no depende de ninguna pantalla concreta de OOBE. |
| OOBE sin conexión | DWORD `BypassNRO=1` en `HKLM\SYSTEM\Setup\OOBE` (registro offline de `boot.wim`) — el mismo valor que pone `%WinDir%\System32\Oobe\BypassNRO.cmd` en caliente | Hive `SYSTEM` de `boot.wim` | **Media** — el mecanismo en sí (la clave de registro) es el mismo que usa Microsoft; lo que ha cambiado entre versiones es si el script `BypassNRO.cmd` sigue presente/accesible desde la pantalla de red de OOBE. Al aplicarse por registro offline (no dependiendo del script en caliente) es más robusto, pero **debe confirmarse en la build objetivo** antes de darlo por garantizado. |
| Bypass TPM 2.0 | DWORD `BypassTPMCheck=1` en `HKLM\SYSTEM\Setup\LabConfig` | Hive `SYSTEM` de `boot.wim` | **Alta** — clave ampliamente documentada y estable a través de varias versiones de Windows 11. |
| Bypass Secure Boot | DWORD `BypassSecureBootCheck=1` en el mismo `LabConfig` | Hive `SYSTEM` de `boot.wim` | **Alta**, mismo mecanismo que TPM. |
| Bypass CPU | DWORD `BypassCPUCheck=1` en `LabConfig` | Hive `SYSTEM` de `boot.wim` | **Alta**. |
| Bypass RAM | DWORD `BypassRAMCheck=1` en `LabConfig` | Hive `SYSTEM` de `boot.wim` | **Alta** para omitir la comprobación de Setup — ver la distinción obligatoria más abajo (sección "Qué significa realmente el bypass de RAM"). |
| Bypass almacenamiento | Sin una clave `LabConfig` equivalente confirmada de forma tan consistente como las anteriores | Hive `SYSTEM` de `boot.wim` (sin confirmar) | **Baja/sin confirmar** — ver limitación explícita abajo. |

### Por qué "en boot.wim" y no "en install.wim"

Las comprobaciones de compatibilidad ocurren **antes** de que exista
ninguna instalación: se hacen desde WinPE, leyendo `boot.wim`. Modificar
`install.wim` (que es lo que hace hoy `MRS.RemovalEngine`) no tendría ningún
efecto sobre estas comprobaciones — llegarían a ejecutarse igual, antes de
que `install.wim` entre en juego. Por eso el mecanismo correcto es
**offline-editar el registro de `boot.wim`** (montar el WIM, cargar su hive
`SYSTEM` con `reg load`, añadir los valores, `reg unload`, desmontar/`commit`)
— el mismo patrón transaccional que `RemovalEngine` ya usa para
`install.wim` (Export → Mount RW → modificar → Commit/Discard), pero
aplicado a un WIM distinto y a un contenido distinto (claves de registro, no
paquetes/features).

## Qué significa realmente el bypass de RAM (sección 6 del prompt)

Esto se documenta explícitamente porque el prompt lo exige:

- **"Bypass del requisito de instalación de RAM"** significa: el DWORD
  `BypassRAMCheck=1` hace que **Setup.exe no bloquee la instalación** por
  no cumplir el mínimo de RAM (4 GB oficial). Setup arrancará y completará
  la instalación en una máquina con, por ejemplo, 2 GB de RAM.
- **"Windows 11 funcionando correctamente con 2 GB"** es una afirmación
  completamente distinta, y **NO se promete en esta fase ni en ninguna**:
  Windows 11 con 2 GB de RAM es extremadamente limitado (el propio Setup, y
  después el sistema instalado, dependerán mucho del archivo de paginación;
  aplicaciones modernas, Windows Update, Windows Defender y el propio
  Explorador pueden volverse muy lentos o inestables). El bypass elimina el
  **bloqueo del instalador**, no las limitaciones reales de ejecutar Windows
  11 con esa cantidad de memoria.
- Existe una única comprobación de RAM relevante para este proyecto: la que
  hace Setup en WinPE antes de instalar (la que `BypassRAMCheck` neutraliza).
  No se ha encontrado evidencia de una segunda comprobación de RAM
  independiente dentro de OOBE en 26H2 (a diferencia de TPM/Secure Boot, que
  sí pueden volver a comprobarse en distintos puntos según la versión) — pero,
  siguiendo el mismo principio de no prometer de más, esto debería
  confirmarse en la build objetivo real antes de depender de ello.

## Limitación explícita: bypass de almacenamiento

A diferencia de TPM/Secure Boot/CPU/RAM, **no se ha encontrado una clave
`LabConfig` oficial-equivalente, ampliamente documentada y estable, para el
requisito de almacenamiento mínimo (64 GB)** en Windows 11 26H2. Es posible
que:

- la comprobación de almacenamiento sea menos estricta en instalación
  offline que en la ruta de Windows Update (que es donde más se documenta
  el bloqueo por espacio insuficiente), o
- requiera una combinación distinta (por ejemplo, particionar el disco de
  forma específica vía `autounattend.xml`/`DiskConfiguration`) en vez de un
  simple valor de registro.

**No se ha implementado como un hecho fiable.** `InstallationOptions.BypassStorage`
existe como opción independiente (tal como pide la sección 5: "cada bypass
debe tener una opción independiente", "no un único BypassEverything"), y
`InstallationConfigurationPlanner` genera su acción con el `Mechanism`
marcado explícitamente como "sin confirmar" — pero no se afirma en ningún
sitio que este bypass concreto funcione de forma fiable. Queda documentado
como pendiente de investigación empírica adicional, en vez de implementarse
como un hack sin verificar.

## Nuevos modelos

**`MRS.InstallationOptions`** (nuevo proyecto, sin ninguna referencia a otro
proyecto — mismo patrón de independencia que `MRS.ProfileEngine`):
- `Models/InstallationOptions.cs` — record con los 7 booleanos
  (`AllowLocalAccount`, `AllowOfflineOobe`, `BypassTpm`, `BypassSecureBoot`,
  `BypassCpu`, `BypassRam`, `BypassStorage`), todos `= true` por defecto
  (`InstallationOptions.Default`). Cada campo es independiente: desactivar
  uno nunca cambia otro (garantizado por construcción — son propiedades
  booleanas simples sin ninguna lógica cruzada).
- `Serialization/InstallationOptionsSerializer.cs` — `ToJson`/`FromJson`/
  `FromJsonOrDefault`. Un JSON vacío, parcial o `null` resuelve siempre a
  valores seguros (nunca un estado parcial o inválido); uno malformado
  lanza `JsonException` en `FromJson` o se resuelve a `Default` en
  `FromJsonOrDefault`.

**`MRS.ISOEngine`** (antes stub; ahora referencia a `MRS.InstallationOptions`):
- `Models/GenerationWorkspace.cs` — rutas de una copia de trabajo de la ISO
  en generación (`SourceIsoPath`, `WorkspacePath`, `BootWimPath`,
  `InstallWimPath`, `Index`, `Architecture`). Solo modela rutas; la
  extracción/copiado real de una ISO a este layout no se implementa en esta
  fase (esa es la "generación definitiva" que la sección 13 excluye).
- `Models/InstallationActionCategory.cs` — enum `{ Install, Compat }` (prefijos de log `[INSTALL]`/`[COMPAT]`).
- `Models/InstallationConfigurationAction.cs` — una modificación concreta
  (`Category`, `Description`, `Mechanism`, `TargetArtifact`) con
  `ToLogLine()` que produce exactamente el formato del prompt
  (`"[INSTALL] Local account enabled"`).
- `Models/WorkspaceValidationResult.cs` — `{ IsValid, Errors }`.
- `Configuration/InstallationConfigurationPlanner.cs` — función pura:
  `InstallationOptions` → lista ordenada de `InstallationConfigurationAction`
  (Install primero, Compat después; una opción deshabilitada simplemente no
  genera ninguna acción, nunca una "acción vacía o inválida").
- `Configuration/GenerationWorkspaceValidator.cs` — la fase de validación
  de la sección 10: comprueba que existan `boot.wim`/`install.wim`, que el
  índice y la arquitectura sean válidos, y que el workspace **nunca** sea la
  misma ruta que la ISO original. Si algo falta, devuelve `IsValid = false`
  con la lista de errores — el llamador debe ABORTAR, nunca generar una ISO
  aparentemente válida pero incompleta.

## Qué se puede implementar de forma fiable (y qué no, todavía)

**Fiable, listo para una fase de ejecución futura:**
- Cuenta local vía `autounattend.xml` (mecanismo soportado oficialmente).
- Bypass de TPM/Secure Boot/CPU/RAM vía `LabConfig` en el registro offline
  de `boot.wim`.
- La validación de workspace (sección 10) y la planificación de acciones
  (sección 9) — ambas implementadas y probadas en esta fase.

**Pendiente de confirmación empírica antes de considerarse fiable:**
- OOBE sin conexión (`BypassNRO`): el mecanismo de registro es sólido, pero
  su interacción exacta con la pantalla de red de OOBE puede variar entre
  builds de 24H2/25H2/26H2 — Microsoft ha movido/retirado el script
  `BypassNRO.cmd` en algunas versiones.
- Bypass de almacenamiento: sin una clave `LabConfig` equivalente
  confirmada; implementado como opción independiente pero con su mecanismo
  marcado explícitamente como no verificado.

**No implementado en esta fase (fuera de alcance, sección 13):**
- Montar `boot.wim` de verdad, cargar su hive de registro, escribir los
  valores y confirmar el commit (el "motor" queda preparado a nivel de plan
  + validación, no de ejecución DISM real).
- Generar/copiar un `autounattend.xml` real dentro de un workspace.
- `oscdimg`, compresión final, limpieza final de la ISO, PostInstall .NET,
  PCPI, o cualquier eliminación adicional de componentes.

## UI

La aplicación no tiene todavía una pantalla de "generación de ISO" (esa
fase — `oscdimg`, PostInstall, PCPI — está explícitamente fuera de alcance
de P15). Se añadió la sección pedida en la pantalla **PLAN DE MODIFICACIÓN**
(la más próxima a "configuración antes de generar", y no es la pantalla de
"selección de componentes" — esa es COMPONENTES, que no se ha tocado): una
columna nueva con "OPCIONES DE INSTALACIÓN" (cuenta local, OOBE sin
conexión) y "COMPATIBILIDAD DE HARDWARE" (los 5 bypasses), todas las
casillas empezando marcadas, con los dos textos informativos exactos del
prompt (uno general, uno específico de RAM). Cambiar una casilla solo
actualiza `_installationOptions` en memoria — nunca ejecuta nada sobre
Windows, sobre la imagen ni sobre ningún WIM real; no hay ninguna llamada a
DISM en este camino.

Esta decisión de ubicación se documenta explícitamente por si se prefiere
otra: en cuanto exista una pantalla real de "generación de ISO" en una fase
posterior, este bloque puede trasladarse allí sin cambiar el modelo
(`InstallationOptions` es independiente de dónde vivan sus controles).

## Archivos modificados/creados

- `src/MRS.InstallationOptions/` (nuevo proyecto): `MRS.InstallationOptions.csproj`,
  `Models/InstallationOptions.cs`, `Serialization/InstallationOptionsSerializer.cs`.
- `src/MRS.ISOEngine/`: `MRS.ISOEngine.csproj` (referencia a `MRS.InstallationOptions`;
  eliminado `Class1.cs`), `Models/GenerationWorkspace.cs`,
  `Models/InstallationActionCategory.cs`, `Models/InstallationConfigurationAction.cs`,
  `Models/WorkspaceValidationResult.cs`, `Configuration/InstallationConfigurationPlanner.cs`,
  `Configuration/GenerationWorkspaceValidator.cs`.
- `tests/MRS.InstallationOptions.Tests/` (nuevo): `MRS.InstallationOptions.Tests.csproj`,
  `InstallationOptionsTests.cs` (17 tests).
- `tests/MRS.ISOEngine.Tests/` (nuevo): `MRS.ISOEngine.Tests.csproj`,
  `InstallationConfigurationPlannerTests.cs`, `GenerationWorkspaceValidatorTests.cs` (16 tests).
- `src/MRS.WindowsBuilder/MRS.WindowsBuilder.csproj` — referencia a `MRS.InstallationOptions`.
- `src/MRS.WindowsBuilder/MainWindow.xaml` — sección "OPCIONES DE INSTALACIÓN"/
  "COMPATIBILIDAD DE HARDWARE" en la pantalla PLAN DE MODIFICACIÓN.
- `src/MRS.WindowsBuilder/MainWindow.xaml.cs` — campo `_installationOptions`,
  `RefreshInstallationOptionsCheckboxes`, `InstallationOption_Changed`.
- `MRS-Windows-Builder.sln` — añadidos los 4 proyectos nuevos.

No se ha tocado `MRS.RemovalEngine`, `MRS.RemovalPlanning`, `MRS.ComponentCatalog`,
`MRS.ProfileEngine`, el ciclo Mount/Execute/Verify/Commit/Discard, ni la
telemetría de progreso de P10.

## Decisión arquitectónica: colisión namespace/tipo (nuevo caso, mismo patrón)

`MRS.InstallationOptions` es a la vez el namespace raíz del proyecto y el
nombre de su tipo principal (`MRS.InstallationOptions.Models.InstallationOptions`)
— el mismo caso que `MRS.RemovalEngine`/`MRS.ProfileEngine` en fases
anteriores. La fase anterior lo resolvía con un alias `using X = Y;` donde
`X` coincidía con el nombre simple del tipo; en esta fase se descubrió que
**eso no basta aquí**: dentro de un archivo cuyo namespace está anidado bajo
`MRS.InstallationOptions` (los tests, `MRS.InstallationOptions.Tests`), un
alias con el mismo nombre simple que el tipo sigue perdiendo frente al
namespace en posiciones de tipo (parámetros/retorno) y de atributo
(`[InlineData(nameof(...))]`), aunque funcione en el resto de posiciones
(expresiones normales). La solución aplicada — y ahora documentada para
fases futuras — es usar un alias con un **nombre distinto** al del tipo
(`InstallationOptionsModel`), que no es ambiguo en ninguna posición.

## Tests

**`MRS.InstallationOptions.Tests`** (17): valores por defecto (todos
habilitados), constructor sin parámetros equivalente a `Default`, cada uno
de los 7 bypasses es independiente (`[Theory]`, uno por propiedad),
combinar varias opciones desactivadas no afecta al resto, round-trip de
serialización, JSON vacío/`null`/parcial resuelve siempre a valores
seguros, JSON malformado nunca lanza vía `FromJsonOrDefault`, serialización
determinista, y — de forma estructural, no en tiempo de ejecución — el
propio proyecto de test no referencia `MRS.ComponentCatalog` ni
`MRS.ProfileEngine`, así que "no se confunden `SecurityOptions` con
`InstallationOptions`" está garantizado en tiempo de compilación (si algún
día se intentase mezclarlos desde aquí, el build fallaría antes de llegar a
ejecutar ningún test).

**`MRS.ISOEngine.Tests`** (16):
- `InstallationConfigurationPlannerTests`: orden exacto de las 7 líneas de
  log con `InstallationOptions.Default`, desactivar una opción solo quita su
  propia acción, desactivar todo produce un plan vacío (nunca inválido),
  Install siempre antes que Compat, planificación determinista, toda acción
  declara mecanismo y artefacto objetivo, y una comprobación de que
  `Plan()` es una función pura de una sola entrada (sin acceso a disco).
- `GenerationWorkspaceValidatorTests`: workspace completo válido, cada
  archivo/ruta faltante (ISO origen, workspace, boot.wim, install.wim)
  falla la validación con un mensaje específico, índice inválido,
  arquitectura no soportada, workspace apuntando a la misma ruta que la ISO
  original, y que varios problemas se reportan todos juntos (no solo el
  primero).

Se ejecutaron también todos los tests existentes.

## Resultado de `dotnet test`

```
MRS.InstallationOptions.Tests : 17/17  (nuevo)
MRS.ISOEngine.Tests           : 16/16  (nuevo)
MRS.ProfileEngine.Tests       : 21/21
MRS.ImageEngine.Tests         : 78/78
MRS.ComponentCatalog.Tests    : 59/59
MRS.RemovalPlanning.Tests     : 48/48
MRS.RemovalEngine.Tests       : 48/48
```

Total: **287/287**. Sin regresiones.

## Resultado de `dotnet build`

`dotnet build MRS-Windows-Builder.sln`: **0 errores, 0 advertencias** en
toda la solución, incluidos los 4 proyectos nuevos y `MRS.WindowsBuilder`.

## Prueba recomendada en VM

No se ha podido comprobar nada de esto contra un `boot.wim` real dentro de
esta sesión (no hay una ISO de Windows 11 26H2 26300.9278 disponible para
inspeccionar ni una VM para instalar). Antes de depender de estos mecanismos
en un uso real, se recomienda, en una VM aislada (nunca en hardware de
producción):

1. Extraer `boot.wim` de una ISO real de la build objetivo, montarlo
   offline (`Mount-WindowsImage`), cargar su hive `SYSTEM`
   (`reg load HKLM\OfflineSystem <mount>\Windows\System32\config\SYSTEM`),
   añadir manualmente los DWORD de `LabConfig` (`BypassTPMCheck`,
   `BypassSecureBootCheck`, `BypassCPUCheck`, `BypassRAMCheck`) y
   `Setup\OOBE\BypassNRO`, descargar el hive y confirmar el commit.
2. Regenerar la ISO (fuera de alcance de P15) y arrancarla en una VM
   configurada deliberadamente por debajo de los requisitos (sin TPM
   virtual, sin Secure Boot, 2 GB de RAM) para confirmar que Setup no
   bloquea la instalación.
3. Confirmar en la pantalla de red de OOBE que aparece la opción de
   continuar sin conexión, y completar OOBE con una cuenta local usando el
   `autounattend.xml`.
4. Documentar el resultado real observado en esa build concreta (puede
   diferir de lo aquí investigado si Microsoft cambia algo en un parche).
5. Para almacenamiento: probar explícitamente si el bypass tiene algún
   efecto real, dado que no se confirmó una clave equivalente fiable.

## Commit recomendado

`git commit` con el resumen "P15: OOBE local, cuenta Microsoft y bypass de
requisitos — MRS.InstallationOptions + preparación de MRS.ISOEngine (sin
ejecución real sobre boot.wim todavía)" y `git push` a `main`.
