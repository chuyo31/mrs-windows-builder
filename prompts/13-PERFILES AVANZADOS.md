PROMPT 13 — PERFILES AVANZADOS Y OPCIONES DE SEGURIDAD

Proyecto:
C:\Users\B3RASCASA\Desktop\MRS-W11PRO-by_chuyo31

Contexto:
P12 está terminado.
Los 5 perfiles existentes son:

minimal
light
recommended
clean
custom

Actualmente los JSON tienen componentIds vacíos porque los ComponentId reales proceden del inventario de la ISO.

Objetivo de P13:
Diseñar e implementar el sistema de configuración avanzada de perfiles para que:

- MÍNIMO, LIGERO y RECOMENDADO mantengan siempre Defender y Windows Update.
- LIMPIO y PERSONALIZADO permitan al usuario decidir si mantener o desactivar Defender y Windows Update.
- Esta decisión debe integrarse con la arquitectura existente de ProfileEngine + ComponentCatalog + ProtectionEngine + RemovalPlanning.
- NO ejecutar todavía ninguna desactivación real de Defender/Windows Update sobre una imagen.

IMPORTANTE:
No eliminar simplemente las protecciones existentes.
La protección debe convertirse en una decisión explícita y segura basada en la configuración del perfil.

==================================================
1. MODELO DE OPCIONES
==================================================

Crear una configuración de perfil equivalente a:

SecurityOptions
{
    KeepDefender
    KeepWindowsUpdate
}

Valores por defecto:

minimal:
    KeepDefender = true
    KeepWindowsUpdate = true

light:
    KeepDefender = true
    KeepWindowsUpdate = true

recommended:
    KeepDefender = true
    KeepWindowsUpdate = true

clean:
    KeepDefender = true
    KeepWindowsUpdate = true

custom:
    KeepDefender = true
    KeepWindowsUpdate = true

IMPORTANTE:
En clean/custom los valores deben poder modificarse posteriormente desde la UI.

No utilizar strings mágicos si puede evitarse.
Usar enums/modelos tipados.

==================================================
2. REGLAS DE SEGURIDAD
==================================================

MÍNIMO / LIGERO / RECOMENDADO:

Defender y Windows Update deben permanecer siempre protegidos.

Aunque una selección intentase eliminarlos:

    ProtectionEngine → BLOCK

No debe existir ninguna forma accidental de saltarse esta protección.

LIMPIO / PERSONALIZADO:

Si KeepDefender = true:

    Defender → protegido

Si KeepDefender = false:

    Defender → permitido para una futura estrategia de desactivación

Si KeepWindowsUpdate = true:

    Windows Update → protegido

Si KeepWindowsUpdate = false:

    Windows Update → permitido para una futura estrategia de desactivación

IMPORTANTE:

"permitido" NO significa "eliminar ahora".

P13 solo debe cambiar la decisión de protección/selección.
La implementación concreta de desactivación se realizará en una fase posterior y específica.

==================================================
3. PERFIL + CONFIGURACIÓN
==================================================

Extender ProfileDefinition de forma compatible con los perfiles actuales.

Debe ser posible representar:

ProfileDefinition
    Id
    Name
    Description
    ComponentIds
    SecurityOptions

Mantener compatibilidad con JSON existentes.

Si SecurityOptions no existe en un JSON antiguo:
    usar valores seguros por defecto.

Es decir:

KeepDefender = true
KeepWindowsUpdate = true

==================================================
4. UI
==================================================

En la pantalla COMPONENTES, cuando esté seleccionado:

MÍNIMO
LIGERO
RECOMENDADO

mostrar visualmente:

🛡 Microsoft Defender       PROTEGIDO
🔄 Windows Update           PROTEGIDO

Sin controles modificables.

Para LIMPIO y PERSONALIZADO mostrar una sección:

OPCIONES AVANZADAS

Seguridad

☑ Mantener Microsoft Defender

☑ Mantener Windows Update

Las dos opciones deben empezar activadas.

Si el usuario desmarca Defender:

mostrar advertencia clara:

"Microsoft Defender quedará desactivado en la configuración del perfil."

Si desmarca Windows Update:

"Windows Update quedará desactivado en la configuración del perfil."

No ejecutar ninguna acción sobre Windows ni sobre la imagen al cambiar estos controles.

Solo modificar la configuración del perfil actual.

==================================================
5. PROTECTION ENGINE
==================================================

Integrar la nueva configuración sin romper el comportamiento existente.

La regla general debe ser:

KeepDefender = true
    → Defender protegido

KeepDefender = false
    → Defender ya no queda bloqueado por esta protección específica

KeepWindowsUpdate = true
    → Windows Update protegido

KeepWindowsUpdate = false
    → Windows Update ya no queda bloqueado por esta protección específica

IMPORTANTE:

Las dependencias críticas y protecciones estructurales deben continuar funcionando.

No eliminar ni debilitar otras protecciones:

- Servicing Stack
- CBS
- LCU
- WinRE
- OOBE
- Frameworks necesarios
- red
- USB
- audio
- Bluetooth
- impresión
- componentes necesarios para el arranque
- etc.

No convertir "KeepDefender=false" en "desproteger todo lo relacionado con seguridad".

Debe existir una identificación clara de qué componentes pertenecen a Defender y cuáles a Windows Update.

==================================================
6. COMPONENT CATALOG
==================================================

Revisar las reglas actuales de CatalogClassifier y ProtectionEngine.

No inventar ComponentIds.

Usar las categorías/clasificaciones existentes siempre que sean suficientes.

Si la clasificación actual no permite diferenciar correctamente:

    Defender
    Windows Update

crear una abstracción de categoría/regla reutilizable.

No modificar reglas sin necesidad.

==================================================
7. REMOVAL PLANNING
==================================================

RemovalPlan debe reflejar correctamente:

- protegido
- permitido
- bloqueado
- motivo del bloqueo

No modificar RemovalEngine para ejecutar nuevas eliminaciones.

El plan debe poder representar que Defender/Windows Update han dejado de estar protegidos cuando el usuario lo ha elegido explícitamente.

Pero todavía NO debe ejecutarse una estrategia de desactivación.

==================================================
8. PERFILES JSON
==================================================

Actualizar los 5 JSON existentes para incluir SecurityOptions.

Ejemplo conceptual:

{
  "id": "clean",
  "name": "Limpio",
  "description": "...",
  "componentIds": [],
  "securityOptions": {
    "keepDefender": true,
    "keepWindowsUpdate": true
  }
}

Todos deben comenzar seguros.

No rellenar componentIds inventados.

==================================================
9. TESTS
==================================================

Añadir tests para:

- defaults seguros;
- perfiles con SecurityOptions;
- JSON antiguo sin SecurityOptions;
- Mínimo mantiene Defender;
- Ligero mantiene Defender;
- Recomendado mantiene Defender;
- Limpio permite cambiar Defender;
- Limpio permite cambiar Windows Update;
- Personalizado permite cambiar ambos;
- Defender protegido cuando KeepDefender=true;
- Defender permitido cuando KeepDefender=false;
- Windows Update protegido cuando KeepWindowsUpdate=true;
- Windows Update permitido cuando KeepWindowsUpdate=false;
- otras protecciones críticas siguen intactas;
- no existe ninguna ejecución real de desactivación.

Mantener todos los tests existentes.

Ejecutar:

dotnet test
dotnet build

==================================================
10. NO HACER EN P13
==================================================

NO:

- modificar RemovalEngine para desactivar Defender;
- modificar RemovalEngine para desactivar Windows Update;
- eliminar servicios mediante sc.exe;
- modificar registro de Windows;
- usar PowerShell para desactivar Defender;
- usar políticas locales;
- eliminar paquetes de seguridad automáticamente;
- eliminar componentes de Windows Update automáticamente;
- introducir ResetBase;
- introducir StartComponentCleanup;
- introducir Cleanup-Mountpoints;
- tocar el ciclo Mount → Execute → Verify → Commit/Discard → Unmount;
- cambiar la telemetría P10;
- inventar ComponentIds.

P13 es configuración + planificación + UI + protección.

La implementación real de "desactivar Defender" y "desactivar Windows Update" será una fase posterior independiente.

==================================================
RESULTADO OBLIGATORIO
==================================================

Crear:

prompts/13-resultado.md

Incluir:

- cambios realizados;
- nuevos modelos;
- cambios en ProfileEngine;
- cambios en ProtectionEngine;
- cambios de UI;
- cambios en JSON;
- tests ejecutados;
- resultado de dotnet test;
- resultado de dotnet build;
- cualquier decisión arquitectónica relevante.

Actualizar README si corresponde al estado de P13.

Al finalizar, indicar el commit recomendado.

No hacer cambios fuera del alcance de P13.