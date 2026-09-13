# Resultado - P17: validación real de boot.wim, LabConfig y OOBE

## Resultado en una frase

**La prueba real no se pudo ejecutar.** Esta sesión no tiene privilegios de
administrador, y DISM los exige incluso para una consulta de solo lectura
(`Get-WimInfo`), no solo para montar/modificar. Siguiendo la instrucción
explícita de la sección 1 del prompt ("si no está elevado, abortar con
mensaje claro; NO intentar continuar y después interpretar Error 740 como
fallo de implementación"), se abortó la prueba real **antes** de tocar
nada, sin hacer ningún cambio de código (no hay ningún problema real que
corregir: no llegó a ejecutarse nada que pudiera fallar). Se documenta esto
con total transparencia, tal como exige el prompt, en vez de fingir una
validación que no ocurrió.

```
[VALIDATION] Administrator privileges: FAILED
[VALIDATION] Aborting real test — this session is not elevated.
```

## 1. Ejecución elevada — comprobación

```powershell
$id = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = New-Object Security.Principal.WindowsPrincipal($id)
$principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
```

Resultado: **`False`** (usuario `DESKTOP-7H7G7QP\B3RASCASA`, sesión no elevada).

Confirmación independiente con DISM (antes de intentar nada sobre
`boot.wim`, para no arriesgar nada):

```
> dism /English /Get-WimInfo /WimFile:D:\sources\install.wim
Error: 740
Elevated permissions are required to run DISM.
```

Esto reproduce exactamente lo que ya se documentó en `prompts/16-resultado.md`
para `/Get-MountedImageInfo`, y confirma que **ninguna** operación de DISM
—ni siquiera una consulta de solo lectura como `/Get-WimInfo`— funciona sin
privilegios elevados en este entorno. También se comprobó
`Get-WindowsImage` (el cmdlet de PowerShell del módulo DISM): mismo
resultado, "La operación solicitada requiere elevación."

**Conclusión de la sección 1**: se abortó aquí. Las secciones 4-6, 8-10 del
prompt (LabConfig real, idempotencia real, OOBE offline real, prueba de RAM
en VM, TPM/Secure Boot/CPU individuales) requieren `Mount-Wim`/`reg load`
sobre un WIM real, que son operaciones DISM/de registro con el mismo
requisito de elevación — no se intentaron.

## 2. ISO original — lo que sí se pudo hacer sin elevación

`Mount-DiskImage` (montar la ISO como unidad virtual, de solo lectura) **no
requiere elevación** — es una operación distinta de DISM. Se usó para
identificar y dejar constancia de la ISO, sin modificar nada:

Se localizaron dos ISO reales en el escritorio del usuario:

- `26300.8697.260612-2005.GE_PRERELEASE_IM_CLIENTMULTI_X64FRE_ES-ES.ISO` —
  la más próxima a un origen "limpio" (prerelease oficial, build
  **26300.8697**, no exactamente la build objetivo 26300.9278 — no hay
  ninguna ISO de esa build exacta disponible en este entorno).
- `W11-26H2+PCPI-ES-ny_Chuyo31.iso` — ya es una ISO personalizada del propio
  usuario (nombre sugiere post-procesado "PCPI"), no una fuente adecuada
  para esta prueba.

Se montó la primera (solo lectura) y se registró, sin usar DISM (con
`Test-Path`/`Get-Item`/`Get-FileHash`, que no requieren elevación):

```
[VALIDATION] Source ISO: 26300.8697.260612-2005.GE_PRERELEASE_IM_CLIENTMULTI_X64FRE_ES-ES.ISO
[VALIDATION] Install.wim: sources\install.wim existe, 7 955 102 491 bytes
[VALIDATION] Boot.wim: sources\boot.wim existe, 692 758 253 bytes
[VALIDATION] Boot.wim SHA256: 5B6BA52158C408FADBD54C9ECC7E4A5ACBC3D1DAD8FD112A40FB5345AE15B6E6
[VALIDATION] Pro index: NO SE PUDO DETERMINAR (Get-WindowsImage/DISM requieren elevación)
```

Este hash de `boot.wim` queda registrado aquí precisamente para lo que pide
la sección 2 ("calcular una huella para demostrar posteriormente que la ISO
original no cambió"): si en una sesión elevada futura se ejecuta la prueba
real sobre esta misma ISO, recalcular este hash después debe dar
**exactamente el mismo valor** — eso demuestra que la ISO original no se
tocó (nuestro propio código, además, nunca abre la ISO en modo escritura:
`BootWimProvisioner` monta con `-Access ReadOnly` y solo copia, ver
`prompts/16-resultado.md`).

La ISO se desmontó inmediatamente después (`Dismount-DiskImage`); no quedó
ningún montaje.

## 3-11. Workspace / LabConfig real / idempotencia real / cuenta local real / OOBE real / RAM / TPM-SecureBoot-CPU / Storage

**No ejecutadas.** Todas requieren, en algún punto, `Mount-Wim` sobre
`boot.wim` y/o `reg load` sobre su hive `SYSTEM` — ambas bloqueadas por la
falta de elevación (sección 1). Intentarlas habría producido `Error: 740`
en el primer paso real, sin ninguna relación con la corrección del código
de P16 — exactamente la trampa que la sección 1 pide evitar explícitamente
("NO interpretar Error 740 como fallo de implementación").

La sección 11 (Storage) es la única de este bloque cuyo comportamiento SÍ
se puede confirmar sin elevación, porque `InstallationExecutionValidator`
actúa **antes** de tocar nada (no requiere DISM ni registro):

```
> dotnet test --filter "FullyQualifiedName~InstallationExecutionValidatorTests"
Correctas! - Con error: 0, Superado: 3, Omitido: 0, Total: 3
```

Confirma: si `BypassStorage = true`, el servicio aborta con el mensaje
exacto "El bypass de almacenamiento no está implementado/validado para esta
build." antes de llamar a `BootWimProvisioner`/`BootWimModifier` — igual que
ya se verificó de extremo a extremo en P16
(`InstallationImageServiceTests.Storage_bypass_enabled_aborts_before_touching_the_workspace`).

## 12-13. Limpieza / integridad de la ISO original

No se montó ningún WIM ni se cargó ningún hive de registro, así que no hay
nada que limpiar:

```
[VALIDATION] MRS mounts: 0 (ninguna operación DISM llegó a ejecutarse)
[VALIDATION] Hives pendientes: 0
[VALIDATION] Source ISO unchanged: OK (nunca se abrió en escritura; hash de boot.wim registrado arriba para verificación futura)
```

## 14. Progreso

No aplica: no se ejecutó ningún paso real de `InstallationImageService`
(nada que reportar en las fases 0-100 del prompt). El sistema de progreso
de P16 (`InstallationProgressInfo`) no se ha modificado.

## 15. Tests automáticos

No se sustituyó ninguna prueba real por un test simulado (no había ninguna
prueba real que sustituir: no llegó a ejecutarse). Se mantienen exactamente
los mismos tests de P16, sin añadir ninguno nuevo (esta fase es de
validación real, no de código):

```
MRS.InstallationOptions.Tests : 17/17
MRS.ImageEngine.Tests         : 78/78
MRS.ISOEngine.Tests           : 71/71
MRS.ProfileEngine.Tests       : 21/21
MRS.ComponentCatalog.Tests    : 59/59
MRS.RemovalPlanning.Tests     : 48/48
MRS.RemovalEngine.Tests       : 48/48
```

Total: **342/342** (igual que el resultado final de P16 — "342+" cumplido,
sin regresiones).

```
dotnet build MRS-Windows-Builder.sln
Compilación correcta. 0 Advertencia(s). 0 Errores.
```

## 16. Correcciones

**Ninguna.** No se descubrió ningún problema porque no se pudo ejecutar
ninguna prueba real que pudiera revelarlo. No se ha modificado ningún
archivo de código en esta fase — solo se crea este documento.

## 17. Tabla de funcionalidades

| Funcionalidad | Implementada | Validada realmente |
|---|---|---|
| Cuenta local | Sí | No |
| OOBE offline | Sí (mecanismo BypassNRO implementado, no activado automáticamente — ver P16) | No |
| TPM bypass | Sí | No |
| Secure Boot bypass | Sí | No |
| CPU bypass | Sí | No |
| RAM bypass | Sí | No |
| 2 GB RAM Setup | N/A (nada que "implementar": es una prueba, no un mecanismo) | No |
| Storage bypass | No | No |

Ninguna fila se marca "Sí" en "Validada realmente" sin evidencia de una
prueba real ejecutada — y no se ejecutó ninguna, así que ninguna lo está.

## Documentación

- **ISO utilizada**: `26300.8697.260612-2005.GE_PRERELEASE_IM_CLIENTMULTI_X64FRE_ES-ES.ISO`
  (build 26300.8697; no hay una ISO de la build exacta 26300.9278 disponible
  en este entorno).
- **Build**: 26300.8697 (objetivo real: 26300.9278 — no coinciden).
- **Arquitectura**: no determinada (requiere `Get-WindowsImage`/DISM, bloqueado por elevación).
- **Edición/índice**: no determinado (mismo motivo).
- **Comandos relevantes**: `Mount-DiskImage`/`Dismount-DiskImage` (sin elevación,
  usados); `dism /Get-WimInfo`, `Get-WindowsImage`, `Mount-Wim`, `reg load`
  (todos bloqueados por `Error: 740` / "requiere elevación").
- **Resultado de cada prueba**: ver tabla de la sección 17 — todas "No validada".
- **Errores encontrados**: `Error: 740` de DISM (esperado, no un fallo de
  implementación — es exactamente el escenario que la sección 1 pide
  distinguir).
- **Correcciones realizadas**: ninguna.
- **Integridad de la ISO original**: no se modificó (nunca se abrió en
  escritura); hash de `boot.wim` registrado para verificación futura.
- **Estado final de mounts**: ninguno (ni de la ISO —desmontada— ni de
  ningún WIM —nunca montado—).

## Qué falta para completar P17 de verdad

Alguien con una sesión elevada en esta misma máquina (o el propio usuario)
tiene que ejecutar el flujo real. Pasos concretos, reutilizando el código ya
implementado y probado en P16:

1. Abrir una consola elevada (`Ejecutar como administrador`).
2. Confirmar `dism /Get-WimInfo /WimFile:...` ya no devuelve `Error: 740`.
3. Ejecutar `InstallationImageService.ApplyAsync` (o una pequeña utilidad de
   prueba que lo invoque) contra una copia de la ISO real de la build
   26300.9278 x64 es-ES Pro, índice 2 (o, si no está disponible, contra la
   ISO prerelease 26300.8697 ya localizada, dejando explícito que no es la
   build exacta).
4. Repetir con una segunda combinación de opciones (TPM=true, SecureBoot=false,
   CPU=true, RAM=false) sobre el mismo `boot.wim` de trabajo, y confirmar con
   `reg query`/una segunda inspección offline que Secure Boot y RAM
   **no** permanecen aplicados — la prueba de idempotencia real de la
   sección 6.
5. Confirmar con `Get-MountedWimInfo` que no queda ningún mount al terminar,
   y que el hash de `boot.wim` **dentro de la ISO montada de solo lectura**
   (no la copia de trabajo) sigue siendo `5B6BA52158C408FADBD54C9ECC7E4A5ACBC3D1DAD8FD112A40FB5345AE15B6E6`
   si se usó la misma ISO prerelease de este documento.
6. Para OOBE offline y la prueba de RAM: requieren además una VM (VirtualBox
   está instalado en esta máquina — se detectó `VBoxGuestAdditions.iso` —
   pero automatizar una instalación completa de Windows dentro de una VM,
   con interacción en pantallas de arranque/OOBE, no es algo que se pueda
   conducir de forma no interactiva desde esta sesión; requiere al usuario
   al mando de la VM).

## Resultado final

### AUTOMÁTICO
- tests: **342/342**
- build: OK
- 0 errores
- 0 warnings

### PRUEBA REAL
- ISO utilizada: `26300.8697.260612-2005.GE_PRERELEASE_IM_CLIENTMULTI_X64FRE_ES-ES.ISO` (build 26300.8697, no la build objetivo exacta)
- Pro index: no determinado (bloqueado por elevación)
- boot.wim modificado: **No** (la prueba no llegó a ejecutarse)
- LabConfig verificado: **No**
- autounattend verificado: **No**
- OOBE offline: **no validado**
- TPM: **no validado**
- Secure Boot: **no validado**
- CPU: **no validado**
- RAM: **no validado**
- prueba 2 GB: **no realizada**
- Storage: no implementado (confirmado por test automatizado, sin necesitar elevación)

### LIMPIEZA
- mounts MRS: 0
- hives pendientes: 0
- ISO original intacta: **Sí**

### DOCUMENTACIÓN
- `prompts/17-resultado.md` (este archivo)

## Commit recomendado

`git commit` con el resumen "P17: validación real bloqueada por falta de
sesión elevada (Error 740 de DISM incluso en consultas de solo lectura);
documentación honesta, sin cambios de código, suite automática 342/342" y
`git push` a `main`.
