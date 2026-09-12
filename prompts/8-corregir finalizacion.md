PROMPT 08 — CORREGIR FINALIZACIÓN DE UI Y LIMPIEZA IDEMPOTENTE

Contexto:
La primera prueba REAL de MRS Windows Builder sobre Windows 11 26H2 Pro ha sido exitosa.

Prueba:
- Imagen real Windows 11 26H2 Pro.
- Componente: Clipchamp.Clipchamp
- Acción: RemoveAppx
- Resultado: 1/1 acciones completadas.
- DISM realizó correctamente el desmontaje.
- El log de DISM confirma:
  "Received unmount request."
  "Unmount complete."
  "Wimserv stopped."
- Posteriormente Get-MountedWimInfo ya NO muestra el montaje de MRS.
- El único montaje que queda pertenece a una prueba anterior de tiny11 y es independiente de MRS.

Problema encontrado:
Después de terminar correctamente, la pantalla "APLICANDO CAMBIOS" queda bloqueada:
- aparece el MessageBox "Cambios aplicados correctamente (1 acción(es))."
- después de pulsar Aceptar, "Cerrar" permanece deshabilitado.
- "Cancelar" tampoco responde.
- La UI no vuelve correctamente al flujo normal.

IMPORTANTE:
NO cambiar la lógica funcional de DISM ni la estrategia de RemovalEngine que ya ha funcionado en una prueba real.

OBJETIVO P08:
Corregir exclusivamente:
1. Estado de la UI después de éxito.
2. Estado de la UI después de error.
3. Estado de la UI después de cancelación.
4. CanExecute/CanExecuteChanged de los botones.
5. Sincronización correcta del estado async con la UI.
6. Limpieza idempotente cuando un montaje ya ha sido desmontado.

Revisa MainWindow, ViewModels, comandos y la pantalla/estado "APLICANDO CAMBIOS".

Comportamiento obligatorio:

MIENTRAS SE EJECUTA:
- IsBusy/estado equivalente = true.
- Cancelar habilitado.
- Cerrar/Continuar deshabilitado.
- No permitir iniciar otra operación.

ÉXITO:
- esperar a que ApplyAsync haya terminado COMPLETAMENTE.
- incluir Commit y Unmount y su verificación dentro de la operación.
- solo entonces marcar la operación como completada.
- IsBusy = false.
- Cancelar = disabled.
- Cerrar/Continuar = enabled.
- mostrar el MessageBox de éxito una sola vez.
- después de pulsar Aceptar la ventana debe seguir completamente interactiva.
- Cerrar debe funcionar.

ERROR:
- IsBusy = false.
- Cancelar = disabled.
- Cerrar/volver = enabled.
- conservar información del error.
- NO ocultar excepciones.
- respetar el comportamiento de Discard/rollback existente.

CANCELACIÓN:
- respetar Cooperative Cancellation existente.
- esperar a que la operación termine realmente antes de desbloquear la UI.
- IsBusy = false al finalizar.
- botones coherentes con el estado final.

LIMPIEZA IDEMPOTENTE:
Si durante el finally se intenta desmontar una imagen que ya fue desmontada correctamente:
- detectar que el montaje ya no existe antes de tratar el resultado como error.
- no convertir un "ya desmontado" en fallo de la operación.
- no ejecutar Cleanup-Mountpoints automáticamente.
- no borrar montajes manualmente.
- no ocultar otros errores reales de DISM.

MUY IMPORTANTE:
No asumir que un ExitCode=0 de una operación intermedia significa éxito global.
El resultado final solo debe ser exitoso cuando:
1. todas las acciones han terminado correctamente;
2. Commit ha terminado correctamente;
3. Unmount ha terminado correctamente o se confirma que ya estaba desmontado;
4. la verificación final ha terminado correctamente;
5. no queda ningún montaje de MRS asociado a la operación.

También revisa el caso en que Unmount ya haya ocurrido dentro del flujo y el finally vuelva a intentar desmontarlo.

No introducir:
- Thread.Sleep
- polling
- timers para "esperar"
- Cleanup-Mountpoints automático
- cambios arbitrarios en DISM
- cambios en RemovalPlan
- cambios en Catalog
- nuevos mecanismos paralelos innecesarios.

Mantener:
- async/await.
- MVVM.
- arquitectura actual.
- contratos públicos salvo necesidad real.

TESTS:
Añade o modifica tests para cubrir como mínimo:

A) Éxito completo:
Apply -> Commit -> Unmount -> Verify -> UI desbloqueada.

B) Éxito cuando el Unmount ya ocurrió:
no produce falso error.

C) Error durante una acción:
UI vuelve a estado interactivo y se conserva el error.

D) Error durante Commit:
UI desbloqueada y no mostrar éxito.

E) Error durante Unmount:
UI desbloqueada y no mostrar éxito salvo que se confirme inequívocamente que ya estaba desmontado.

F) Cancelación:
UI desbloqueada al terminar.

G) CanExecute:
Cerrar cambia de disabled a enabled al finalizar.

H) Cancelar:
se deshabilita al finalizar.

I) MessageBox:
no deja IsBusy permanentemente activo.

J) No se muestra el mensaje de éxito antes de terminar Commit/Unmount/Verify.

IMPORTANTE SOBRE EL CÓDIGO:
Antes de modificar, identifica exactamente dónde se produce el bloqueo y explícalo brevemente en el resultado.
No hagas una reescritura de la pantalla.
Haz el cambio mínimo y robusto.

VALIDACIÓN:
1. dotnet test
2. dotnet build
3. 0 errores
4. 0 warnings
5. comprobar que todos los tests pasan.

Guarda el resultado en:

prompts\08-resultado.md

El resultado debe incluir:
- causa raíz,
- archivos modificados,
- solución aplicada,
- tests añadidos/modificados,
- resultado de dotnet test,
- resultado de dotnet build.

No avances todavía a perfiles, catálogo nuevo, ISO final, optimización de tamaño ni nuevas eliminaciones.
P08 termina aquí.