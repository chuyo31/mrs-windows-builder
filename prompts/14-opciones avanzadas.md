PROMPT 14 — OPCIONES AVANZADAS DE SEGURIDAD EN LA PANTALLA INICIAL

Objetivo:
Corregir la integración visual de P13.

PROBLEMA ACTUAL:
En la pantalla inicial de MRS Windows Builder, al seleccionar los perfiles LIMPIO o PERSONALIZADO solo cambia el perfil y aparece el mensaje en el registro.

Las opciones avanzadas de:
- Mantener Microsoft Defender
- Mantener Windows Update

solo están disponibles actualmente en la pantalla COMPONENTES.

Queremos que también aparezcan inmediatamente en la pantalla inicial al seleccionar LIMPIO o PERSONALIZADO.

IMPORTANTE:
P13 ya funciona correctamente a nivel de ProfileEngine/ComponentCatalog.
NO rehacer esa arquitectura.
NO eliminar las opciones existentes de COMPONENTES.
Este prompt es principalmente de integración UI y estado.

==================================================
1. COMPORTAMIENTO DE LOS PERFILES
==================================================

MÍNIMO:
- No mostrar opciones avanzadas.
- Defender permanece protegido.
- Windows Update permanece protegido.

LIGERO:
- No mostrar opciones avanzadas.
- Defender permanece protegido.
- Windows Update permanece protegido.

RECOMENDADO:
- No mostrar opciones avanzadas.
- Defender permanece protegido.
- Windows Update permanece protegido.

LIMPIO:
Mostrar:

OPCIONES AVANZADAS

☑ Mantener Microsoft Defender
☑ Mantener Windows Update

PERSONALIZADO:
Mostrar exactamente las mismas opciones.

Los checkboxes deben aparecer automáticamente al seleccionar LIMPIO o PERSONALIZADO.

Al volver a MÍNIMO/LIGERO/RECOMENDADO:
- ocultar las opciones avanzadas;
- restaurar el comportamiento protegido correspondiente;
- no permitir que una configuración anterior de Limpio/Personalizado debilite estos perfiles.

==================================================
2. ESTADO
==================================================

Reutilizar SecurityOptions y EffectiveSecurityOptions existentes de P13.

No crear una segunda representación incompatible de estas opciones.

El estado seleccionado en la pantalla inicial debe conservarse cuando el usuario pulse "Continuar" y llegue a COMPONENTES.

Ejemplo:

LIMPIO
☑ Mantener Microsoft Defender
☐ Mantener Windows Update

Al llegar a COMPONENTES debe continuar exactamente:

Defender = mantener
Windows Update = desactivar

No volver automáticamente a los valores por defecto.

Lo mismo para PERSONALIZADO.

==================================================
3. ADVERTENCIAS
==================================================

Los checkboxes empiezan marcados.

Si el usuario desmarca Defender:

Mostrar una advertencia visual próxima al checkbox:

⚠ Microsoft Defender se desactivará al generar la imagen.

Si desmarca Windows Update:

⚠ Windows Update se desactivará al generar la imagen.

No bloquear la selección.

No implementar todavía la desactivación real.

P13 solo prepara la planificación/protección.
La ejecución real de estas acciones pertenece a una fase posterior.

==================================================
4. DISEÑO VISUAL
==================================================

Mantener exactamente el estilo visual actual de MRS Windows Builder.

En la tarjeta "Perfiles":

[ Mínimo ] [ Ligero ]
[ Recomendado ] [ Limpio ]
[ Personalizado ]

Cuando esté seleccionado LIMPIO o PERSONALIZADO, debajo de los botones mostrar:

────────────────────────────

OPCIONES AVANZADAS

☑ Mantener Microsoft Defender
☑ Mantener Windows Update

⚠ Las opciones desactivadas se aplicarán al generar la imagen.

────────────────────────────

Debe integrarse dentro de la misma tarjeta de perfiles.

No crear una ventana emergente.

No utilizar MessageBox para cada cambio.

La interfaz debe ser clara y compacta.

Si la tarjeta necesita crecer verticalmente, ajustar el layout para que no corte contenido.

==================================================
5. COMPONENTES
==================================================

Mantener las opciones avanzadas que P13 añadió en la pantalla COMPONENTES.

Debe existir una única fuente de verdad para SecurityOptions.

Evitar tener:

InitialScreenSecurityOptions
y
ComponentsSecurityOptions

como estados independientes.

Ambas pantallas deben reflejar el mismo estado.

Si el usuario cambia una opción en COMPONENTES, la pantalla inicial debe quedar sincronizada cuando corresponda.

==================================================
6. CAMBIO DE PERFIL
==================================================

Definir claramente el comportamiento:

Caso A:
LIMPIO seleccionado.

Usuario configura:

☐ Defender
☑ Windows Update

Después pulsa PERSONALIZADO.

Las opciones deben seguir disponibles y conservarse salvo que el comportamiento actual del ProfileEngine indique explícitamente otra cosa.

Caso B:
LIMPIO seleccionado.

Usuario desmarca Defender.

Después pulsa RECOMENDADO.

Debe quedar:

Defender protegido
Windows Update protegido

No debe heredarse la configuración insegura.

Caso C:
RECOMENDADO → LIMPIO.

Al entrar en LIMPIO:
- Defender marcado.
- Windows Update marcado.

Los perfiles seguros siempre parten de valores seguros.

==================================================
7. LOG
==================================================

Mantener el logging existente.

Al cambiar de perfil:

[INFO] [PROFILES] Perfil 'clean' aplicado...

Cuando cambie una opción:

[INFO] [SECURITY] Mantener Microsoft Defender: false

[INFO] [SECURITY] Mantener Windows Update: true

No escribir logs continuamente por cada render de UI.

Solo registrar cambios reales.

==================================================
8. CONTINUAR
==================================================

El botón "Continuar" debe seguir funcionando exactamente como ahora.

Antes de pasar a COMPONENTES:

- conservar profileId;
- conservar SecurityOptions;
- aplicar la configuración al catálogo cuando corresponda;
- no ejecutar todavía modificaciones sobre el WIM.

No inventar ComponentIds.

No ejecutar DISM adicional solamente por cambiar Defender/Windows Update.

==================================================
9. ARQUITECTURA
==================================================

Antes de modificar código, revisar cómo P12/P13 implementaron:

- ApplyProfile()
- SecurityOptions
- EffectiveSecurityOptions
- ComponentCatalogService
- ProtectionEngine
- estado actual de MainWindow.

Reutilizar la infraestructura existente.

No hacer refactors grandes.

No mover lógica a WPF si pertenece a ProfileEngine/ComponentCatalog.

No introducir dependencias circulares.

==================================================
10. TESTS
==================================================

Añadir tests únicamente donde aporten valor.

Como mínimo comprobar:

1. MÍNIMO fuerza Defender protegido.
2. MÍNIMO fuerza Windows Update protegido.
3. LIGERO fuerza Defender protegido.
4. LIGERO fuerza Windows Update protegido.
5. RECOMENDADO fuerza Defender protegido.
6. RECOMENDADO fuerza Windows Update protegido.
7. LIMPIO permite modificar ambas opciones.
8. PERSONALIZADO permite modificar ambas opciones.
9. Cambiar a un perfil seguro restaura la protección.
10. SecurityOptions se conserva entre selección inicial y pantalla COMPONENTES.
11. No se generan ComponentIds inventados.
12. El comportamiento existente de P13 no se rompe.

No crear tests WPF complejos si no existe infraestructura para ello.

==================================================
11. REGRESIÓN P09/P10/P11/P12/P13
==================================================

NO modificar:

- ImageInventoryService salvo que sea estrictamente necesario.
- RemovalPlan
- RemovalEngine
- WorkingImageFactory
- DismRunner
- SafeUnmountAsync
- gestión de workspaces
- progreso P10 de creación de imagen
- ProfileService salvo integración necesaria
- catálogo de componentes existente
- reglas de protección existentes
- lógica de dependencias
- lógica de eliminación.

Mantener intacto el comportamiento seguro de:

Servicing Stack
CBS
SSU
LCU
WinRE
OOBE
Networking
USB
Audio
Printing
Frameworks
etc.

Solo Defender y Windows Update tienen las opciones especiales de P13.

==================================================
12. DOCUMENTACIÓN
==================================================

Crear:

prompts/14-resultado.md

Incluir brevemente:

- problema encontrado;
- solución;
- archivos modificados;
- comportamiento de cada perfil;
- tests;
- resultado del build.

Actualizar README solo si es necesario.

==================================================
13. VALIDACIÓN FINAL
==================================================

Ejecutar:

dotnet test MRS-Windows-Builder.sln

Después:

dotnet build MRS-Windows-Builder.sln

Resultado esperado:

0 errores
0 warnings

Comprobar además visualmente la aplicación.

Prueba manual mínima:

1. Abrir MRS Windows Builder.
2. Seleccionar ISO.
3. Analizar.
4. Seleccionar RECOMENDADO.
   → No aparecen opciones avanzadas.
5. Seleccionar LIMPIO.
   → Aparecen Defender y Windows Update.
6. Desmarcar Defender.
   → Aparece advertencia.
7. Desmarcar Windows Update.
   → Aparece advertencia.
8. Seleccionar PERSONALIZADO.
   → Las opciones siguen disponibles.
9. Volver a RECOMENDADO.
   → Las opciones desaparecen y ambas protecciones quedan bloqueadas/protegidas.
10. Volver a LIMPIO.
   → Las opciones aparecen nuevamente en estado seguro por defecto.
11. Continuar a COMPONENTES.
   → SecurityOptions coincide con la configuración seleccionada.
12. Comprobar que no se ejecuta ninguna eliminación real.

Al finalizar indicar:

- tests X/X
- build OK/ERROR
- archivos modificados
- resultado de la prueba visual
- commit recomendado.

No realizar ningún cambio adicional fuera de este alcance.