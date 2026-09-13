PROMPT 16 — IMPLEMENTACIÓN REAL: BOOT.WIM, LABCONFIG Y OOBE

Objetivo:
Pasar de la planificación de P15 a la implementación real de las opciones de instalación y compatibilidad sobre un workspace de generación.

IMPORTANTE:
P15 fue una fase de diseño/investigación.
P16 es la primera fase que puede modificar imágenes reales, pero SIEMPRE sobre una copia de trabajo.

La ISO original debe permanecer intacta.

==================================================
1. ALCANCE
==================================================

Implementar realmente:

- bypass TPM 2.0;
- bypass Secure Boot;
- bypass CPU;
- bypass RAM;
- cuenta local mediante autounattend.xml.

Investigar y, si existe un mecanismo fiable para Windows 11 26H2:

- OOBE sin conexión.

NO implementar todavía:

- bypass de almacenamiento si continúa sin mecanismo fiable confirmado;
- PostInstall .NET;
- lanzamiento de PCPI;
- optimización de tamaño;
- eliminación adicional de componentes;
- generación ISO definitiva.

==================================================
2. BOOT.WIM
==================================================

Implementar un servicio específico para modificar boot.wim.

Debe trabajar sobre una COPIA de boot.wim dentro del workspace.

Flujo:

ISO original
    ↓
Workspace
    ↓
copia de boot.wim
    ↓
Mount-Wim
    ↓
modificación offline
    ↓
Commit
    ↓
Unmount
    ↓
validación

Nunca modificar directamente:

D:\sources\boot.wim

ni ninguna otra ruta perteneciente a la ISO original.

Usar el ciclo seguro de montaje/desmontaje ya utilizado en el proyecto.

==================================================
3. LABCONFIG
==================================================

Implementar las opciones P15 mediante:

HKLM\SYSTEM\Setup\LabConfig

dentro del boot.wim montado.

Opciones independientes:

BypassTPM
BypassSecureBoot
BypassCPU
BypassRAM

Aplicar únicamente las opciones activadas.

No crear automáticamente opciones que el usuario haya desactivado.

Ejemplo conceptual:

BypassTPM = 1
BypassSecureBoot = 1
BypassCPU = 1
BypassRAM = 1

NO inventar claves adicionales.

Utilizar DISM + REG/Registry offline de forma segura y reproducible.

==================================================
4. REGISTRO OFFLINE
==================================================

Crear una abstracción apropiada para:

- montar hive SYSTEM;
- modificar claves;
- descargar hive;
- verificar cambios.

No dejar hives cargados al finalizar.

El servicio debe garantizar limpieza incluso ante excepciones.

Si una operación falla:

- intentar descargar hive;
- desmontar WIM;
- no dejar el workspace en estado inconsistente;
- preservar logs.

Registrar:

[COMPAT] LabConfig mounted
[COMPAT] BypassTPM applied
[COMPAT] BypassSecureBoot applied
[COMPAT] BypassCPU applied
[COMPAT] BypassRAM applied
[COMPAT] LabConfig verified

Solo registrar las opciones realmente aplicadas.

==================================================
5. CUENTA LOCAL
==================================================

Implementar la generación de:

autounattend.xml

para permitir configuración de cuenta local durante OOBE.

IMPORTANTE:

No codificar una contraseña real en el proyecto.

No incluir credenciales del usuario en el repositorio.

La solución debe permitir:

- cuenta local;
- nombre configurable si la arquitectura actual lo permite;
- contraseña opcional/configurable;
- evitar dependencia de una cuenta Microsoft.

Separar:

AutounattendGenerator

de

ISO/Workspace/servicios de DISM.

El generador debe ser determinista.

La configuración debe poder validarse antes de escribir el archivo.

==================================================
6. UBICACIÓN DE AUTOUNATTEND.XML
==================================================

Determinar la ubicación correcta para que Windows Setup lo detecte automáticamente desde la ISO.

Preferir el mecanismo estándar de Windows Setup.

No copiar el archivo arbitrariamente dentro de install.wim si no es necesario.

La solución debe dejar documentado:

- dónde se coloca;
- cuándo Setup lo consume;
- qué ocurre si el usuario ya proporciona otra respuesta;
- qué partes del OOBE controla realmente.

==================================================
7. OOBE SIN CONEXIÓN
==================================================

P15 dejó BypassNRO como pendiente de confirmar.

Antes de implementarlo:

determinar si continúa siendo válido en Windows 11 26H2 build 26300.9278.

Si se confirma:

- implementar de forma modular;
- asociarlo exclusivamente a AllowOfflineOobe;
- permitir desactivarlo;
- validarlo en la imagen de prueba.

Si NO es fiable:

NO implementarlo como solución definitiva.

Marcarlo como:

"Pendiente de mecanismo fiable para esta build"

y documentar la razón.

No utilizar hacks de OOBE que puedan romper futuras versiones sin dejarlo explícitamente documentado.

==================================================
8. BYPASS DE RAM Y 2 GB
==================================================

Mantener la distinción de P15:

BypassRAM:
permite intentar superar la comprobación del instalador.

NO significa:

"Windows 11 está oficialmente soportado con 2 GB".

La UI y documentación deben mantener esta distinción.

Si es posible validar el bypass con una VM de 2 GB, documentar el resultado.

Si Setup supera la comprobación pero Windows no funciona correctamente:

registrarlo como limitación.

No ocultar resultados negativos.

==================================================
9. STORAGE
==================================================

NO implementar bypass de almacenamiento todavía si P15 no encontró mecanismo fiable.

La opción puede permanecer en InstallationOptions, pero:

- no debe aparentar estar implementada;
- Generation/Installation planner debe conocer su estado;
- si está activada y no existe implementación, la validación debe impedir una generación engañosa.

Mostrar un error claro:

"El bypass de almacenamiento no está implementado/validado para esta build."

NO ignorar silenciosamente la opción.

==================================================
10. SERVICIO PRINCIPAL
==================================================

Crear una abstracción tipo:

IInstallationImageService

o nombre equivalente.

Debe recibir:

- source ISO;
- workspace;
- InstallationOptions.

Y ejecutar solamente las modificaciones correspondientes.

Ejemplo:

InstallationOptions
        ↓
InstallationConfigurationPlanner
        ↓
InstallationImageService
        ↓
BootWimModifier
        ↓
AutounattendGenerator

Mantener separación clara entre planificación y ejecución.

==================================================
11. IDEMPOTENCIA
==================================================

Muy importante.

Si se aplica dos veces sobre el mismo workspace:

- no duplicar autounattend;
- no duplicar claves;
- no producir XML corrupto;
- no dejar múltiples montajes;
- no generar estados contradictorios.

LabConfig debe actualizar las claves existentes de forma determinista.

==================================================
12. VALIDACIÓN POST-MODIFICACIÓN
==================================================

Después de modificar boot.wim:

volver a montar o inspeccionar la imagen de trabajo y verificar:

- LabConfig existe cuando corresponde;
- BypassTPM tiene el valor esperado;
- BypassSecureBoot tiene el valor esperado;
- BypassCPU tiene el valor esperado;
- BypassRAM tiene el valor esperado;
- opciones desactivadas no aparecen;
- autounattend.xml existe donde corresponda;
- XML es válido.

No validar solamente que DISM devolvió ExitCode 0.

==================================================
13. WORKSPACE Y SEGURIDAD
==================================================

Reutilizar el patrón de P09:

- OperationId;
- WorkspaceId;
- logging estructurado;
- validación de rutas;
- source ISO de solo lectura;
- trabajo exclusivamente sobre workspace;
- comprobación de mounts;
- cleanup seguro;
- preservar workspace si hay error.

No utilizar:

/Cleanup-Mountpoints

como solución automática.

No borrar directorios de montaje a ciegas.

==================================================
14. PROGRESO
==================================================

Utilizar el sistema de progreso existente de P10/P14 cuando sea arquitectónicamente compatible.

Mostrar fases reales:

0-10   Preparando workspace
10-20  Preparando boot.wim
20-35  Montando boot.wim
35-60  Aplicando compatibilidad
60-75  Configurando OOBE
75-90  Validando configuración
90-100 Finalizando

NO simular progreso interno de DISM.

==================================================
15. UI
==================================================

La UI actual de PLAN DE MODIFICACIÓN ya tiene las opciones.

No rediseñarla completamente.

Al generar/aplicar la configuración, mostrar claramente:

OPCIONES DE INSTALACIÓN

✓ Cuenta local
✓ OOBE sin conexión

COMPATIBILIDAD

✓ TPM
✓ Secure Boot
✓ CPU
✓ RAM
⚠ Almacenamiento no implementado

No permitir seleccionar almacenamiento como si estuviera operativo.

Si la arquitectura actual todavía no tiene generación real, integrar el servicio sin crear todavía todo el ISOEngine final.

==================================================
16. TESTS
==================================================

Añadir tests para:

### LabConfig

- TPM activado;
- TPM desactivado;
- Secure Boot activado/desactivado;
- CPU activado/desactivado;
- RAM activado/desactivado;
- combinaciones;
- ausencia de claves no solicitadas;
- actualización idempotente.

### Autounattend

- XML válido;
- cuenta local;
- ausencia de credenciales hardcoded;
- generación determinista;
- opciones de configuración.

### Validación

- boot.wim inexistente;
- install.wim inexistente;
- workspace igual a source ISO;
- índice inválido;
- arquitectura incompatible;
- storage bypass no implementado;
- configuración válida.

### Seguridad

- hive descargado tras error;
- workspace preservado en fallo;
- no modificar ISO original.

==================================================
17. TEST REAL CONTROLADO
==================================================

Preparar soporte para probar sobre una COPIA de la ISO real:

Windows 11 26H2
Build 26300.9278
x64
es-ES
Pro
Index 2

IMPORTANTE:

NO modificar la ISO original.

Crear un workspace temporal.

Aplicar:

- TPM bypass;
- Secure Boot bypass;
- CPU bypass;
- RAM bypass;
- cuenta local.

OOBE offline solo si P15 confirma que el mecanismo es válido.

Storage NO.

Después validar los archivos modificados.

==================================================
18. NO TOCAR
==================================================

No modificar:

- RemovalEngine;
- RemovalPlanning;
- ComponentCatalog;
- ProfileEngine;
- SecurityOptions;
- catálogo de componentes;
- reglas de protección;
- lógica de eliminación;
- ciclo Mount/Execute/Verify/Commit/Discard de RemovalEngine;
- telemetría de creación de imagen;
- lógica de inventario P09.

==================================================
19. DOCUMENTACIÓN
==================================================

Crear:

prompts/16-resultado.md

Documentar:

- mecanismo utilizado para LabConfig;
- archivos modificados;
- cómo se monta el hive;
- cómo se valida;
- mecanismo de cuenta local;
- resultado de BypassNRO;
- estado del bypass de almacenamiento;
- resultado de prueba;
- limitaciones;
- tests;
- build.

Distinguir claramente:

IMPLEMENTADO
VALIDADO
PENDIENTE
NO IMPLEMENTADO

==================================================
20. VALIDACIÓN FINAL
==================================================

Ejecutar:

dotnet test MRS-Windows-Builder.sln

Después:

dotnet build MRS-Windows-Builder.sln

Esperado:

0 errores
0 warnings

Si se realiza prueba real:

- comprobar Get-MountedWimInfo;
- comprobar que no queda ningún mount de MRS;
- comprobar que la ISO original no ha cambiado;
- comprobar que el workspace queda consistente.

Al finalizar indicar:

- tests X/X;
- build;
- archivos modificados;
- mecanismos implementados;
- mecanismos pendientes;
- resultado de validación;
- resultado de prueba real;
- commit recomendado.

NO realizar cambios fuera del alcance de este prompt.