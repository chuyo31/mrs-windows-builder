PROMPT 04 — INVENTARIO REAL DEL WIM

OBJETIVO:
Implementar y probar el inventario REAL de una imagen Windows mediante DISM.

ESTADO ACTUAL:

Proyecto:
C:\Users\B3RASCASA\Desktop\MRS-W11PRO-by_chuyo31

La aplicación ya puede:

- solicitar privilegios de administrador mediante requireAdministrator.
- seleccionar una ISO.
- detectar install.wim/install.esd.
- ejecutar DISM.
- detectar Windows 10/11.
- detectar versión/build/arquitectura/idioma.
- detectar las ediciones.
- seleccionar una edición.

La arquitectura actual es:

MainWindow
    ↓
ImageService
    ↓
DismRunner
    ↓
DISM.exe

Existe también:

ImageInventoryService

que prepara el inventario y el montaje temporal.

Tests actuales:
74/74 correctos.

IMPORTANTE:

Esta fase NO elimina nada.

NO modificar componentes.

NO modificar paquetes.

NO modificar registro.

NO ejecutar Cleanup-Image.

NO ejecutar /ResetBase.

NO crear todavía perfiles Normal/Light/Medium/Ultra.

El objetivo es:

MONTAR → LEER → DESMONTAR


==================================================
1. FLUJO REAL
==================================================

Cuando el usuario:

1. selecciona la ISO.
2. pulsa Analizar imagen.
3. selecciona una edición.
4. pulsa Continuar.

la aplicación debe:

Crear workspace único:

%LOCALAPPDATA%\MRS-Windows-Builder\workspaces\<GUID>\

Crear:

source\
mount\
logs\
output\

Copiar SOLO lo estrictamente necesario para trabajar con la imagen.

NO copiar la ISO completa.

Utilizar la ruta del install.wim/install.esd de la ISO.


==================================================
2. COMPROBAR MONTAJES
==================================================

Antes de montar:

ejecutar mediante DismRunner:

DISM /English /Get-MountedWimInfo

Si no hay montajes:

continuar.

Si existen montajes:

- identificar cuáles pertenecen al workspace de MRS.
- no tocar montajes de otras aplicaciones.
- si existe un montaje huérfano propio, intentar recuperarlo de forma controlada.

NO ejecutar /Cleanup-Mountpoints indiscriminadamente.


==================================================
3. MONTAR EL WIM
==================================================

Montar únicamente el índice seleccionado.

Ejemplo conceptual:

DISM /English /Mount-Wim
    /WimFile:"..."
    /Index:2
    /MountDir:"...\mount"
    /ReadOnly

IMPORTANTE:

El índice NO debe estar codificado.

Debe utilizarse el índice seleccionado por el usuario.

En nuestra ISO actual esperamos que Pro sea índice 2, pero el código debe funcionar con cualquier índice.


==================================================
4. INVENTARIO REAL
==================================================

Una vez montada la imagen:

A) PAQUETES

Ejecutar:

DISM /English /Image:"<mount>" /Get-Packages

Guardar:

- Package Identity
- State
- Release Type
- Install Time
- Description si existe.


B) FEATURES

Ejecutar:

DISM /English /Image:"<mount>" /Get-Features

Guardar:

- Feature Name
- State


C) CAPABILITIES

Ejecutar:

DISM /English /Image:"<mount>" /Get-Capabilities

Guardar:

- Capability Identity
- State


D) APPX PROVISIONADAS

Ejecutar:

DISM /English /Image:"<mount>" /Get-ProvisionedAppxPackages

Guardar:

- DisplayName
- PackageName
- Version
- Architecture
- ResourceId
- PublisherId


E) DRIVERS

Ejecutar:

DISM /English /Image:"<mount>" /Get-Drivers

Guardar:

- Published Name
- Original File Name
- Provider
- Class
- Version
- Date
- Boot Critical si está disponible.


==================================================
5. SERVICIOS Y TAREAS
==================================================

NO modificar todavía.

Si es posible obtener información de servicios/tareas de forma 100% segura y offline:

inventariarla.

Si requiere cargar hives o realizar operaciones más complejas:

NO hacerlo todavía.

Dejar preparado para una fase posterior.

No sacrificar estabilidad por obtener estas dos categorías ahora.


==================================================
6. MODELOS
==================================================

Utilizar los modelos existentes:

ImagePackage
ImageFeature
ImageCapability
ProvisionedApp
ImageDriver
ImageInventory

Si es necesario mejorarlos, hacerlo sin romper compatibilidad.

ImageInventory debe contener como mínimo:

Packages
Features
Capabilities
ProvisionedApps
Drivers

Añadir contadores calculados.


==================================================
7. PARSERS
==================================================

Los parsers deben ser independientes de:

- idioma del Windows instalado.
- orden de propiedades.
- líneas vacías.
- campos opcionales.

Utilizar:

/English

para la salida de DISM.

No buscar únicamente textos españoles.

No asumir un número fijo de elementos.


==================================================
8. DESMONTADO
==================================================

ESTO ES CRÍTICO.

El desmontaje debe estar garantizado mediante:

try/finally

Flujo:

try
{
    Mount
    Inventory
}
finally
{
    Unmount /Discard
}

Después:

DISM /English /Get-MountedWimInfo

debe confirmar que nuestro montaje ya no existe.


==================================================
9. SI HAY ERROR
==================================================

Si falla cualquier operación:

- registrar la fase exacta.
- registrar el comando.
- registrar ExitCode.
- registrar stderr/stdout relevante.
- intentar desmontar.
- NO borrar el workspace automáticamente.
- mostrar al usuario la ubicación del workspace.

Ejemplo:

[ERROR] Inventario de paquetes fallido.
[ERROR] ExitCode: 123
[INFO] Intentando desmontar imagen...
[INFO] Workspace conservado:
C:\Users\...\workspaces\<GUID>


==================================================
10. CHECKPOINT SERVICING / 26H2
==================================================

La imagen de prueba es:

Windows 11
26H2
Build 26300.9278
x64
es-ES

Debe asumirse que puede utilizar servicing basado en checkpoints.

NO utilizar:

/ResetBase

NO ejecutar:

/StartComponentCleanup

NO realizar operaciones de limpieza durante esta fase.


==================================================
11. INTERFAZ
==================================================

Al terminar correctamente:

mostrar:

INVENTARIO

Paquetes       XXX
Aplicaciones    XX
Features        XX
Capabilities    XX
Drivers         XX

Categorías:

[Paquetes]
[Aplicaciones]
[Features]
[Capabilities]
[Drivers]

DataGrid:

Nombre | Estado | Detalles

Debe poder seleccionarse cada categoría.

Mostrar un indicador:

"Imagen inventariada correctamente"

El botón:

Continuar

puede quedar habilitado para la siguiente fase, pero NO debe realizar ninguna modificación todavía.


==================================================
12. RENDIMIENTO
==================================================

NO cargar innecesariamente archivos grandes en memoria.

NO copiar la ISO completa.

Ejecutar DISM de forma secuencial y controlada.

Actualizar la UI mediante progreso/log.

La interfaz no debe quedarse aparentemente bloqueada sin información.


==================================================
13. TESTS
==================================================

Añadir tests para:

- ImageInventoryService.
- Mount/Unmount mediante FakeDismRunner.
- orden correcto de operaciones.
- desmontaje cuando el inventario funciona.
- desmontaje cuando falla un comando.
- conservación del workspace en caso de error.
- múltiples paquetes.
- múltiples features.
- múltiples capabilities.
- múltiples AppX.
- múltiples drivers.
- ExitCode != 0.
- parser con campos opcionales.
- parser con campos en diferente orden.

NO utilizar una ISO real en tests unitarios.


==================================================
14. PRUEBA REAL OBLIGATORIA
==================================================

Después de implementar:

Ejecutar:

dotnet build

dotnet test

Ambos deben terminar:

0 errores
0 advertencias
Todos los tests correctos.

Después ejecutar la aplicación elevada.

Utilizar nuestra ISO real:

Windows 11 26H2
26300.9278
es-ES
x64

Seleccionar:

Windows 11 Pro

Pulsar:

Continuar

y realizar el inventario REAL.

IMPORTANTE:

No dar por terminado el trabajo solo porque los tests pasen.

La prueba real debe comprobar:

- montaje real del índice.
- Get-Packages real.
- Get-Features real.
- Get-Capabilities real.
- Get-ProvisionedAppxPackages real.
- Get-Drivers real.
- desmontaje real.
- ausencia de montajes al finalizar.


==================================================
15. COMPROBACIÓN FINAL
==================================================

Después de cerrar el inventario:

ejecutar:

DISM /English /Get-MountedWimInfo

Debe indicar:

No mounted images found.

Si aparece cualquier montaje:

NO continuar con la siguiente fase.

Investigar primero el desmontaje.


==================================================
16. RESULTADO
==================================================

Actualizar:

prompts\04-resultado.md

Incluir:

- archivos creados/modificados.
- arquitectura.
- comandos DISM utilizados.
- resultado de build.
- resultado de tests.
- resultado de la prueba REAL.
- número real de paquetes.
- número real de aplicaciones.
- número real de features.
- número real de capabilities.
- número real de drivers.
- resultado del desmontaje.
- problemas encontrados.

NO incluir explicaciones largas.

NO implementar todavía:

- eliminación.
- perfiles.
- catálogo de componentes.
- dependencias.
- protección.
- limpieza.
- compresión.
- creación de ISO.
- App Packs.
- PCPI.

Esta fase termina cuando MRS Windows Builder pueda montar una imagen real, inventariarla completamente, desmontarla correctamente y demostrar que no ha dejado ningún montaje abierto.