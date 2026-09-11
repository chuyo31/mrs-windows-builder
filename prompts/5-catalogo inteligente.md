PROMPT 05 — CATÁLOGO INTELIGENTE, CLASIFICACIÓN Y PROTECCIÓN

OBJETIVO:

Convertir el inventario real obtenido en el Prompt 04 en un catálogo estructurado de componentes Windows.

Esta fase NO debe eliminar componentes.

NO modificar el WIM.

NO ejecutar operaciones destructivas.

NO implementar todavía el motor de eliminación.

El objetivo es crear la base sobre la que posteriormente funcionarán los perfiles Normal, Light, Medium, Ultra y Personalizado.


==================================================
ESTADO ACTUAL
==================================================

Proyecto:

C:\Users\B3RASCASA\Desktop\MRS-W11PRO-by_chuyo31

Arquitectura:

MainWindow
    ↓
ImageService
    ↓
ImageInventoryService
    ↓
DismRunner
    ↓
DISM.exe

Ya existe inventario real.

Prueba real realizada sobre:

Windows 11 Pro
26H2
Build 26300.9278
x64
es-ES

Inventario real:

- 208 paquetes
- 57 Apps provisionadas
- 136 Features
- 424 Capabilities
- 0 drivers de terceros detectados por DISM

El montaje y desmontaje real funcionan correctamente.

Tests actuales: 77/77.


==================================================
PARTE 1 — CORRECCIÓN VISUAL DEL INVENTARIO
==================================================

Antes de añadir funcionalidad nueva, corregir la apariencia del DataGrid de inventario.

Actualmente los encabezados:

Nombre
Estado
Detalles

se muestran con el estilo predeterminado de WPF y tienen muy poco contraste.

Aplicar un estilo coherente con el tema oscuro existente.

DataGrid:

- fondo oscuro.
- filas oscuras.
- texto claro.
- encabezados claramente visibles.
- borde discreto.
- selección azul/acento.
- hover ligeramente diferenciado.
- texto blanco/casi blanco.
- evitar fondos blancos del tema predeterminado de WPF.

Lista lateral:

Paquetes
Apps
Features
Capabilities
Drivers

Debe mantener el estilo oscuro de la aplicación.

No cambiar el diseño general.


==================================================
PARTE 2 — CONCEPTO DE CATÁLOGO
==================================================

Crear un nuevo proyecto/módulo si es necesario:

MRS.ComponentCatalog

El catálogo NO debe depender de la UI.

Debe poder recibir un ImageInventory y producir una colección de ComponentDefinition.


==================================================
PARTE 3 — MODELO ComponentDefinition
==================================================

Crear un modelo similar a:

ComponentDefinition

Propiedades mínimas:

- Id
- Name
- DisplayName
- Description
- Category
- SourceType
- Risk
- RemovalMode
- Protection
- Detected
- Installed
- Superseded
- Dependencies
- Conflicts
- Tags

Enums recomendados:

ComponentCategory:

- System
- Security
- WindowsUpdate
- Store
- Application
- Gaming
- Communication
- AI
- Telemetry
- Media
- Networking
- Printing
- Accessibility
- Development
- Language
- Driver
- Feature
- Capability
- Framework
- Unknown

ComponentRisk:

- Critical
- High
- Medium
- Low
- Unknown

ComponentProtection:

- Protected
- Recommended
- Optional
- Removable
- Unknown

RemovalMode:

- None
- Package
- Appx
- Feature
- Capability
- Registry
- FileSystem
- Task
- Service

IMPORTANTE:

En esta fase RemovalMode es DESCRIPTIVO.

NO ejecutar ninguna eliminación.


==================================================
PARTE 4 — IDENTIFICACIÓN
==================================================

Crear un CatalogClassifier.

Su función:

ImageInventory
    ↓
CatalogClassifier
    ↓
ComponentDefinition


Debe identificar componentes conocidos a partir de:

- Package Identity
- AppX PackageName
- Feature Name
- Capability Identity

NO depender exclusivamente de una coincidencia exacta.

Utilizar reglas de identificación.

Ejemplos conceptuales:

Xbox
GamingApp
Solitaire
→ Gaming

Clipchamp
→ Application

Copilot
→ AI

BingNews
BingWeather
BingSearch
→ Application / Optional

OutlookForWindows
Teams
Skype
PhoneLink
→ Communication

WindowsStore
StorePurchaseApp
Store engagement/framework
→ Store

VCLibs
.NET Native
UI.Xaml
WindowsAppRuntime
→ Framework / Protected

Defender
SecurityHealth
→ Security / Critical / Protected

Windows Update
servicing
CBS
SSU
LCU
→ WindowsUpdate/System / Critical / Protected

Wi-Fi
Bluetooth
Ethernet
Networking
→ Networking / Protected

Printing
Print
XPS
Spooler-related components
→ Printing

Media Foundation
Media Player
codecs
→ Media

IMPORTANTE:

No marcar automáticamente todo lo que coincida con una palabra como eliminable.

La clasificación y la protección son cosas diferentes.


==================================================
PARTE 5 — PROTECCIÓN
==================================================

Crear un ProtectionEngine.

Objetivo:

EVITAR que posteriormente el usuario pueda eliminar accidentalmente componentes críticos.

Debe existir una protección inicial fuerte.

Como mínimo proteger:

- Windows servicing
- Component Based Servicing
- Servicing Stack
- Windows Update
- Defender
- Windows Security
- Microsoft Store
- componentes fundamentales de Store
- Windows Installer
- WinRE
- networking básico
- Wi-Fi
- Ethernet
- Bluetooth
- USB
- audio
- impresión
- .NET/frameworks necesarios
- Windows App Runtime cuando sea dependencia
- VCLibs
- UI.Xaml
- paquetes base del sistema
- paquetes de idioma necesarios
- componentes necesarios para OOBE

La protección debe poder explicar POR QUÉ algo está protegido.

Por ejemplo:

ProtectionReason:

"Componente crítico para Windows Update"

o:

"Framework utilizado por otras aplicaciones AppX"


==================================================
PARTE 6 — ESTADOS
==================================================

No confundir:

Estado DISM

con:

Estado del catálogo.

Por ejemplo:

Package State:
Installed

Catalog:

Protection:
Protected

Risk:
Critical

Esto permite representar:

Installed + Protected
Installed + Optional
Installed + Removable
Superseded + System
etc.


==================================================
PARTE 7 — DEPENDENCIAS
==================================================

Crear una primera estructura de dependencias.

Ejemplo:

Microsoft.Paint
    ↓
VCLibs
    ↓
UI.Xaml

El catálogo NO debe concluir que VCLibs se puede eliminar simplemente porque Paint sea eliminable.

Otro ejemplo:

Store application
    ↓
Store frameworks
    ↓
Protected

Crear:

ComponentDependency

con:

- SourceComponentId
- TargetComponentId
- DependencyType

DependencyType:

- Required
- Runtime
- Framework
- System
- Optional

En esta fase NO necesitamos resolver todas las dependencias automáticamente.

Pero la arquitectura debe estar preparada para ello.


==================================================
PARTE 8 — COMPONENTES PROTEGIDOS
==================================================

Crear un conjunto de identificadores/reglas protegidas.

NO codificar simplemente:

"todo Microsoft-Windows = protegido"

porque sería demasiado general.

Las reglas deben ser específicas y revisables.

Crear un sistema similar a:

ProtectionRule

- Id
- Pattern
- Category
- Reason
- Risk
- Enabled

Ejemplo conceptual:

{
    "id": "windows-servicing",
    "pattern": "ServicingStack",
    "category": "System",
    "risk": "Critical",
    "reason": "Necesario para el mantenimiento de Windows"
}

No hace falta que sea exactamente este JSON todavía.


==================================================
PARTE 9 — CATÁLOGO DINÁMICO
==================================================

MUY IMPORTANTE:

No crear una lista fija de 75 componentes.

El catálogo debe partir de lo que realmente existe en ImageInventory.

Por tanto:

ISO diferente
    ↓
inventario diferente
    ↓
catálogo diferente

Las reglas únicamente clasifican lo detectado.


==================================================
PARTE 10 — CATÁLOGO JSON
==================================================

Preparar almacenamiento de reglas externas.

Crear:

catalog/
    win10/
    win11/
    shared/

Ejemplo:

catalog/win11/protection-rules.json

catalog/win11/components.json

El objetivo es que posteriormente podamos actualizar reglas sin modificar el motor principal.

No hace falta cargar cientos de reglas ahora.

Crear una base pequeña y bien estructurada.


==================================================
PARTE 11 — UI DEL CATÁLOGO
==================================================

Añadir una nueva pantalla después del inventario:

COMPONENTES

Mostrar:

┌─────────────────────────────────────────────────────────┐
│ COMPONENTES                                             │
│                                                         │
│ 🔍 Buscar componente...                                 │
│                                                         │
│ Categoría: [Todas ▼]                                    │
│                                                         │
│ [ ] Clipchamp       Aplicación      🟢 Removible        │
│ [ ] Xbox            Gaming          🟢 Removible        │
│ [ ] Copilot         IA              🟢 Removible        │
│ [🔒] Store          Sistema         🔴 Protegido        │
│ [🔒] VCLibs         Framework       🔴 Protegido        │
│                                                         │
└─────────────────────────────────────────────────────────┘

IMPORTANTE:

Todavía NO debe existir una acción que modifique el WIM.

Las casillas son únicamente de selección/preparación.

Una casilla de componente protegido debe estar:

- bloqueada
- claramente identificada
- mostrando el motivo de protección.


==================================================
PARTE 12 — INFORMACIÓN DETALLADA
==================================================

Al seleccionar un componente mostrar detalles:

Nombre
Categoría
Estado
Riesgo
Protección
Origen
Descripción
Dependencias
Dependientes
Motivo de protección

Ejemplo:

Microsoft.UI.Xaml.2.8

Categoría:
Framework

Estado:
Installed

Riesgo:
Critical

Protección:
Protected

Motivo:
Framework utilizado por aplicaciones Windows modernas.

Dependencias:
...

Utilizado por:
...


==================================================
PARTE 13 — BÚSQUEDA
==================================================

La búsqueda debe encontrar por:

- Id
- Name
- DisplayName
- PackageName
- categoría
- tags

Debe funcionar sin distinguir mayúsculas/minúsculas.


==================================================
PARTE 14 — FILTROS
==================================================

Preparar filtros:

Todos
Protegidos
Removibles
Opcionales
Críticos
Aplicaciones
Features
Capabilities
Frameworks
Gaming
Communication
AI
Telemetry
etc.


==================================================
PARTE 15 — PERFIL
==================================================

NO implementar todavía los perfiles.

Pero preparar el modelo para:

Normal
Light
Medium
Ultra
Custom

Los perfiles se implementarán en el siguiente paso.


==================================================
PARTE 16 — SEGURIDAD
==================================================

Regla fundamental:

El catálogo NO ejecuta DISM.

El catálogo clasifica.

El motor DISM será utilizado posteriormente por el RemovalEngine.

Arquitectura:

Inventory
    ↓
Catalog
    ↓
Protection
    ↓
User Selection
    ↓
Removal Plan
    ↓
Removal Engine
    ↓
DISM


==================================================
PARTE 17 — TESTS
==================================================

Añadir tests para:

- clasificación de Xbox.
- clasificación de Clipchamp.
- clasificación de Copilot.
- clasificación de Store.
- clasificación de frameworks.
- clasificación de Defender.
- protección de Windows Update.
- protección de Servicing Stack.
- protección de VCLibs.
- protección de UI.Xaml.
- protección de networking.
- protección de WinRE.
- componentes desconocidos.
- reglas sin coincidencia.
- búsqueda.
- filtros.
- dependencias.
- componentes Installed.
- componentes Superseded.

No utilizar ISO real en tests.

Usar modelos y FakeInventory.


==================================================
PARTE 18 — NO HACER
==================================================

NO implementar:

- eliminación de paquetes.
- eliminación AppX.
- eliminación de Features.
- eliminación de Capabilities.
- eliminación de archivos.
- modificación de registro.
- deshabilitación de servicios.
- deshabilitación de tareas.
- Cleanup.
- ResetBase.
- compresión.
- creación ISO.
- perfiles finales.
- App Packs.
- PCPI.


==================================================
PARTE 19 — COMPILACIÓN
==================================================

Ejecutar:

dotnet build

dotnet test

Objetivo:

0 errores
0 advertencias
100% tests correctos.


==================================================
PARTE 20 — RESULTADO
==================================================

Crear:

prompts\05-resultado.md

Incluir solamente:

- archivos creados/modificados.
- arquitectura del catálogo.
- reglas de protección implementadas.
- categorías implementadas.
- dependencias implementadas.
- cambios de UI.
- resultado de build.
- resultado de tests.
- problemas encontrados.

NO escribir explicaciones largas.


==================================================
CRITERIO DE FINALIZACIÓN
==================================================

El Prompt 05 se considera terminado cuando:

1. El inventario real puede convertirse en un catálogo.
2. Los componentes se clasifican.
3. Los componentes críticos quedan protegidos.
4. Existen motivos de protección.
5. Existen estructuras de dependencias.
6. Se puede buscar y filtrar.
7. La UI muestra claramente protegido/removible/opcional.
8. NO se modifica el WIM.
9. Build limpio.
10. Todos los tests pasan.