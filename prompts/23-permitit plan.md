# PROMPT 23 — PERMITIR PLAN Y GENERACIÓN CON 0 ELIMINACIONES

Proyecto:
C:\Users\B3RASCASA\Desktop\MRS-W11PRO-by_chuyo31

OBJETIVO

Permitir que el usuario continúe hasta la generación de ISO aunque no haya
seleccionado ningún componente para eliminar.

Esto es necesario para validar el pipeline completo de generación de MRS
con una imagen Pro sin modificaciones de componentes.

CASO PRINCIPAL

Selección:

    0 componentes

Debe ser un estado válido.

El flujo debe poder hacer:

ISO original
→ selección Pro
→ 0 eliminaciones
→ opciones de instalación
→ boot.wim
→ install.wim sin eliminaciones
→ PostInstall
→ validación
→ oscdimg
→ ISO final

No se debe seleccionar ningún componente artificialmente.

CAMBIOS

1. MainWindow:

Actualmente:

    ViewPlanButton.IsEnabled = (plan?.TotalSelected ?? 0) > 0;

Cambiar la lógica para permitir abrir el plan cuando exista un catálogo/plan
válido aunque:

    TotalSelected == 0

No habilitar el botón simplemente porque el catálogo exista si el estado
necesario para generar todavía no está preparado.

2. RemovalPlan:

Revisar si un RemovalPlan con 0 componentes ya es válido.

Si ya lo soporta, reutilizarlo sin modificar su modelo.

Si existe una validación que considere obligatoriamente que debe haber
componentes seleccionados, hacerla explícitamente compatible con un plan vacío.

NO inventar componentes.

3. ShowPlan:

Cuando haya 0 eliminaciones, mostrar claramente algo como:

    SIN ELIMINACIONES DE COMPONENTES

    La imagen se generará sin eliminar componentes del sistema.

El plan debe seguir mostrando las opciones de instalación.

4. Confirmación:

Actualmente existe:

    if (plan.TotalAllowed == 0)
    {
        ...
        return;
    }

Esto impide el escenario de 0 eliminaciones.

Cambiarlo para distinguir:

    A) 0 seleccionados
       → continuar hacia generación ISO.

    B) Hay seleccionados pero todos están bloqueados
       → mantener el bloqueo actual y NO generar.

    C) Hay acciones permitidas
       → flujo actual de eliminación + generación.

IMPORTANTE:

0 seleccionados NO significa error.

0 seleccionados significa:

    "No se eliminarán componentes."

5. RemovalEngine:

NO debe ejecutarse cuando:

    plan.TotalSelected == 0

En ese caso el pipeline debe conservar la imagen Pro sin modificaciones de
componentes y continuar con las siguientes fases.

NO introducir un Mount/Commit innecesario solamente para simular una eliminación.

6. ISOEngine:

Revisar el flujo actual para comprobar que puede recibir una imagen de trabajo
sin RemovalPlan con acciones.

Si el pipeline ya soporta el plan vacío, reutilizarlo.

Si existe una validación que exige una acción de RemovalEngine, adaptarla
mínimamente para aceptar el caso de 0 eliminaciones.

NO cambiar el diseño general del pipeline.

7. Mensajes de UI:

El usuario debe poder distinguir:

    "0 eliminaciones"

de:

    "No hay acciones permitidas porque todas las selecciones están bloqueadas"

Son situaciones diferentes.

8. Botones:

"Ver plan" debe poder abrir el plan con 0 seleccionados.

"Generar ISO" / confirmación debe poder continuar con 0 eliminaciones.

No habilitar acciones de eliminación cuando no corresponda.

9. Seguridad:

No modificar:

- ComponentCatalog
- ProtectionEngine
- ProfileEngine
- SecurityOptions
- InstallationOptions
- LabConfig
- Autounattend
- PostInstall
- BootWimProvisioner
- RemovalEngine salvo el bypass limpio cuando no hay acciones
- reglas de protección existentes

10. No ejecutar DISM adicional innecesario.

La ruta de 0 eliminaciones debe ser lo más directa posible.

TESTS

Añadir tests para:

- RemovalPlan vacío válido.
- 0 seleccionados permite abrir plan.
- 0 seleccionados no ejecuta RemovalEngine.
- 0 seleccionados puede continuar hacia ISOEngine.
- Selecciones bloqueadas siguen bloqueando.
- Selecciones permitidas siguen usando el flujo actual.
- No se mezclan los dos casos.

Mantener todos los tests existentes.

Ejecutar:

    dotnet test

    dotnet build

Ambos deben terminar correctamente.

DOCUMENTACIÓN

Crear:

    prompts\23-resultado.md

Documentar:

- problema encontrado;
- comportamiento anterior;
- nuevo comportamiento;
- diferencia entre "0 eliminaciones" y "0 acciones permitidas";
- cambios realizados;
- tests;
- build.

No realizar refactorizaciones no relacionadas.

Al finalizar, indicar el commit recomendado.