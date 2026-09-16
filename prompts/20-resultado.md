# Resultado - P20: primera generación real de ISO MRS

## Resultado en una frase

**La generación real no se pudo ni siquiera iniciar.** Esta sesión no está
elevada y Windows ADK/`oscdimg.exe` no está instalado en esta máquina — los
dos requisitos que la sección 1 del prompt exige comprobar **antes** de
tocar cualquier WIM. Siguiendo la instrucción explícita ("Si el proceso no
está elevado: ABORTAR... No continuar"), se abortó aquí, con los dos
mensajes exactos que pide el prompt, sin ejecutar ninguna fase del pipeline
(`CopyIsoTree` en adelante). No es correcto usar la frase "Generación real
realizada, validación VM pendiente." (sección 22): esa frase describe una
generación que sí llegó a completarse y solo queda pendiente de arrancar en
VM; aquí la generación **ni se intentó**, por lo que el estado real es:

```
Generación real NO iniciada — bloqueada en la comprobación de entorno (sección 1).
```

## 1. Preparación del entorno — comprobación

```powershell
$id = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = New-Object Security.Principal.WindowsPrincipal($id)
$isAdmin = $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
"IsAdmin=$isAdmin User=$($id.Name)"
```

Resultado: **`IsAdmin=False User=DESKTOP-7H7G7QP\B3RASCASA`**.

```powershell
$paths = @(
    'C:\Program Files (x86)\Windows Kits\10\Assessment and Deployment Kit\Deployment Tools\amd64\Oscdimg\oscdimg.exe',
    'C:\Program Files\Windows Kits\10\Assessment and Deployment Kit\Deployment Tools\amd64\Oscdimg\oscdimg.exe'
)
$found = $paths | Where-Object { Test-Path $_ }
$cmd = Get-Command oscdimg.exe -ErrorAction SilentlyContinue
"StandardPathsFound=$($found.Count -gt 0) InPath=$($cmd -ne $null)"
```

Resultado: **`StandardPathsFound=False InPath=False`**.

Ambas comprobaciones reproducen exactamente lo ya documentado en
`prompts/16-resultado.md`, `prompts/17-resultado.md` y `prompts/19-resultado.md`.
Siguiendo el prompt al pie de la letra, se abortó aquí:

```
[VALIDATION] Se requieren privilegios de administrador para ejecutar DISM.
[VALIDATION] Windows ADK/oscdimg no está instalado.
[VALIDATION] Aborting — no se ejecuta CopyIsoTree ni ninguna fase posterior.
```

No se descargó ni instaló ningún software (Windows ADK ni nada más), tal
como exige explícitamente el prompt.

## Corrección aplicada (sección 19/23: ISOEngine no está en "NO TOCAR")

Al intentar ejecutar esta validación se descubrió que
`IsoGenerationPipeline` (P19) **no comprobaba el entorno por sí mismo**: si
se hubiera invocado sin más, habría avanzado hasta `CopyIsoTree` (que sí
funciona sin elevación) y habría fallado de forma confusa varias fases
después, dentro de `PrepareBootWim` o de `oscdimg`, con un `DISM Error: 740`
o un mensaje de "oscdimg no disponible" que un usuario podría interpretar
como un fallo de implementación en vez de una carencia del entorno — la
misma trampa que P17 documentó explícitamente para pruebas manuales.

Se añadió una comprobación de entorno explícita, como **primera** fase del
pipeline (antes incluso de validar la propia `IsoGenerationRequest`), con
los dos mensajes literales exigidos por la sección 1 del prompt:

- `src/MRS.ISOEngine/Pipeline/IElevationChecker.cs` / `ElevationChecker.cs`
  — envuelve `WindowsPrincipal.IsInRole(Administrator)`; inyectable para
  poder simular ambos estados en tests (la propia sesión de tests tampoco
  está elevada).
- `src/MRS.ISOEngine/Pipeline/EnvironmentPreflightResult.cs` /
  `EnvironmentPreflightChecker.cs` — combina `IElevationChecker` y el
  `IOscdimgRunner.IsAvailable()` ya existente (P19) en un único resultado
  con los mensajes exactos del prompt.
- `IsoGenerationPipeline` ahora recibe un `IElevationChecker` opcional
  (por defecto `new ElevationChecker()`, sin romper compatibilidad) y
  ejecuta `EnvironmentPreflightChecker.Check(...)` como paso 0, antes de
  `CreateWorkspace`/`CopyIsoTree`.

Esto es una corrección necesaria descubierta durante la propia prueba de
P20 (no una funcionalidad nueva ajena a lo pedido), y no toca ningún
proyecto de la lista "NO TOCAR" de la sección 23 (`MRS.ISOEngine` no está en
esa lista). No cambia el comportamiento de ningún flujo ya probado: si el
entorno está listo, el pipeline sigue exactamente igual que en P19.

Tests nuevos (6, todos pasando):
- `IsoGenerationPipelineTests.Missing_elevation_aborts_with_the_exact_required_message_before_validating_the_request`
- `IsoGenerationPipelineTests.Missing_oscdimg_aborts_with_the_exact_required_message_before_touching_any_WIM`
- `EnvironmentPreflightCheckerTests` (4 tests: listo, sin elevación, sin oscdimg, ambos)

## 2. ISO de prueba

Se localizó, en el escritorio del usuario, una ISO real que coincide
**exactamente** con la build preferida por el prompt (a diferencia de P17,
donde solo había una prerelease de build distinta):

```
descargar.w11\26300.9278_amd64_es-es_multi_def45c01_convert\
26300.9278.260824-2119.26H2_GE_RELEASE_SVC_PROD3_CLIENTMULTI_X64FRE_ES-ES.ISO
```

- **Build**: 26300.9278 (coincide con la preferida por el prompt).
- **Idioma**: es-ES (según nombre de archivo — `CLIENTMULTI` sugiere
  imagen multi-edición; no se pudo confirmar el idioma interno del WIM sin
  DISM/`Get-WindowsImage`, que requieren elevación).
- **Arquitectura**: x64 (`X64FRE` en el nombre).
- **Edición/índice Pro**: **NO SE PUDO DETERMINAR** (`Get-WindowsImage`/
  `dism /Get-WimInfo` requieren elevación — mismo bloqueo que P17).
- **Tamaño**: 10 640 267 264 bytes.

No se afirma que esta ISO sea Pro/índice 2 porque no hay forma de
confirmarlo sin DISM en este entorno; se documenta honestamente como
"no determinado", tal como exige la sección 2 del prompt.

## 3. Integridad de la ISO — hashes registrados (sin elevación)

Usando únicamente `Mount-DiskImage`/`Get-FileHash` (no requieren
elevación, a diferencia de DISM):

```
[VALIDATION] ISO SHA256: 6E3900478F651A9ACAF5EF1D21163E3FD0BE50A6186F45A7F56209FB828DA2EE
[VALIDATION] boot.wim: 719 302 724 bytes, SHA256 E81A8F66B12A9EE0C2A2EF0F26AC0DDBACA33A17681FA5D325DCB9B3D5DB23C7
[VALIDATION] install.wim: 9 657 098 178 bytes, SHA256 3DD8C5DD4F055EEC4C8D1CD9146C8417D0F38BAD97F9AA1D3CBEA96DD60079D5
[VALIDATION] boot\etfsboot.com: existe
[VALIDATION] efi\microsoft\boot\efisys.bin: existe
[VALIDATION] setup.exe: existe
```

Estos hashes quedan registrados aquí precisamente para que, en una futura
sesión elevada con Windows ADK instalado, se pueda repetir el cálculo sobre
la misma ISO y confirmar que **no cambió** (cumpliendo la sección 3 del
prompt: "Al finalizar repetirlos. Esperado: ISO original intacta"). La ISO
se montó y desmontó en modo **solo lectura**; no se ejecutó ningún proceso
de escritura sobre ella.

## 4. Configuración de prueba — no aplicada

La configuración conservadora (Perfil RECOMENDADO, Defender/Windows Update
mantener, TPM/SecureBoot/CPU/RAM bypass, Storage NO, cuenta local sí) está
documentada en el prompt y ya implementada por P15/P16, pero **no se llegó
a aplicar** porque el pipeline abortó en la sección 1.

## 5-18. Generación / boot.wim / install.wim / autounattend / PostInstall / oscdimg / ISO resultante / mounts / VirtualBox / 2GB / TPM-SecureBoot-CPU / OOBE offline / PostInstall end-to-end / automatización

**Ninguna de estas fases se ejecutó.** Todas dependen, directa o
indirectamente, de DISM (`Mount-Wim`, `Export-Image`) y/o de `oscdimg.exe`
— ambos bloqueados por la sección 1. Intentarlas habría reproducido
exactamente el `Error: 740` ya documentado en P16/P17/P19, sin relación
alguna con la corrección del código de P19.

### Hallazgo relevante para una futura sesión elevada: archivos reales de PostInstall SÍ existen ahora

A diferencia de P18/P19 (donde no se encontraron archivos con los nombres
exactos requeridos), esta vez sí se localizaron, en el escritorio del
usuario, archivos reales con los **nombres exactos** que
`PostInstallConfiguration` exige por defecto:

```
Desktop\PCPI-Minimal\PCPI-Retro-Minimals-Portable-0.0.5.exe            (65 858 703 bytes)
Desktop\NTLite-Cache\26300.9278...\sources\$OEM$\$$\Setup\FilesU\
    windowsdesktop-runtime-8.0.26-win-x64.exe                          (58 453 024 bytes)
    PCPI-Retro-Minimals-Portable-0.0.5.exe
```

No se han usado en esta fase (no se ejecutó ningún empaquetado real: eso
requeriría invocar el pipeline, bloqueado por la sección 1), pero quedan
documentados aquí para que una sesión elevada futura no tenga que
localizarlos de nuevo ni inventar sustitutos.

## 19. Correcciones

Ver "Corrección aplicada" arriba (comprobación de entorno explícita en
`IsoGenerationPipeline`). Es la única corrección de esta fase; no se hizo
ningún cambio grande, y no se tocó ningún proyecto de la lista "NO TOCAR".

## 20. Tests automáticos

```
dotnet test MRS-Windows-Builder.sln
```

```
MRS.InstallationOptions.Tests : 17/17
MRS.ProfileEngine.Tests       : 21/21
MRS.PostInstall.Tests         : 44/44
MRS.ImageEngine.Tests         : 78/78
MRS.ComponentCatalog.Tests    : 59/59
MRS.RemovalPlanning.Tests     : 48/48
MRS.RemovalEngine.Tests       : 48/48
MRS.ISOEngine.Tests           : 102/102   (96 de P19 + 6 nuevos de P20)
```

Total: **417/417**, 0 fallos. Sin regresiones respecto al baseline de P19
(411/411 + 6 tests nuevos de la comprobación de entorno).

```
dotnet build MRS-Windows-Builder.sln
Compilación correcta. 0 Advertencia(s). 0 Errores.
```

## 21. Documentación — tabla final

| Prueba | Resultado |
|---|---|
| Build | PASS |
| Tests | 417/417 |
| oscdimg | FAIL (no instalado) |
| ISO generada | FAIL (no se llegó a generar) |
| ISO arranca | NO VALIDADO |
| Setup | NO VALIDADO |
| Pro | NO VALIDADO |
| Cuenta local | NO VALIDADO |
| OOBE offline | NO VALIDADO |
| TPM bypass | NO VALIDADO |
| Secure Boot bypass | NO VALIDADO |
| CPU bypass | NO VALIDADO |
| RAM bypass | NO VALIDADO |
| Setup con 2 GB | NO VALIDADO |
| PostInstall | NO VALIDADO |
| PCPI | NO VALIDADO |
| Storage bypass | NO IMPLEMENTADO |

- **ISO utilizada**: `26300.9278.260824-2119.26H2_GE_RELEASE_SVC_PROD3_CLIENTMULTI_X64FRE_ES-ES.ISO`
- **Build**: 26300.9278 (coincide con la preferida por el prompt)
- **Hashes**: ver sección 3 (ISO, boot.wim, install.wim)
- **Tamaño final (ISO generada)**: N/A — no se generó ninguna ISO
- **Workspace**: no se llegó a crear ningún `GenerationWorkspace`
- **Mounts**: 0 (solo se montó la ISO original, de solo lectura, y se
  desmontó inmediatamente con `Dismount-DiskImage`; ningún WIM se montó)
- **Errores**: `Se requieren privilegios de administrador para ejecutar DISM.`
  y `Windows ADK/oscdimg no está instalado.` (ambos esperados, documentados
  en P16/P17/P19; no son fallos de implementación)
- **Correcciones**: comprobación de entorno explícita añadida a
  `IsoGenerationPipeline` (ver arriba)
- **Limitaciones**: esta sesión no puede autoelevarse (UAC requiere consentimiento
  interactivo) ni instalar el Windows ADK automáticamente (prohibido
  explícitamente por el prompt); tampoco hay forma de conducir una VM de
  VirtualBox de forma no interactiva para las pruebas de las secciones 13-18

## 22. Criterio de éxito

No se afirma que "342+/417 tests pasan" ni "oscdimg devuelve 0" equivalgan
a P20 completado — de hecho ni siquiera se pudo ejecutar `oscdimg`. Estado
real, siguiendo el espíritu de la sección 22:

```
Generación real NO iniciada — bloqueada en la comprobación de entorno (sección 1).
Validación VM: no aplica todavía (no hay ISO que arrancar).
```

## 23. No tocar

No se modificó `RemovalEngine`, `ComponentCatalog`, `ProfileEngine`,
`InstallationOptions`, `SecurityOptions`, `LabConfig` ni `PostInstall`. No
se implementó optimización de tamaño ni bypass de almacenamiento.

## 24. Resultado final

- **Tests**: 417/417
- **Build**: OK, 0 errores, 0 warnings
- **ADK/oscdimg**: NO instalado (`StandardPathsFound=False InPath=False`)
- **Elevación**: NO (`IsAdmin=False`)
- **ISO utilizada**: build 26300.9278, x64, es-ES (según nombre de archivo),
  edición/índice Pro no determinado sin DISM
- **ISO generada**: ninguna
- **Tamaño / SHA256 de la ISO generada**: N/A
- **Mounts finales**: 0
- **ISO original intacta**: Sí (solo lectura; hashes registrados en sección 3
  para verificación futura)
- **Resultado de VM**: no aplica (no hay ISO)
- **Resultado 2 GB**: no aplica
- **Resultado OOBE**: no aplica
- **Resultado bypasses (TPM/SecureBoot/CPU/RAM)**: no aplica
- **Resultado PostInstall**: no aplica (aunque se localizaron, y se
  documentan, archivos reales con los nombres exactos requeridos, listos
  para una sesión futura)
- **Problemas encontrados**: falta de elevación y de Windows ADK (entorno,
  no código)
- **Correcciones**: comprobación de entorno explícita en
  `IsoGenerationPipeline` (detalle arriba)
- **Commit recomendado**: "P20: comprobación de entorno explícita en el
  pipeline (DISM/ADK); generación real bloqueada por falta de sesión
  elevada y de Windows ADK, documentada con evidencia (ISO build 26300.9278
  exacta, hashes registrados); sin regresiones (417/417)"

## Qué falta para completar P20 de verdad

1. Ejecutar esta misma prueba desde una consola elevada (`Ejecutar como
   administrador`) con Windows ADK (Deployment Tools) instalado.
2. Invocar `IsoGenerationPipeline.GenerateAsync` contra la ISO ya
   localizada aquí (build 26300.9278 exacta, hash de sección 3) con la
   configuración conservadora de la sección 4 del prompt.
3. Usar los archivos de PostInstall ya localizados en la sección "Hallazgo
   relevante" (nombres exactos, sin necesidad de buscarlos de nuevo).
4. Verificar físicamente boot.wim/install.wim/autounattend/oscdimg/mounts
   según las secciones 6-12 del prompt.
5. Crear una VM en VirtualBox (detectado en sesiones anteriores) y seguir
   el orden de pruebas de las secciones 13-18, documentando cada resultado
   por separado sin combinar pruebas.
6. Repetir los hashes de la sección 3 sobre la ISO original al terminar,
   para confirmar que no cambió.

## Commit recomendado

`git commit` con el resumen "P20: comprobación de entorno explícita en el
pipeline; generación real bloqueada por falta de sesión elevada y Windows
ADK, documentada con evidencia real (ISO build 26300.9278, hashes
registrados); 417/417 sin regresiones" y `git push` a `main`.
