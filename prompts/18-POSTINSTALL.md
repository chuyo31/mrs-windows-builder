PROMPT 18 — POSTINSTALL: .NET 8.0.26 + PCPI

Objetivo:
Implementar de forma modular y real el sistema PostInstall de MRS Windows Builder para que una ISO generada por MRS pueda instalar automáticamente:

1. Microsoft .NET Desktop Runtime 8.0.26 x64
2. PCPI-Retro-Minimals-Portable-0.0.5.exe

El objetivo es dejar preparada la infraestructura que posteriormente utilizará ISOEngine para construir la ISO final.

IMPORTANTE:
NO generar todavía la ISO final.
NO ejecutar nada sobre la ISO original.
NO modificar RemovalEngine.
NO modificar ComponentCatalog.
NO modificar ProfileEngine.
NO modificar InstallationOptions salvo integración estrictamente necesaria.

==================================================
1. RESULTADO ESPERADO
==================================================

Después de instalar Windows desde una ISO generada por MRS:

Windows inicia
    ↓
PostInstall
    ↓
instala .NET Desktop Runtime 8.0.26 x64
    ↓
comprueba que la instalación ha terminado correctamente
    ↓
ejecuta PCPI-Retro-Minimals-Portable-0.0.5.exe
    ↓
registra el resultado
    ↓
finaliza

No asumir que PCPI existe en una ruta concreta del ordenador del desarrollador.

Todo debe viajar con la ISO/workspace.

==================================================
2. NUEVO MÓDULO
==================================================

Utilizar el proyecto existente:

MRS.PostInstall

Si ya existe parcialmente, auditarlo antes de modificarlo.

Debe permanecer desacoplado de WPF.

No introducir referencias innecesarias a:

MRS.WindowsBuilder
MRS.RemovalEngine
MRS.ComponentCatalog

La lógica debe poder probarse sin ejecutar realmente instaladores.

==================================================
3. MODELOS
==================================================

Crear modelos apropiados para representar:

PostInstallConfiguration

Debe permitir configurar como mínimo:

- habilitar/deshabilitar PostInstall;
- runtime .NET;
- versión del runtime;
- arquitectura;
- paquete PCPI;
- ejecutar PCPI después del runtime.

No hardcodear rutas absolutas del ordenador del desarrollador.

Los nombres y estructura pueden adaptarse a la arquitectura existente.

==================================================
4. .NET DESKTOP RUNTIME
==================================================

Objetivo:

Instalar:

Microsoft .NET Desktop Runtime 8.0.26
x64

Usar el instalador offline.

NO descargar nada durante la instalación de Windows.

El instalador debe estar incluido en el workspace/ISO.

El nombre/ruta interna debe ser configurable.

Ejemplo conceptual:

postinstall/
    dotnet/
        windowsdesktop-runtime-8.0.26-win-x64.exe
    pcpi/
        PCPI-Retro-Minimals-Portable-0.0.5.exe

NO asumir que esos archivos existen todavía.

El sistema debe detectar archivos ausentes antes de generar la ISO.

==================================================
5. INSTALACIÓN SILENCIOSA
==================================================

Preparar ejecución equivalente a:

windowsdesktop-runtime-8.0.26-win-x64.exe /install /quiet /norestart

Pero NO hardcodear la línea en múltiples lugares.

Crear un modelo/servicio que represente:

- executable;
- arguments;
- working directory;
- timeout;
- expected exit codes.

Éxito debe determinarse mediante ExitCode.

No considerar:

"el proceso terminó"

como éxito.

==================================================
6. PCPI
==================================================

Después de instalar .NET correctamente:

ejecutar:

PCPI-Retro-Minimals-Portable-0.0.5.exe

NO ejecutarlo antes de confirmar que .NET ha terminado correctamente.

Utilizar una ruta relativa al contenido PostInstall.

NO utilizar:

C:\Users\B3RASCASA\...
C:\Users\...
Desktop
Downloads

ni ninguna ruta específica del equipo de desarrollo.

==================================================
7. CONTROL DE ERRORES
==================================================

Si .NET falla:

NO lanzar PCPI.

Registrar:

[POSTINSTALL] .NET installation failed
[POSTINSTALL] ExitCode: X

Si .NET termina correctamente:

[POSTINSTALL] .NET installation completed

Si PCPI falla:

registrar:

[POSTINSTALL] PCPI launch failed
[POSTINSTALL] ExitCode: X

No interpretar automáticamente un cierre de PCPI como error si su ejecutable no devuelve un código convencional.

Documentar el comportamiento.

==================================================
8. EJECUCIÓN DE PCPI
==================================================

Determinar si PCPI debe:

- ejecutarse y esperar;
- ejecutarse y continuar;
- ejecutarse una única vez.

Preferencia inicial:

ejecutarlo una vez después de completar la instalación del runtime.

Evitar bucles o relanzamientos infinitos.

==================================================
9. MECANISMO WINDOWS POSTINSTALL
==================================================

Investigar y seleccionar el mecanismo adecuado para ejecutar PostInstall después de instalar Windows.

Comparar técnicamente, antes de decidir:

- SetupComplete.cmd;
- $OEM$\$$\Setup\Scripts;
- FirstLogonCommands;
- RunOnce;
- mecanismo equivalente.

Preferir el mecanismo más apropiado para:

- instalación automática;
- funcionamiento sin Internet;
- cuenta local;
- instalación desde ISO;
- ejecución una sola vez;
- compatibilidad con Windows 11 26H2.

IMPORTANTE:

No elegir simplemente el mecanismo porque sea conocido de versiones antiguas.

Documentar la decisión.

==================================================
10. CUENTA LOCAL / OOBE
==================================================

PostInstall debe ser compatible con la arquitectura de P15/P16.

No asumir que existe un usuario concreto.

No introducir:

- usuario hardcodeado;
- contraseña;
- SID;
- nombre de perfil;
- ruta C:\Users\...

Si el mecanismo elegido requiere ejecutar algo después del primer inicio de sesión, documentarlo y justificarlo.

==================================================
11. ARCHIVOS POSTINSTALL
==================================================

Crear una estructura preparada para ISOEngine.

Ejemplo:

$OEM$/
└── $$/
    └── Setup/
        └── Scripts/
            └── ...

No asumir que esta estructura exacta es obligatoria: determinar primero el mecanismo elegido.

El resultado debe poder copiarse posteriormente al workspace de generación.

==================================================
12. GENERADOR
==================================================

Crear un:

PostInstallPackageBuilder

o nombre equivalente.

Responsabilidad:

- validar los paquetes;
- crear estructura de archivos;
- copiar runtime;
- copiar PCPI;
- generar scripts/configuración;
- producir un paquete PostInstall listo para integrarlo en una ISO.

No debe generar la ISO.

Entrada:

PostInstallConfiguration
+ archivos instaladores

Salida:

directorio PostInstall preparado.

==================================================
13. VALIDACIÓN PREVIA
==================================================

Antes de preparar el paquete:

comprobar:

- runtime existe;
- versión esperada;
- arquitectura x64;
- PCPI existe;
- nombres/rutas válidos;
- no hay rutas absolutas del desarrollador;
- configuración coherente.

Si falta .NET:

ABORTAR.

Si falta PCPI:

ABORTAR.

No crear una ISO posteriormente con PostInstall incompleto.

==================================================
14. INTEGRACIÓN CON ISOENGINE
==================================================

Preparar una interfaz limpia para que posteriormente ISOEngine pueda hacer:

ISO workspace
    ↓
PostInstallPackageBuilder
    ↓
copiar contenido PostInstall
    ↓
ISO final

NO implementar todavía toda esta integración.

Solo dejar la API necesaria.

==================================================
15. PROGRESO
==================================================

Reutilizar el sistema de progreso existente cuando sea apropiado.

Fases:

0-10   Validando paquetes
10-25  Preparando .NET
25-50  Preparando PCPI
50-70  Generando configuración
70-90  Validando PostInstall
90-100 Finalizando

No simular progreso de procesos que todavía no se están ejecutando.

==================================================
16. LOG
==================================================

Utilizar logging estructurado.

Ejemplo:

[POSTINSTALL] Preparing package
[POSTINSTALL] Runtime: .NET Desktop Runtime 8.0.26 x64
[POSTINSTALL] PCPI: PCPI-Retro-Minimals-Portable-0.0.5.exe
[POSTINSTALL] Validation: OK
[POSTINSTALL] Package prepared successfully

Durante ejecución futura:

[POSTINSTALL] Installing .NET
[POSTINSTALL] .NET ExitCode: 0
[POSTINSTALL] Launching PCPI

==================================================
17. SEGURIDAD
==================================================

No almacenar:

- contraseñas;
- tokens;
- credenciales;
- datos personales.

No descargar ejecutables desde Internet durante el build ni durante PostInstall.

Los ejecutables deben ser suministrados por el usuario/proyecto.

==================================================
18. TESTS
==================================================

Añadir tests para:

### Configuration

- valores por defecto;
- deshabilitar PostInstall;
- versión 8.0.26;
- x64;
- configuración inválida.

### Package validation

- .NET inexistente;
- PCPI inexistente;
- ambos existentes;
- rutas absolutas prohibidas;
- archivos duplicados;
- workspace inválido.

### Command execution

- ExitCode 0;
- ExitCode distinto de 0;
- argumentos;
- timeout;
- cancelación.

### Orden

Comprobar que:

.NET
    ↓
PCPI

y nunca:

PCPI
    ↓
.NET

### Generación

Comprobar que el paquete resultante contiene:

- runtime;
- PCPI;
- script/configuración correspondiente.

### Seguridad

Comprobar que no aparecen rutas como:

C:\Users\B3RASCASA

ni otras rutas específicas del equipo.

==================================================
19. PRUEBA CON ARCHIVOS REALES
==================================================

Si los archivos reales están disponibles en el proyecto:

- windowsdesktop-runtime-8.0.26-win-x64.exe
- PCPI-Retro-Minimals-Portable-0.0.5.exe

utilizarlos para validar:

- existencia;
- tamaño;
- hash si resulta útil;
- copia al workspace;
- estructura PostInstall.

NO ejecutar automáticamente los instaladores sobre el equipo de desarrollo.

La validación debe ser de empaquetado.

==================================================
20. NO TOCAR
==================================================

No modificar:

- RemovalEngine;
- RemovalPlanning;
- ComponentCatalog;
- ProfileEngine;
- SecurityOptions;
- InstallationOptions;
- ImageInventoryService;
- WorkingImageFactory;
- LabConfig;
- boot.wim;
- generación ISO definitiva.

InstallationOptions solo puede tocarse si existe una integración estrictamente necesaria y no cambia su semántica.

==================================================
21. DOCUMENTACIÓN
==================================================

Crear:

prompts/18-resultado.md

Documentar:

- mecanismo PostInstall elegido;
- por qué se eligió;
- estructura generada;
- cómo se ejecutará después de Windows;
- .NET 8.0.26;
- PCPI;
- validaciones;
- limitaciones;
- tests;
- build.

Distinguir:

IMPLEMENTADO
VALIDADO
PENDIENTE DE PRUEBA EN VM

==================================================
22. VALIDACIÓN FINAL
==================================================

Ejecutar:

dotnet test MRS-Windows-Builder.sln

Después:

dotnet build MRS-Windows-Builder.sln

Esperado:

0 errores
0 warnings

Si se encuentran problemas de arquitectura:

corregirlos sin realizar refactors no relacionados.

Al finalizar indicar:

- tests X/X;
- build;
- archivos modificados;
- mecanismo PostInstall elegido;
- estado de .NET;
- estado de PCPI;
- prueba con archivos reales;
- limitaciones;
- commit recomendado.

NO generar todavía la ISO final.