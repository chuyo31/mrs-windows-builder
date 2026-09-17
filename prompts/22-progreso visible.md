# PROMPT 22 — PROGRESO VISIBLE DURANTE EL INVENTARIADO

Proyecto:
C:\Users\B3RASCASA\Desktop\MRS-W11PRO-by_chuyo31

OBJETIVO

Mejorar únicamente la UX del proceso de inventariado de la imagen.

Actualmente, al pulsar "Analizar imagen", la pantalla puede permanecer varios
minutos mostrando solo:

    [DISM] Mount-Wim...

El proceso funciona, pero visualmente parece bloqueado.

Queremos una barra de progreso visible y un estado textual que indique que
el proceso continúa trabajando.

REQUISITOS FUNCIONALES

1. Añadir progreso determinista al inventariado, de 0 a 100 %.

2. Dividir el proceso en fases visibles. Como mínimo:

   - Preparando inventariado
   - Comprobando montajes
   - Montando imagen
   - Analizando paquetes
   - Analizando características
   - Analizando capacidades
   - Analizando aplicaciones provisionadas
   - Analizando controladores
   - Construyendo catálogo
   - Finalizando
   - 100 % completado

3. La barra NO debe fingir el porcentaje interno de DISM.
   DISM no proporciona un progreso fiable para estas operaciones.

4. El porcentaje debe representar las fases reales del proceso.
   Puede avanzar al comenzar/terminar cada operación.

5. Mientras una operación DISM larga está ejecutándose, mantener visible:
   - barra de progreso;
   - porcentaje de la fase;
   - texto de estado;
   - indicador visual de actividad.

6. Evitar que la interfaz parezca congelada.

7. Mantener el terminal/log existente funcionando exactamente como hasta ahora.

8. El progreso debe actualizarse en el hilo de UI sin bloquearlo.

9. CancellationToken debe seguir funcionando si el inventariado ya lo soporta.

10. El progreso NO debe modificar:
    - DISM;
    - RemovalEngine;
    - RemovalPlan;
    - ComponentCatalog;
    - ProfileEngine;
    - InstallationOptions;
    - ISOEngine;
    - generación de ISO.

11. Preferir una abstracción reutilizable tipo IProgress<T> o equivalente
    en la capa de inventariado, sin introducir dependencia WPF en las capas
    de dominio/servicios.

12. Si ImageInventoryService necesita reportar fases, hacerlo mediante una
    abstracción independiente de WPF.

13. No crear porcentajes falsos basados en tiempo transcurrido.

14. Si una fase falla, mostrar claramente:
    - fase que falló;
    - estado de error;
    - mantener el log/diagnóstico existente.

15. Al terminar correctamente:
    - progreso = 100 %;
    - estado = "Inventariado completado";
    - continuar exactamente hacia COMPONENTES como actualmente.

UI

Añadir en la pantalla de inventariado una zona visible aproximadamente debajo
del estado principal:

    INVENTARIANDO IMAGEN

    [===========================>          ] 72 %

    Analizando capacidades...

El diseño debe mantener el estilo visual actual de MRS Windows Builder.

No hacer un rediseño general de la interfaz.

COMPATIBILIDAD

Revisar el flujo actual de ImageInventoryService y MainWindow antes de cambiarlo.
No duplicar operaciones DISM ni crear una segunda fase de inventariado.

IMPORTANTE

Este prompt SOLO implementa el progreso visual del inventariado.

No cambiar el comportamiento del catálogo ni las reglas de protección.

TESTS

Añadir tests para verificar como mínimo:

- las fases se reportan en orden;
- el progreso nunca supera 100;
- el progreso nunca es negativo;
- la fase final termina en 100;
- una excepción/fallo no informa falsamente 100 %;
- el inventariado conserva el comportamiento actual.

Ejecutar:

    dotnet test

    dotnet build

Ambos deben terminar correctamente.

DOCUMENTACIÓN

Crear:

    prompts\22-resultado.md

Documentar:

- causa/limitación UX original;
- arquitectura elegida;
- fases implementadas;
- cómo se evita fingir progreso interno de DISM;
- cambios realizados;
- tests;
- build.

No realizar refactorizaciones no relacionadas.

Al finalizar, indicar el commit recomendado.