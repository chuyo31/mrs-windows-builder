PROMPT 17 — VALIDACIÓN REAL DE BOOT.WIM, LABCONFIG Y OOBE

Objetivo:
Validar sobre una ISO real de Windows 11 26H2 que la implementación realizada en P16 funciona realmente con DISM y Setup, antes de continuar con PostInstall y generación definitiva de ISO.

IMPORTANTE:
Esta fase NO debe añadir nuevas funcionalidades salvo correcciones necesarias para que P16 funcione realmente.

La prioridad es VALIDAR.

ISO objetivo:

Windows 11 26H2
Build 26300.9278
x64
es-ES
Windows 11 Pro
Index 2

==================================================
1. EJECUCIÓN ELEVADA
==================================================

La prueba debe ejecutarse desde una sesión con privilegios de administrador.

Antes de comenzar:

- comprobar que el proceso tiene privilegios elevados;
- si no está elevado, abortar con mensaje claro;
- NO intentar continuar y después interpretar Error 740 como fallo de implementación.

Registrar:

[VALIDATION] Administrator privileges: OK

==================================================
2. ISO ORIGINAL
==================================================

Localizar una ISO real de Windows 11 disponible.

NO modificar la ISO original.

Montarla en solo lectura si es necesario.

Registrar:

[VALIDATION] Source ISO
[VALIDATION] Install.wim
[VALIDATION] Boot.wim
[VALIDATION] Pro index

Antes de modificar nada, calcular si resulta conveniente una huella/hash de boot.wim para demostrar posteriormente que la ISO original no cambió.

==================================================
3. WORKSPACE
==================================================

Crear un workspace temporal completamente independiente.

Ejemplo conceptual:

source/
boot/
mount/
logs/
output/

Copiar boot.wim al workspace.

Verificar:

- source boot.wim existe;
- working boot.wim existe;
- source y working son rutas distintas;
- workspace no está dentro de la ISO;
- no hay mounts previos de MRS asociados al workspace.

==================================================
4. LABCONFIG — PRUEBA REAL
==================================================

Ejecutar sobre el working boot.wim:

Mount-Wim
    ↓
cargar SYSTEM hive
    ↓
crear/modificar:

HKLM\SYSTEM\Setup\LabConfig

Aplicar:

BypassTPM
BypassSecureBoot
BypassCPU
BypassRAM

según InstallationOptions.

Después:

- leer valores;
- comprobar que son DWORD;
- comprobar valor 1;
- descargar hive;
- desmontar boot.wim con Commit.

NO dejar ningún hive cargado.

NO dejar ningún WIM montado.

==================================================
5. VALIDACIÓN POST-COMMIT
==================================================

Volver a inspeccionar el boot.wim de trabajo.

Comprobar que los cambios persisten después de:

Unmount-Wim /Commit

No considerar suficiente:

"DISM devolvió ExitCode 0".

Debe comprobarse que el resultado realmente contiene LabConfig.

==================================================
6. IDEMPOTENCIA REAL
==================================================

Ejecutar una segunda aplicación sobre una copia/estado equivalente.

Comprobar que:

- no aparecen claves duplicadas;
- no se corrompe el hive;
- no se rompe boot.wim;
- las claves desactivadas se eliminan;
- el resultado es determinista.

Probar especialmente:

Primera ejecución:

TPM = true
SecureBoot = true
CPU = true
RAM = true

Segunda ejecución:

TPM = true
SecureBoot = false
CPU = true
RAM = false

Comprobar que Secure Boot y RAM no permanecen accidentalmente.

==================================================
7. CUENTA LOCAL
==================================================

Validar el autounattend.xml generado.

Comprobar:

- XML válido;
- estructura correcta;
- configuración de cuenta local;
- no contiene contraseña hardcoded;
- no contiene credenciales reales;
- configuración determinista.

Si el mecanismo requiere una ubicación concreta en la ISO:

validar esa ubicación.

NO modificar install.wim para solucionar algo que corresponda a Setup/OOBE si no es necesario.

==================================================
8. OOBE OFFLINE / BYPASSNRO
==================================================

Realizar la prueba específica para:

AllowOfflineOobe

Determinar en la build:

26300.9278

si el mecanismo elegido sigue funcionando.

IMPORTANTE:

No marcar BypassNRO como "validado" solo porque el archivo o registro exista.

Debe existir evidencia de que permite completar OOBE sin conexión.

Si la VM permite probarlo:

- iniciar sin red;
- comprobar que Setup/OOBE no obliga a cuenta Microsoft;
- comprobar que se puede crear cuenta local;
- completar OOBE.

Si no puede validarse completamente:

dejarlo como:

IMPLEMENTADO — NO VALIDADO EN OOBE REAL

No afirmar que funciona.

==================================================
9. PRUEBA DE RAM
==================================================

Si el entorno de virtualización permite crear una VM con 2 GB:

crear una VM de prueba con:

- 2 GB RAM;
- configuración de CPU/TPM/Secure Boot incompatible según las opciones;
- disco suficiente para la instalación;
- ISO generada por esta fase.

Objetivo:

comprobar si Setup supera la comprobación de RAM.

IMPORTANTE:

El resultado debe expresarse como:

"Setup supera/no supera la comprobación de RAM"

NO afirmar:

"Windows 11 funciona correctamente con 2 GB"

a menos que exista una prueba real de funcionamiento.

Si la VM no permite realizar la prueba:

documentarlo.

==================================================
10. TPM / SECURE BOOT / CPU
==================================================

Preparar pruebas separadas cuando sea posible.

Comprobar que las opciones permiten superar las comprobaciones correspondientes.

No probar todas simultáneamente como única evidencia.

Idealmente:

Test A:
TPM incompatible

Test B:
Secure Boot incompatible

Test C:
CPU incompatible

Test D:
RAM = 2 GB

Documentar resultados individualmente.

==================================================
11. STORAGE
==================================================

NO implementar ni activar el bypass de almacenamiento.

Debe seguir bloqueado.

Si InstallationOptions recibe:

BypassStorage = true

el servicio debe abortar antes de modificar nada.

Comprobar este comportamiento.

==================================================
12. LIMPIEZA
==================================================

Después de cada prueba:

- comprobar Get-MountedWimInfo;
- comprobar que no queda ningún mount de MRS;
- comprobar que no queda hive cargado;
- desmontar ISO cuando corresponda;
- conservar logs de prueba.

NO utilizar:

/Cleanup-Mountpoints

como mecanismo normal de limpieza.

NO borrar manualmente directorios de mount mientras DISM los considere montados.

==================================================
13. INTEGRIDAD DE LA ISO ORIGINAL
==================================================

Después de todas las pruebas:

comprobar que:

- ISO original sigue intacta;
- boot.wim original no fue modificado;
- install.wim original no fue modificado.

Si se calculó hash antes:

compararlo después.

Registrar:

[VALIDATION] Source ISO unchanged: OK

==================================================
14. PROGRESO
==================================================

Utilizar el progreso P10/P14 si corresponde.

Mostrar fases reales:

0-10   Preparando validación
10-25  Preparando boot.wim
25-45  Montando boot.wim
45-65  Aplicando LabConfig
65-75  Configurando OOBE
75-90  Validando imagen
90-100 Limpieza

NO inventar progreso interno de DISM.

==================================================
15. TESTS AUTOMÁTICOS
==================================================

No sustituir las pruebas reales por tests simulados.

Mantener todos los tests existentes.

Ejecutar:

dotnet test MRS-Windows-Builder.sln

Esperado:

342+ / todos OK

Si se añaden tests:

documentar el nuevo total.

Después:

dotnet build MRS-Windows-Builder.sln

Esperado:

0 errores
0 warnings

==================================================
16. CORRECCIONES
==================================================

Si la prueba real descubre un problema:

corregir solamente el problema necesario.

Ejemplos:

- hive no se descarga;
- boot.wim queda montado;
- LabConfig no persiste;
- autounattend se coloca incorrectamente;
- una opción desactivada no se elimina;
- workspace queda inconsistente.

NO hacer refactors grandes.

NO añadir funcionalidades de P18.

==================================================
17. DOCUMENTACIÓN
==================================================

Actualizar:

prompts/17-resultado.md

Debe contener una tabla:

| Funcionalidad | Implementada | Validada realmente |
|---------------|--------------|--------------------|
| Cuenta local | Sí/No | Sí/No |
| OOBE offline | Sí/No | Sí/No |
| TPM bypass | Sí/No | Sí/No |
| Secure Boot bypass | Sí/No | Sí/No |
| CPU bypass | Sí/No | Sí/No |
| RAM bypass | Sí/No | Sí/No |
| 2 GB RAM Setup | Sí/No | Sí/No |
| Storage bypass | No | No |

No marcar "validado" sin evidencia de la prueba.

Documentar:

- ISO utilizada;
- build;
- arquitectura;
- edición;
- comandos relevantes;
- resultado de cada prueba;
- errores encontrados;
- correcciones realizadas;
- integridad de la ISO original;
- estado final de mounts.

==================================================
18. NO TOCAR
==================================================

No modificar:

- RemovalEngine;
- RemovalPlanning;
- ComponentCatalog;
- ProfileEngine;
- SecurityOptions;
- inventario;
- catálogo;
- perfiles;
- lógica de eliminación;
- PostInstall;
- oscdimg;
- generación ISO final.

==================================================
19. RESULTADO FINAL
==================================================

Al finalizar indicar claramente:

### AUTOMÁTICO
- tests X/X
- build OK
- 0 errores
- 0 warnings

### PRUEBA REAL
- ISO utilizada
- Pro index
- boot.wim modificado
- LabConfig verificado
- autounattend verificado
- OOBE offline: validado/no validado
- TPM: validado/no validado
- Secure Boot: validado/no validado
- CPU: validado/no validado
- RAM: validado/no validado
- prueba 2 GB: resultado
- Storage: no implementado

### LIMPIEZA
- mounts MRS: 0
- hives pendientes: 0
- ISO original intacta: Sí/No

### DOCUMENTACIÓN
- prompts/17-resultado.md

Indicar commit recomendado.

NO avanzar a PostInstall ni generación ISO definitiva dentro de este prompt.