# Resultado - P18: PostInstall — .NET 8.0.26 + PCPI

## Resumen

Se implementa `MRS.PostInstall`: un empaquetador que produce, a partir de
`PostInstallConfiguration` + los instaladores reales (.NET Desktop Runtime
8.0.26 x64 y PCPI-Retro-Minimals-Portable-0.0.5.exe), una estructura
`$OEM$\$$\Setup\Scripts\` lista para que una fase posterior de `MRS.ISOEngine`
la copie al workspace de generación. **No genera ninguna ISO** y no ejecuta
ningún instalador sobre el equipo de desarrollo — solo empaqueta.

## Mecanismo PostInstall elegido: `SetupComplete.cmd`

**Decisión**: usar `%WinDir%\Setup\Scripts\SetupComplete.cmd`, distribuido
dentro de la ISO como `$OEM$\$$\Setup\Scripts\SetupComplete.cmd` (Windows
Setup copia automáticamente el contenido de `$OEM$\$$\...` a `%WinDir%\...`
durante la fase `specialize`).

**Comparación técnica** (sección 9 del prompt):

| Mecanismo | Contexto de ejecución | Depende de un usuario/logon | Ejecución garantizada una sola vez | Compatible con ISO/desatendida sin red |
|---|---|---|---|---|
| **SetupComplete.cmd** (`$OEM$\$$\Setup\Scripts`) | SYSTEM, al final de Setup, antes de OOBE/primer logon | **No** | Sí (Setup lo invoca una vez, al terminar la instalación) | Sí — es justamente el mecanismo pensado para esto |
| FirstLogonCommands (`autounattend.xml`) | El primer usuario que inicia sesión | **Sí** — depende de qué cuenta inicia sesión primero, y de que efectivamente inicie sesión | Solo si el primer logon ocurre como se espera; frágil si se cancela/retrasa | Parcial — pensado para ejecutar comandos ligeros de personalización, no instaladores pesados |
| RunOnce (registro `HKLM`/`HKCU`) | Depende de dónde se escriba (`HKLM` = próximo arranque; `HKCU` = próximo logon de ESE usuario) | Sí si se usa `HKCU` (necesitaría el SID/perfil del usuario, prohibido por la sección 10) | Sí, pero puede competir con otras entradas `RunOnce` de terceros | Parcial — más frágil si hay UAC/interacción de por medio |

`SetupComplete.cmd` es el único que cumple **todos** los requisitos de la
sección 9 a la vez sin comprometer la sección 10 (nunca necesita saber qué
usuario/SID/perfil existe: se ejecuta en contexto SYSTEM, en una ruta fija
`%WinDir%\Setup\Scripts\`, antes de que el usuario cree o inicie sesión en
ninguna cuenta). No se eligió "porque es conocido de versiones antiguas": es
el único de los tres que no depende de ningún evento de logon, lo que
descarta directamente el riesgo de "usuario hardcodeado/SID/ruta
`C:\Users\...`" que la sección 10 prohíbe explícitamente.

**Nota importante sobre `$OEM$\$$\Setup\Scripts` y `SetupComplete.cmd`**: no
son dos mecanismos alternativos — son la misma técnica. `$OEM$\$$\Setup\Scripts\`
es la ubicación **de origen** dentro de la distribución (la ISO); Windows
Setup copia su contenido a `%WinDir%\Setup\Scripts\` durante `specialize`, y
`SetupComplete.cmd` es justamente el archivo que, si existe ahí, Setup
ejecuta automáticamente al terminar. El prompt los listaba como opciones
separadas; se documenta aquí la aclaración porque afecta a las secciones 11
y 12.

**Limitación documentada, no ocultada**: `SetupComplete.cmd` se ejecuta en
contexto **SYSTEM**, en la sesión 0, **antes** de que exista ningún
escritorio interactivo. Esto es exactamente el contexto correcto para una
instalación silenciosa de .NET Desktop Runtime (`/install /quiet /norestart`,
sin necesidad de ninguna UI). Para PCPI no se ha podido confirmar si necesita
un escritorio interactivo (no hay documentación del propio PCPI disponible
en este proyecto) — se lanza igualmente desde `SetupComplete.cmd` por ahora
como decisión razonada, pero queda marcado como **PENDIENTE DE PRUEBA EN VM**
si PCPI resultase necesitar sesión interactiva; en ese caso habría que
moverlo a un mecanismo de post-logon (con las salvedades de la tabla de
arriba) en una fase posterior, nunca en esta.

## Estructura generada

```
<output>/$OEM$/$$/Setup/Scripts/
    SetupComplete.cmd
    dotnet/
        windowsdesktop-runtime-8.0.26-win-x64.exe
    pcpi/
        PCPI-Retro-Minimals-Portable-0.0.5.exe
```

`PostInstallPackageBuilder.Build(...)` produce exactamente esto a partir de
`PostInstallConfiguration` + `PostInstallSourceFiles` (rutas reales de los
dos instaladores en el equipo que construye el paquete — nunca hardcodeadas).
El nombre de cada instalador es configurable (`DotNetInstallerFileName`/
`PcpiFileName`); el contenido de `SetupComplete.cmd` es **determinista**
(`SetupCompleteScriptGenerator.Generate`, probado con un test de
determinismo) y solo usa rutas relativas a su propia ubicación (`%~dp0`) —
nunca `C:\Users\...`, `Desktop`, `Downloads` ni ningún dato del equipo de
desarrollo (probado explícitamente).

## Cómo se ejecutará después de Windows (flujo completo)

```
Windows Setup termina (specialize)
    ↓ Setup copia $OEM$\$$\Setup\Scripts\* a %WinDir%\Setup\Scripts\
Setup detecta SetupComplete.cmd y lo ejecuta (contexto SYSTEM, una vez)
    ↓
"%SCRIPT_DIR%dotnet\windowsdesktop-runtime-8.0.26-win-x64.exe" /install /quiet /norestart
    ↓ (solo si ExitCode == 0; nunca "el proceso terminó" como criterio)
"%SCRIPT_DIR%pcpi\PCPI-Retro-Minimals-Portable-0.0.5.exe"
    ↓
Windows arranca a OOBE (cuenta local vía autounattend.xml, P16) con .NET y PCPI ya instalados
```

Cada paso registra en `%~dp0PostInstall.log` con las líneas exactas del
prompt (`[POSTINSTALL] Installing .NET`, `.NET ExitCode: X`,
`[POSTINSTALL] .NET installation completed`/`failed`, `[POSTINSTALL] Launching PCPI`,
`PCPI ExitCode: X`). Las líneas de "tiempo de construcción" (`Preparing package`,
`Runtime: ...`, `PCPI: ...`, `Validation: OK`, `Package prepared successfully`)
las emite `PostInstallPackageBuilder` mediante `IAppLogger`, **no** el script
— son dos momentos distintos (construir el paquete vs. ejecutarlo después),
y mezclarlos en el mismo log habría sido confuso además de redundante.

## .NET Desktop Runtime 8.0.26 x64

- Instalación **silenciosa**: `/install /quiet /norestart` — una única
  constante, en `PostInstallCommands.DotNetRuntimeInstall`, referenciada
  tanto por el generador del script como por cualquier ejecución directa
  futura (`ICommandExecutor`) — nunca repetida en más de un sitio (probado).
- Éxito decidido **solo por `ExitCode`** (nunca por "el proceso terminó";
  `CommandExecutor`/`SetupComplete.cmd` comprueban explícitamente el código).
- Si falla: no se lanza PCPI (`if %DOTNET_EXITCODE% NEQ 0 (...) goto :EOF`),
  y se registra `[POSTINSTALL] .NET installation failed` + `ExitCode: X`
  antes de terminar.
- **No se incluye el instalador real en el repositorio**: no existe un
  `windowsdesktop-runtime-8.0.26-win-x64.exe` dentro de este proyecto (ver
  "Prueba con archivos reales" más abajo). `PostInstallPackageValidator`
  aborta con un mensaje claro si no se le proporciona una ruta real.

## PCPI-Retro-Minimals-Portable-0.0.5.exe

- Se ejecuta **una sola vez**, **solo después** de confirmar que .NET
  terminó con `ExitCode == 0` (nunca antes — verificado con un test que
  comprueba el orden textual dentro del script generado).
- **No se interpreta automáticamente un cierre de PCPI como error**: al ser
  un ejecutable portable de terceros sin contrato de código de salida
  documentado, `PostInstallCommands.Pcpi` deja `ExpectedExitCodes = { 0 }`
  como valor razonable por defecto, pero esto es una suposición explícita,
  no una certeza — documentada aquí tal como pide la sección 7, en vez de
  asumir en silencio que "0 = éxito" es correcto para PCPI.
- Ejecución preferida (sección 8): **una vez, después de completar el
  runtime** — implementado exactamente así; no hay ningún bucle ni
  relanzamiento.
- No se usa ninguna ruta absoluta del equipo de desarrollo: PCPI se lanza
  desde su propio directorio relativo dentro del paquete
  (`%SCRIPT_DIR%pcpi\...`), igual que el runtime.

## Modelos y servicios creados

- `Models/PostInstallConfiguration.cs` — `Enabled`, `DotNetRuntimeVersion`,
  `Architecture`, `DotNetInstallerFileName`, `PcpiFileName`,
  `RunPcpiAfterRuntime`. Solo nombres/versiones/flags — nunca una ruta real.
- `Models/PostInstallSourceFiles.cs` — rutas reales de los dos instaladores
  (parámetro de entrada en tiempo de ejecución, nunca hardcodeado).
- `Models/CommandExecutionSpec.cs`/`CommandExecutionResult.cs` — modelo
  genérico de ejecución (executable/arguments/working directory/timeout/
  expected exit codes), sección 5 del prompt.
- `Execution/PostInstallCommands.cs` — único lugar donde viven las líneas de
  comandos reales ("/install /quiet /norestart"; lanzamiento de PCPI sin
  argumentos).
- `Execution/ICommandExecutor.cs`/`CommandExecutor.cs` — ejecuta un
  `CommandExecutionSpec` reutilizando `IProcessRunner` (la misma
  infraestructura que ya usan `DismRunner`/`OfflineRegistryEditor`).
- `Configuration/PostInstallConfigurationValidator.cs` — coherencia de la
  configuración en sí (versión bien formada, arquitectura x64, nombres de
  archivo sin rutas, sin nombres duplicados).
- `Packaging/PostInstallPackageValidator.cs` — añade la existencia real de
  los dos instaladores.
- `Packaging/PostInstallPackageBuilder.cs` — el `PostInstallPackageBuilder`
  de la sección 12: valida, copia, genera `SetupComplete.cmd`, verifica el
  resultado, reporta progreso.
- `Scripts/SetupCompleteScriptGenerator.cs` — genera el `.cmd` de forma
  determinista.
- `Models/PostInstallProgressInfo.cs`/`PostInstallProgressLevel.cs` — mismo
  patrón Stage/Percent/Message/Level/Timestamp que P10/P16, como tipo propio
  (independencia entre motores, mismo principio que `SecurityOptions`/
  `InstallationOptions`).
- `Exceptions/PostInstallException.cs`.

`MRS.PostInstall` referencia únicamente `MRS.DismEngine` (para
`IProcessRunner`/`IAppLogger`, la infraestructura de proceso/logging ya
compartida en todo el proyecto) — cero referencias a `MRS.WindowsBuilder`,
`MRS.RemovalEngine`, `MRS.ComponentCatalog` ni `MRS.ProfileEngine`.

## Único cambio fuera de `MRS.PostInstall`: `IProcessRunner.RunAsync` gana `workingDirectory`

La sección 5 del prompt pide explícitamente que el modelo de ejecución
represente un "working directory" — pero `IProcessRunner`/`ProcessRunner`
(en `MRS.DismEngine`, ya usado por `DismRunner`/`IsoMounter`/
`OfflineRegistryEditor`) no lo soportaba. Se añadió como **parámetro
opcional final** (`string? workingDirectory = null`, por defecto hereda el
directorio del proceso actual — comportamiento idéntico al de antes de este
cambio para todo el código existente): la alternativa habría sido duplicar
la lógica de captura de stdout/stderr/timeout/cancelación/kill-tree ya
probada dentro de `MRS.PostInstall`, lo que viola directamente el principio
de reutilización que el propio proyecto sigue en el resto de motores
(P16 reutilizó `IProcessRunner` para `OfflineRegistryEditor` en vez de
reinventar la ejecución de procesos). Es un cambio aditivo y compatible con
binarios/código anteriores; se ejecutó toda la suite existente para
confirmarlo (ningún test de P07-P17 cambió su comportamiento).

## Integración con ISOEngine (sección 14 — solo la API, sin implementarla)

`PostInstallPackageResult.OemRootPath` es exactamente la ruta que una fase
futura de `MRS.ISOEngine` necesitaría copiar a
`<workspace>\sources\$OEM$\` para que quede dentro de la ISO final. No se ha
implementado esa copia todavía (fuera de alcance de P18); `MRS.PostInstall`
no referencia `MRS.ISOEngine` ni al revés — la integración queda para
cuando `ISOEngine` construya el flujo completo de generación.

## Validación previa (sección 13)

`PostInstallPackageValidator.Validate` comprueba, antes de copiar nada:
configuración coherente (versión con formato `X.Y.Z`, arquitectura `x64`,
nombres de archivo sin rutas ni duplicados) + existencia real de ambos
instaladores. Si falta `.NET` o `PCPI`, o el directorio de salida ya existe
con contenido inesperado, `PostInstallPackageBuilder.Build` **aborta antes
de crear nada** (verificado con tests: ningún directorio de salida se crea
si la validación falla).

## Prueba con archivos reales (sección 19)

**No disponibles con los nombres/versiones exactos pedidos por el prompt
dentro de este repositorio.** Se comprobó explícitamente: no existe
`windowsdesktop-runtime-8.0.26-win-x64.exe` ni
`PCPI-Retro-Minimals-Portable-0.0.5.exe` en ningún lugar de este proyecto.
Sí se encontraron, en el escritorio del usuario (fuera del repositorio, y
por tanto fuera de lo que "en el proyecto" pide la sección 19),
`windowsdesktop-runtime-8.0.30-win-x64.exe` (versión **8.0.30**, no 8.0.26)
y un `PCPI.exe` compilado de un proyecto `PCPI-Minimal` (nombre distinto de
`PCPI-Retro-Minimals-Portable-0.0.5.exe`) — ninguno coincide exactamente con
lo que pide el prompt, así que no se han usado: sustituir por una versión
distinta sin decirlo habría sido presentar una prueba con archivos reales
que en realidad no lo son. Todos los tests de "package validation"/
"generación" usan archivos de marcador de posición (texto plano, mismo
patrón que el resto del proyecto), nunca ejecutados.

## Seguridad

Ningún archivo de este cambio contiene contraseñas, tokens, credenciales ni
datos personales. No se descarga nada de Internet en ningún punto (los
instaladores son siempre una entrada — `PostInstallSourceFiles` — que el
llamador debe proporcionar). Se verificó explícitamente, con tests, que ni
el script generado ni el paquete producido contienen `C:\Users\`,
`B3RASCASA`, `\Desktop\` ni `\Downloads\` en ningún punto.

## Tests

**Configuration** (`PostInstallConfigurationValidatorTests`, 8): valores por
defecto (coinciden con el objetivo del prompt), configuración por defecto
válida, deshabilitar tolera cualquier valor, versión `8.0.26`/`9.0.0`
aceptadas, versiones malformadas rechazadas, solo `x64` aceptado, nombres de
archivo con aspecto de ruta rechazados, nombres duplicados rechazados.

**Package validation** (`PostInstallPackageValidatorTests`, 6): ambos
instaladores presentes es válido, .NET ausente, PCPI ausente, ambos
ausentes (los dos errores reportados a la vez), deshabilitado nunca exige
archivos, configuración incoherente + archivos ausentes se reportan juntos.

**Command execution** (`CommandExecutorTests`, 7): `ExitCode 0` es éxito,
`ExitCode` distinto nunca es éxito, `ExpectedExitCodes` puede incluir más de
un valor (p. ej. 3010 "reinicio pendiente"), argumentos/ejecutable/directorio
de trabajo se pasan exactamente, un timeout nunca es éxito aunque el
`ExitCode` sea 0, la cancelación se propaga al `IProcessRunner` subyacente,
"terminar" no es éxito si el `ExitCode` no es el esperado.

**Orden** (`SetupCompleteScriptGeneratorTests`, 8, incluye
`PostInstallCommandsTests`, 3): .NET siempre antes que el lanzamiento de
PCPI (nunca al revés — verificado por posición textual), PCPI nunca se
lanza antes de comprobar el `ExitCode` de .NET, generación determinista,
`RunPcpiAfterRuntime = false` no menciona PCPI en absoluto, solo rutas
relativas a `%~dp0`, sin usuario/ruta del desarrollador, líneas de log
documentadas presentes, y las banderas de instalación silenciosa provienen
de `PostInstallCommands` (no de una segunda copia hardcodeada).

**Generación** (`PostInstallPackageBuilderTests`, 7): el paquete generado
contiene runtime + PCPI + `SetupComplete.cmd` en la estructura documentada,
deshabilitado no produce ningún paquete, .NET/PCPI ausentes abortan sin
crear ningún directorio de salida, un directorio de salida ya existente y no
vacío se rechaza (`PostInstallException`), ninguna ruta del equipo de
desarrollo aparece dentro del paquete generado, el progreso se reporta desde
la validación hasta el 100%.

Se ejecutaron también todos los tests existentes (P07-P17): sin
regresiones.

## Resultado de `dotnet test`

```
MRS.PostInstall.Tests          : 44/44  (nuevo)
MRS.InstallationOptions.Tests  : 17/17
MRS.ISOEngine.Tests            : 71/71
MRS.ProfileEngine.Tests        : 21/21
MRS.ImageEngine.Tests          : 78/78
MRS.ComponentCatalog.Tests     : 59/59
MRS.RemovalPlanning.Tests      : 48/48
MRS.RemovalEngine.Tests        : 48/48
```

Total: **386/386**. Sin regresiones.

## Resultado de `dotnet build`

`dotnet build MRS-Windows-Builder.sln`: **0 errores, 0 advertencias** en
toda la solución.

## Distinción IMPLEMENTADO / VALIDADO / PENDIENTE DE PRUEBA EN VM

| Elemento | Estado |
|---|---|
| Modelo `PostInstallConfiguration`/validación | **IMPLEMENTADO** y **VALIDADO** (14 tests, sin ejecutar nada real). |
| `PostInstallPackageBuilder` (genera `$OEM$\$$\Setup\Scripts\...`) | **IMPLEMENTADO** y **VALIDADO** con archivos de marcador de posición; **PENDIENTE DE PRUEBA EN VM** con los instaladores reales exactos (no disponibles en este entorno, ver sección 19). |
| `SetupComplete.cmd` generado (contenido, orden, rutas relativas) | **IMPLEMENTADO** y **VALIDADO** a nivel de texto (16 tests); **PENDIENTE DE PRUEBA EN VM** su ejecución real por Windows Setup (requiere instalar Windows desde una ISO que incluya este paquete — fuera de alcance de P18, que explícitamente no genera la ISO). |
| Instalación silenciosa de .NET (`/install /quiet /norestart`) | **IMPLEMENTADO**; **PENDIENTE DE PRUEBA EN VM** confirmar el comportamiento real del instalador 8.0.26 x64 (no se dispone del archivo exacto). |
| Lanzamiento de PCPI tras .NET | **IMPLEMENTADO**; **PENDIENTE DE PRUEBA EN VM** si PCPI necesita un escritorio interactivo (ver limitación documentada arriba). |
| Integración con ISOEngine (copiar `$OEM$` al workspace) | **NO IMPLEMENTADO** (fuera de alcance de P18 — solo se deja la API, `PostInstallPackageResult.OemRootPath`). |

## Limitaciones

- Ningún instalador real con el nombre/versión exactos del prompt está
  disponible en este entorno; la validación de "archivos reales" (sección
  19) no se pudo completar tal como se pedía.
- No se ha podido confirmar si PCPI necesita una sesión interactiva — se
  lanza desde `SetupComplete.cmd` (contexto SYSTEM) como decisión razonada,
  marcada explícitamente como pendiente de confirmación.
- La integración con `ISOEngine` (copiar `$OEM$` al workspace de generación
  y, más adelante, a la ISO final) no existe todavía — es la API mínima
  (`PostInstallPackageResult.OemRootPath`) para cuando llegue esa fase.

## Archivos modificados/creados

**Nuevo (`MRS.PostInstall`)**: `MRS.PostInstall.csproj` (referencia a
`MRS.DismEngine`; eliminado `Class1.cs`); `Models/PostInstallConfiguration.cs`,
`PostInstallSourceFiles.cs`, `PostInstallValidationResult.cs`,
`PostInstallPackageResult.cs`, `PostInstallProgressLevel.cs`,
`PostInstallProgressInfo.cs`, `CommandExecutionSpec.cs`,
`CommandExecutionResult.cs`; `Execution/PostInstallCommands.cs`,
`ICommandExecutor.cs`, `CommandExecutor.cs`;
`Configuration/PostInstallConfigurationValidator.cs`;
`Packaging/PostInstallPackageValidator.cs`, `PostInstallPackageBuilder.cs`;
`Scripts/SetupCompleteScriptGenerator.cs`;
`Exceptions/PostInstallException.cs`.

**Modificado (fuera de `MRS.PostInstall`, aditivo)**:
`src/MRS.DismEngine/Processes/IProcessRunner.cs`,
`src/MRS.DismEngine/Processes/ProcessRunner.cs` (parámetro opcional
`workingDirectory`); `tests/MRS.ISOEngine.Tests/Fakes/FakeProcessRunner.cs`
y `tests/MRS.ISOEngine.Tests/OfflineRegistryEditorTests.cs` (actualizados
para el nuevo parámetro; sin cambio de comportamiento).

**Nuevo (`tests/MRS.PostInstall.Tests`)**: `MRS.PostInstall.Tests.csproj`,
`Fakes/FakeProcessRunner.cs`, `PostInstallConfigurationValidatorTests.cs`,
`PostInstallPackageValidatorTests.cs`, `CommandExecutorTests.cs`,
`PostInstallCommandsTests.cs`, `SetupCompleteScriptGeneratorTests.cs`,
`PostInstallPackageBuilderTests.cs`.

No se ha modificado `RemovalEngine`, `RemovalPlanning`, `ComponentCatalog`,
`ProfileEngine`, `SecurityOptions`, `InstallationOptions`,
`ImageInventoryService`, `WorkingImageFactory`, `LabConfig`/`boot.wim`, ni
generado ninguna ISO.

## Commit recomendado

`git commit` con el resumen "P18: PostInstall real — .NET 8.0.26 Desktop
Runtime + PCPI vía SetupComplete.cmd ($OEM$\$$\Setup\Scripts), sin generar
la ISO todavía" y `git push` a `main`.
