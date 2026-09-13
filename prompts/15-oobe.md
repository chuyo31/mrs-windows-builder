PROMPT 15 — OOBE LOCAL, CUENTA MICROSOFT Y BYPASS DE REQUISITOS

Objetivo:
Diseñar e implementar de forma modular la configuración de la ISO para permitir instalar Windows 11 Pro en equipos no compatibles y completar OOBE con cuenta local, sin depender obligatoriamente de una cuenta Microsoft ni de conexión a Internet.

IMPORTANTE:
Esta fase es INDEPENDIENTE del catálogo de componentes y de RemovalEngine.

NO eliminar componentes de OOBE.
NO eliminar Windows Update.
NO eliminar Defender.
NO modificar RemovalPlan.
NO modificar la lógica de eliminación de componentes.

La finalidad es modificar el comportamiento del INSTALADOR/OOBE mediante mecanismos de instalación y configuración apropiados.

==================================================
1. REQUISITOS FUNCIONALES
==================================================

La ISO final debe poder:

- instalar Windows 11 Pro;
- permitir cuenta local;
- no obligar a iniciar sesión con cuenta Microsoft;
- permitir continuar OOBE sin conexión a Internet;
- evitar que los requisitos de hardware compatibles bloqueen la instalación cuando el usuario ha elegido permitirlos.

Requisitos de hardware a contemplar:

- TPM 2.0
- Secure Boot
- CPU compatible
- RAM mínima
- almacenamiento mínimo

MUY IMPORTANTE:

El objetivo específico del proyecto es permitir instalaciones en equipos antiguos con aproximadamente 2 GB de RAM.

No asumir que cambiar un único valor de registro es suficiente.

Investigar primero qué comprobaciones realiza realmente el instalador de Windows 11 26H2 y qué mecanismos son necesarios.

==================================================
2. SEPARAR LOS CONCEPTOS
==================================================

Crear una configuración independiente de:

InstallationOptions / CompatibilityOptions / nombre equivalente apropiado.

No mezclarla con:

SecurityOptions
ProfileDefinition
ComponentDefinition
RemovalPlan

Debe ser una capacidad propia de la ISO.

Como mínimo contemplar:

- AllowLocalAccount
- AllowOfflineOobe
- BypassTpm
- BypassSecureBoot
- BypassCpu
- BypassRam
- BypassStorage

Los nombres pueden adaptarse a la arquitectura existente.

Valores seguros por defecto para el proyecto:

- cuenta local: habilitada;
- OOBE offline: habilitado;
- bypass TPM: habilitado;
- bypass Secure Boot: habilitado;
- bypass CPU: habilitado;
- bypass RAM: habilitado;
- bypass almacenamiento: habilitado.

Pero NO asumir que todos los bypasses se pueden implementar exactamente igual.

==================================================
3. INVESTIGACIÓN PREVIA OBLIGATORIA
==================================================

Antes de modificar código:

Auditar el flujo actual de ISOEngine y PostInstall.

Determinar:

- dónde se encuentra boot.wim;
- dónde se encuentra install.wim;
- dónde se encuentra WinPE/Setup;
- qué archivos/controladores/configuración utiliza Setup;
- qué mecanismos son adecuados para modificar el comportamiento de OOBE;
- qué mecanismos son adecuados para omitir las comprobaciones de compatibilidad.

Revisar específicamente si el mecanismo debe aplicarse:

- en boot.wim;
- en install.wim;
- mediante autounattend.xml;
- mediante $OEM$;
- mediante Setup configuration;
- mediante registro;
- mediante scripts;
- o mediante una combinación controlada.

NO implementar una solución simplemente porque funcionaba en versiones anteriores de Windows.

La ISO objetivo es:

Windows 11 26H2
Build 26300.9278
x64
es-ES
Pro

==================================================
4. CUENTA MICROSOFT / OOBE
==================================================

Diseñar una solución que permita:

OOBE:
→ configuración regional
→ teclado
→ red opcional
→ cuenta local
→ contraseña opcional
→ escritorio

sin exigir:

→ cuenta Microsoft
→ conexión obligatoria a Internet

Evitar hacks frágiles dependientes exclusivamente de una pantalla concreta de OOBE.

Preferir mecanismos soportados por Windows Setup cuando existan.

Si una técnica es específica de una build y puede cambiar en futuras versiones, documentarlo.

==================================================
5. BYPASS DE HARDWARE
==================================================

Implementar de forma modular los bypasses:

TPM
Secure Boot
CPU
RAM
Storage

Cada bypass debe tener una opción independiente.

No hacer un único "BypassEverything" internamente.

Debe ser posible en el futuro ofrecer:

☑ Omitir requisito TPM
☑ Omitir requisito Secure Boot
☑ Omitir requisito CPU
☑ Omitir requisito RAM
☑ Omitir requisito almacenamiento

==================================================
6. RAM 2 GB
==================================================

Este punto es especialmente importante.

El proyecto debe intentar permitir instalación en máquinas con 2 GB de RAM.

Investigar y determinar:

- si el bloqueo se produce durante Setup;
- si se produce durante WinPE;
- si se produce durante OOBE;
- si se trata de una comprobación de memoria independiente;
- si existe más de una comprobación.

No declarar "2 GB soportados" simplemente porque Setup arranque.

Documentar claramente:

"bypass del requisito de instalación de RAM"

vs.

"Windows 11 funcionando correctamente con 2 GB".

El segundo NO debe prometerse.

El objetivo de esta fase es eliminar el bloqueo del instalador cuando sea técnicamente posible.

==================================================
7. NUEVA CAPA DE CONFIGURACIÓN
==================================================

Crear una capa apropiada, por ejemplo:

MRS.InstallationOptions

o integrarla en una capa existente si la arquitectura actual lo permite.

Debe ser independiente de:

MRS.RemovalPlanning
MRS.RemovalEngine
MRS.ComponentCatalog

Crear modelos, servicio y validación apropiados.

No añadir dependencias circulares.

==================================================
8. UI
==================================================

NO crear todavía una interfaz enorme.

Añadir inicialmente una sección clara en la configuración de generación de ISO:

OPCIONES DE INSTALACIÓN

☑ Permitir cuenta local
☑ Permitir instalación sin conexión

COMPATIBILIDAD DE HARDWARE

☑ Omitir requisito TPM 2.0
☑ Omitir requisito Secure Boot
☑ Omitir requisito de CPU
☑ Omitir requisito de RAM
☑ Omitir requisito de almacenamiento

Información:

"Estas opciones permiten utilizar la ISO en equipos que no cumplen los requisitos oficiales de Windows 11."

Para RAM:

"El bypass permite superar la comprobación de instalación. No garantiza un funcionamiento fluido con 2 GB de RAM."

La UI debe aparecer solamente en el flujo donde tenga sentido, no en la selección de componentes.

==================================================
9. ISO ENGINE
==================================================

Preparar ISOEngine para poder aplicar estas configuraciones.

IMPORTANTE:

No destruir la ISO original.

Trabajar siempre sobre un workspace de generación.

Mantener trazabilidad:

Source ISO
→ Workspace
→ modificaciones
→ ISO final

Registrar cada modificación.

Ejemplo:

[INSTALL] Local account enabled
[INSTALL] Offline OOBE enabled
[COMPAT] TPM bypass enabled
[COMPAT] Secure Boot bypass enabled
[COMPAT] CPU bypass enabled
[COMPAT] RAM bypass enabled
[COMPAT] Storage bypass enabled

==================================================
10. VALIDACIÓN
==================================================

Crear una fase de validación antes de generar la ISO final.

Comprobar:

- archivos esperados;
- índices;
- arquitectura;
- existencia de boot.wim;
- existencia de install.wim;
- configuración aplicada;
- coherencia de opciones;
- ausencia de modificaciones en la ISO original.

Si falta algún elemento necesario:

ABORTAR

No generar una ISO aparentemente válida pero incompleta.

==================================================
11. SEGURIDAD / SERVICING
==================================================

NO modificar:

- Servicing Stack
- CBS
- LCU
- WinRE
- Windows Update
- Defender
- networking
- drivers

por el simple hecho de implementar los bypasses.

Los bypasses de instalación deben estar separados del recorte de componentes.

==================================================
12. TESTS
==================================================

Añadir tests para:

- valores por defecto;
- combinación de opciones;
- serialización/deserialización;
- validación;
- todos los bypasses independientes;
- cuenta local;
- OOBE offline;
- RAM;
- TPM;
- Secure Boot;
- CPU;
- Storage.

Comprobar especialmente:

- no se confunden SecurityOptions con InstallationOptions;
- no se generan configuraciones parciales inválidas;
- desactivar una opción no activa otra;
- configuración determinista.

==================================================
13. NO HACER TODAVÍA
==================================================

NO implementar todavía:

- PostInstall de .NET;
- lanzamiento de PCPI;
- generación definitiva con oscdimg;
- compresión final;
- limpieza final de ISO;
- eliminación adicional de componentes.

Eso será posteriormente.

Esta fase debe dejar preparado el motor de instalación/compatibilidad.

==================================================
14. DOCUMENTACIÓN
==================================================

Crear:

prompts/15-resultado.md

Documentar:

- mecanismos encontrados;
- archivos afectados;
- qué se puede implementar de forma fiable;
- limitaciones de Windows 11 26H2;
- qué significa realmente el bypass de 2 GB;
- qué queda pendiente;
- tests;
- build.

IMPORTANTE:

Si alguna de las técnicas investigadas NO funciona de forma fiable en Windows 11 26H2, NO ocultarlo.

Es preferible dejar una opción pendiente/documentada que implementar un hack frágil.

==================================================
15. VALIDACIÓN FINAL
==================================================

Ejecutar:

dotnet test MRS-Windows-Builder.sln

Después:

dotnet build MRS-Windows-Builder.sln

Esperado:

0 errores
0 warnings

Al finalizar indicar:

- tests X/X
- build
- archivos modificados
- mecanismos utilizados
- limitaciones encontradas
- prueba recomendada en VM
- commit recomendado.

NO hacer cambios fuera del alcance de este prompt.