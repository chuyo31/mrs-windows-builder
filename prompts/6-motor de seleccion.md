
PROMPT 06 — MOTOR DE SELECCIÓN Y REMOVAL PLAN

OBJETIVO:

Crear el sistema que transforma la selección del usuario en un RemovalPlan seguro y verificable.

IMPORTANTE:

ESTA FASE NO DEBE MODIFICAR EL WIM.

NO ejecutar todavía:

- DISM /Remove-Package
- DISM /Remove-ProvisionedAppxPackage
- DISM /Disable-Feature
- DISM /Remove-Capability
- eliminación de archivos
- modificación de registro
- eliminación de servicios
- eliminación de tareas
- Cleanup-Image
- ResetBase

El objetivo es:

CATÁLOGO
    ↓
SELECCIÓN USUARIO
    ↓
VALIDACIÓN
    ↓
DEPENDENCIAS
    ↓
PROTECCIONES
    ↓
REMOVAL PLAN
    ↓
PREVISUALIZACIÓN

El RemovalPlan será utilizado por el futuro RemovalEngine.


==================================================
ESTADO ACTUAL
==================================================

Proyecto:

C:\Users\B3RASCASA\Desktop\MRS-W11PRO-by_chuyo31

Estado:

- análisis de ISO funcionando.
- selección de edición funcionando.
- inventario real funcionando.
- montaje WIM ReadOnly funcionando.
- desmontaje funcionando.
- catálogo funcionando.
- clasificación funcionando.
- protección funcionando.
- dependencias iniciales funcionando.
- búsqueda y filtros funcionando.

Inventario real probado:

Windows 11 Pro
26H2
Build 26300.9278
x64
es-ES

Resultados:

208 paquetes
57 Apps
136 Features
424 Capabilities
0 drivers de terceros

Tests actuales:

118/118


==================================================
1. NUEVOS MODELOS
==================================================

Crear:

RemovalPlan

Propiedades mínimas:

- ImageId
- CreatedAt
- Components
- Actions
- Warnings
- Errors
- TotalSelected
- TotalAllowed
- TotalBlocked
- IsValid

Crear:

RemovalPlanItem

Propiedades:

- ComponentId
- DisplayName
- Category
- SourceType
- CurrentState
- Requested
- Allowed
- Action
- Risk
- Protection
- BlockReason
- Dependencies
- Dependents
- EstimatedSize
- Warnings

Crear:

RemovalAction

Propiedades:

- ActionType
- Target
- ComponentId
- Parameters
- EstimatedSize
- Risk

Crear enum:

RemovalActionType:

- None
- RemovePackage
- RemoveAppx
- DisableFeature
- RemoveCapability
- RemoveService
- RemoveTask
- RemoveRegistry
- RemoveFile
- Unknown


==================================================
2. IMPORTANTE: NO CONFUNDIR COMPONENTE Y ACCIÓN
==================================================

Un ComponentDefinition representa QUÉ es.

Una RemovalAction representa QUÉ HARÍAMOS con él.

Ejemplo:

Component:

Clipchamp

Category:
Application

RemovalMode:
Appx

RemovalAction:

RemoveAppx


Otro:

Component:

Windows Update

Protection:
Protected

RemovalAction:

None


==================================================
3. RemovalPlanBuilder
==================================================

Crear:

IRemovalPlanBuilder

y:

RemovalPlanBuilder


Entrada:

ImageInventory
ComponentCatalog
IReadOnlyCollection<ComponentSelection>


Salida:

RemovalPlan


Flujo:

1. recibir selección.
2. localizar componentes.
3. comprobar que existen en catálogo.
4. comprobar estado actual.
5. comprobar protección.
6. comprobar dependencias.
7. comprobar dependientes.
8. determinar acción.
9. calcular bloqueos.
10. calcular warnings.
11. construir plan.


==================================================
4. SELECCIÓN
==================================================

Crear:

ComponentSelection

Propiedades:

- ComponentId
- Selected

No guardar solamente índices de DataGrid.

La selección debe utilizar ComponentId estable.


==================================================
5. PROTECCIÓN
==================================================

Si:

Protection == Protected

entonces:

Requested = true
Allowed = false

y:

Action = None

Debe existir:

BlockReason


Ejemplo:

Usuario selecciona:

Microsoft.UI.Xaml

Resultado:

❌ BLOQUEADO

Motivo:

"Framework protegido porque puede ser utilizado por otras aplicaciones."

IMPORTANTE:

La UI debe permitir seleccionar visualmente si se desea, pero el plan nunca debe permitir una acción destructiva contra un componente protegido.

Preferiblemente el checkbox de protegido ya está bloqueado.


==================================================
6. DEPENDENCIAS
==================================================

Comprobar:

Dependencies
Dependents


Caso:

A depende de B.

Si usuario selecciona A:

B NO se elimina automáticamente.

Si B está protegido:

mostrar:

⚠ A utiliza B.

Si B no está protegido:

seguir sin eliminar B automáticamente.

NUNCA realizar eliminación en cascada silenciosa.

El usuario debe seleccionar explícitamente cada componente.


==================================================
7. DEPENDIENTES
==================================================

Caso:

Paint
    ↓
VCLibs

Si usuario selecciona VCLibs:

comprobar:

¿Hay componentes seleccionados/conservados que dependan de VCLibs?


Si sí:

BLOQUEAR.

Ejemplo:

❌ No se puede eliminar VCLibs.

Motivo:

"El componente es requerido por otros componentes que permanecerán en la imagen."


==================================================
8. ESTADO DISM
==================================================

Para paquetes:

Installed
Superseded
Not Present
Unknown

Reglas iniciales:

Installed:
puede evaluarse.

Superseded:
NO convertir automáticamente en eliminación.

Not Present:
no generar acción.

Unknown:
bloquear y advertir.


IMPORTANTE:

Los paquetes Superseded NO deben eliminarse todavía.

Queremos tratar el servicing y la limpieza de componentes en una fase específica posterior.


==================================================
9. APPX
==================================================

Para AppX provisionadas:

Si:

Protection = Removable

y:

Installed = true

→ permitir RemoveAppx.


Si:

Protection = Protected

→ bloquear.


No eliminar frameworks AppX automáticamente.


==================================================
10. FEATURES
==================================================

Si Feature:

Protected
→ bloquear.

Si:

Optional/Removable
→ permitir DisableFeature.


IMPORTANTE:

Deshabilitar una Feature no es lo mismo que eliminar su payload.

No realizar todavía ninguna operación real.


==================================================
11. CAPABILITIES
==================================================

Si:

Capability

es Optional/Removable:

→ permitir RemoveCapability.


Si:

Protected:

→ bloquear.


==================================================
12. ACCIONES DESCRIPTIVAS
==================================================

El plan debe generar acciones concretas.

Ejemplos:

Clipchamp:

Action:
RemoveAppx

Xbox:

Action:
RemoveAppx

Feature opcional:

Action:
DisableFeature

Capability opcional:

Action:
RemoveCapability

Package permitido:

Action:
RemovePackage


Pero estas acciones son SOLAMENTE DESCRIPTIVAS.

NO ejecutarlas.


==================================================
13. WARNINGS
==================================================

Crear:

RemovalWarning

Propiedades:

- Code
- Message
- Severity
- ComponentId

Severity:

- Info
- Warning
- High
- Critical


Ejemplos:

WARNING:

"El componente tiene dependientes."


HIGH:

"El componente puede afectar a una funcionalidad del sistema."


CRITICAL:

"El componente está protegido y no puede eliminarse."


==================================================
14. ESTIMACIÓN DE TAMAÑO
==================================================

Crear:

EstimatedSize

No inventar tamaños precisos.

Si DISM no proporciona tamaño real:

usar:

null

o:

Unknown.


NO mostrar:

"libera 350 MB"

si no tenemos datos reales.


Podemos mostrar:

"Tamaño estimado: desconocido"


Posteriormente podemos implementar un SizeAnalyzer real.


==================================================
15. VALIDACIÓN DEL PLAN
==================================================

Crear:

RemovalPlanValidator


Debe comprobar:

- ComponentId válido.
- componente existente.
- componente detectado.
- no protegido.
- acción compatible con SourceType.
- dependencias.
- conflictos.
- estados.
- parámetros válidos.


Resultado:

PlanValidationResult

con:

- IsValid
- Errors
- Warnings


==================================================
16. NO MODIFICAR LA IMAGEN
==================================================

Crear una garantía arquitectónica:

RemovalPlanBuilder
    ↓
NO referencia
    ↓
DismRunner


Es decir:

El plan no debe poder ejecutar DISM.

El futuro:

RemovalEngine

será el responsable de ejecutar el plan.


Arquitectura:

ComponentCatalog
      ↓
RemovalPlanBuilder
      ↓
RemovalPlan
      ↓
RemovalEngine
      ↓
DismRunner


==================================================
17. PREVISUALIZACIÓN UI
==================================================

Modificar la pantalla COMPONENTES.

Actualmente tenemos:

- búsqueda.
- filtros.
- DataGrid.
- selección.
- detalles.

Añadir una zona inferior:

RESUMEN DE SELECCIÓN

Ejemplo:

────────────────────────────────────────────

Seleccionados: 12

Permitidos: 9
Bloqueados: 3

⚠ Advertencias: 2

────────────────────────────────────────────


Botón:

"Ver plan"


Al pulsarlo:

mostrar pantalla:

PLAN DE MODIFICACIÓN


==================================================
18. PANTALLA PLAN
==================================================

Diseño:

PLAN DE MODIFICACIÓN

┌─────────────────────────────────────────────┐
│ ✓ ELIMINAR                                  │
│                                             │
│ Clipchamp                                   │
│ Aplicación · RemoveAppx                     │
│ Riesgo: Bajo                                │
├─────────────────────────────────────────────┤
│ ✓ ELIMINAR                                  │
│                                             │
│ Xbox Gaming                                 │
│ Gaming · RemoveAppx                         │
│ Riesgo: Bajo                                │
├─────────────────────────────────────────────┤
│ 🔒 BLOQUEADO                                │
│                                             │
│ Microsoft.UI.Xaml                            │
│ Framework                                   │
│                                             │
│ Framework protegido porque otras            │
│ aplicaciones dependen de él.               │
└─────────────────────────────────────────────┘


Resumen:

12 seleccionados
9 acciones permitidas
3 bloqueados
2 advertencias


Botones:

[Volver]

[Cancelar selección]

[Confirmar plan]


IMPORTANTE:

"Confirmar plan" NO modifica todavía el WIM.

Simplemente guarda/acepta el RemovalPlan en memoria para la siguiente fase.


==================================================
19. PERFILES
==================================================

NO implementar todavía los perfiles completos.

Pero RemovalPlan debe aceptar posteriormente:

Normal
Light
Medium
Ultra
Custom

No crear todavía las reglas de selección automática.


==================================================
20. EXPORTACIÓN DEL PLAN
==================================================

Preparar opcionalmente:

Guardar plan JSON.

Ejemplo:

output/removal-plan.json


Debe contener:

- versión del formato.
- información de imagen.
- componentes seleccionados.
- acciones.
- warnings.
- bloqueos.
- fecha.


No es necesario crear todavía una UI completa de exportación.

Puede existir un servicio:

RemovalPlanSerializer


==================================================
21. SEGURIDAD
==================================================

REGLA FUNDAMENTAL:

Nunca ejecutar una acción simplemente porque el usuario haya marcado un checkbox.

Siempre:

Selection
    ↓
Catalog
    ↓
Protection
    ↓
Dependencies
    ↓
Validation
    ↓
RemovalPlan


Solo un RemovalPlan válido podrá ser entregado al futuro RemovalEngine.


==================================================
22. TESTS
==================================================

Añadir tests exhaustivos.

Como mínimo:

SELECCIÓN:

- selección de un componente válido.
- selección de componente inexistente.
- selección múltiple.
- selección vacía.


PROTECCIÓN:

- componente protegido bloqueado.
- BlockReason presente.
- acción None.


DEPENDENCIAS:

- dependencia protegida.
- dependencia no protegida.
- dependiente de componente seleccionado.
- dependencia no seleccionada no se elimina automáticamente.
- eliminación en cascada nunca automática.


PAQUETES:

- Installed.
- Superseded.
- Not Present.
- Unknown.


APPX:

- AppX removible.
- AppX protegida.
- framework protegido.


FEATURES:

- Feature removible.
- Feature protegida.


CAPABILITIES:

- Capability removible.
- Capability protegida.


PLAN:

- acciones correctas.
- múltiples acciones.
- warnings.
- errores.
- contador TotalSelected.
- contador TotalAllowed.
- contador TotalBlocked.
- IsValid.


VALIDACIÓN:

- acción incompatible.
- componente no detectado.
- componente protegido.
- dependencia conflictiva.
- parámetros inválidos.


SERIALIZACIÓN:

- plan → JSON.
- JSON → plan.
- conservar acciones.
- conservar warnings.
- conservar bloqueos.


NO utilizar ISO real en tests.


==================================================
23. REGRESIÓN
==================================================

No romper:

- análisis ISO.
- selección de edición.
- inventario.
- montaje ReadOnly.
- desmontaje.
- catálogo.
- protección.
- búsqueda.
- filtros.


Ejecutar:

dotnet build

dotnet test


Objetivo:

0 errores
0 advertencias
100% tests correctos.


==================================================
24. RESULTADO
==================================================

Crear:

prompts\06-resultado.md

Incluir:

- archivos creados/modificados.
- nuevos modelos.
- arquitectura.
- validaciones.
- protección.
- dependencias.
- UI.
- serialización.
- resultado build.
- resultado tests.
- problemas encontrados.


NO escribir explicaciones innecesariamente largas.


==================================================
25. NO HACER
==================================================

NO implementar todavía:

- RemovalEngine real.
- Remove-Package real.
- Remove-ProvisionedAppxPackage real.
- Disable-Feature real.
- Remove-Capability real.
- eliminación de archivos.
- eliminación de servicios.
- eliminación de tareas.
- modificación de registro.
- Cleanup.
- ResetBase.
- compresión.
- creación ISO.
- perfiles automáticos.
- App Packs.
- PCPI.


==================================================
CRITERIO DE FINALIZACIÓN
==================================================

P06 está terminado cuando:

1. El usuario puede seleccionar componentes.
2. La selección utiliza IDs estables.
3. Se comprueba protección.
4. Se comprueban dependencias.
5. Se comprueban dependientes.
6. Se generan acciones descriptivas.
7. Se generan warnings.
8. Se generan bloqueos.
9. Se puede validar el plan.
10. Se puede visualizar el plan.
11. Se puede serializar el plan.
12. El plan NO puede modificar el WIM.
13. Build limpio.
14. Todos los tests pasan.

NO avanzar al RemovalEngine dentro de este prompt.