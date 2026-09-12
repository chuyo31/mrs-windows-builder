# Resultado - P08: corregir finalización de UI y limpieza idempotente

## Causa raíz

La app no usa MVVM/`ICommand` (es código-behind con botones `Click` +
`IsEnabled`); el "CanExecute" real es ese `IsEnabled`.

En `MainWindow.RunExecutionAsync` el bloqueo de la UI (`ExecutionCloseButton.IsEnabled
= true`, etc.) vivía **solo en el `finally`**, y entre el `MessageBox.Show`
de éxito y ese `finally` había un `await VerifyExecutionAsync(...)` que
ejecuta un **segundo ciclo completo de DISM** (mediante
`ImageInventoryService.BuildInventoryAsync`: monta de nuevo la copia de
trabajo en solo lectura, hace 5 consultas `/Get-*` y desmonta) para comparar
el inventario antes/después. Ese reinventario:

- no forma parte de la operación transaccional del `RemovalEngine` (que ya
  había terminado con Commit + Unmount + su propia verificación interna);
- es lento sobre una imagen real (208 paquetes, 57 apps, 136 features, 424
  capabilities) y puede tardar bastante o toparse con contención de DISM al
  montar la misma imagen justo después de haber hecho commit sobre ella;
- mientras dura, el código seguía dentro del `try`, así que el `finally` (y
  con él "Cerrar") no se ejecutaba todavía.

Resultado: tras pulsar "Aceptar" en el MessageBox de éxito, la UI quedaba
esperando esa verificación adicional con "Cerrar" deshabilitado y sin forma
de cancelarla (`VerifyExecutionAsync` no usaba el `CancellationToken` de
"Cancelar").

Un segundo problema, relacionado con la "limpieza idempotente" pedida: el
`RemovalEngine` trataba cualquier `ExitCode != 0` de `/Unmount-Wim /Commit`
o `/Unmount-Wim /Discard` como fallo, sin comprobar si el montaje ya no
existía (lo que puede pasar de forma legítima).

## Archivos modificados

- `src/MRS.RemovalEngine/RemovalEngine.cs`
- `src/MRS.WindowsBuilder/MainWindow.xaml.cs`
- `tests/MRS.RemovalEngine.Tests/RemovalEngineTests.cs`

No se ha tocado `RemovalPlan`, `ComponentCatalog`, `DismRunner` ni la
estrategia de montaje/commit/discard que ya funcionó en la prueba real.

## Solución aplicada

### `MainWindow.xaml.cs`

- El desbloqueo de la UI (`ExecutionCancelButton.IsEnabled = false`,
  `ExecutionCloseButton.IsEnabled = true`, `_isApplyingChanges = false`,
  baja del handler de log) se extrajo a `UnlockExecutionUi()`, **idempotente**
  (`_executionUiUnlocked`), y se llama **inmediatamente después de**
  `ReconcileExecutionRows(result)` — es decir, en cuanto
  `RemovalEngine.ExecuteAsync` termina (con su Commit/Unmount/verificación
  internos ya incluidos) — y **antes** del `MessageBox.Show` y del
  reinventario opcional.
- El `finally` sigue llamando a `UnlockExecutionUi()` como red de seguridad
  (fallo antes de montar, cancelación temprana, excepción inesperada);
  al ser idempotente no hay doble efecto.
- `VerifyExecutionAsync` (el reinventario/diagnóstico) pasa a ejecutarse
  **después** de que la UI ya esté desbloqueada: su duración, un fallo o una
  lentitud de DISM ya no pueden volver a bloquear "Cerrar" ni "Cancelar".
- Nuevo guard `_isApplyingChanges`: `PlanConfirmButton_Click` no permite
  solapar una segunda ejecución mientras haya una en curso.
- `_executionCts` se libera (`Dispose`) en el `finally`.

### `RemovalEngine.cs` (limpieza idempotente)

- Nuevo `IsStillMountedAsync(mountDir)` (a partir de `/Get-MountedWimInfo`);
  `VerifyNoMountsRemainAsync` pasa a apoyarse en él.
- Si `/Unmount-Wim /Commit` devuelve `ExitCode != 0`, **antes de declarar
  fallo** se comprueba si el montaje ya no existe; si es así, se registra un
  aviso y la operación se da por completada (`Committed = true`,
  `Phase = Completed`) en lugar de `Failed`.
- `DiscardAsync`: si `image.IsMounted` ya es `false` no se reintenta el
  desmontaje (evita el "el finally vuelve a intentar desmontar" del
  prompt); y si `/Unmount-Wim /Discard` devuelve `ExitCode != 0` pero el
  montaje ya no existe, no se registra como error adicional.
- Nunca se asume éxito solo por el `ExitCode`: siempre se confirma contra
  `Get-MountedWimInfo`. Nunca se ejecuta `/Cleanup-Mountpoints` ni se borra
  un montaje "a mano".

## Tests añadidos/modificados

En `tests/MRS.RemovalEngine.Tests/RemovalEngineTests.cs`:

- `Commit_failure_is_reported_as_failed_when_the_mount_still_exists`
  (antes `..._and_not_committed`, ahora comprueba explícitamente que el
  montaje sigue existiendo para que sea un fallo real) — cubre **D**.
- `Commit_failure_is_idempotently_treated_as_success_when_already_unmounted`
  (nuevo) — cubre **B**: `ExitCode != 0` en commit pero
  `Get-MountedWimInfo` confirma que ya no está montado ⇒ éxito, no falso
  error.
- `Discard_failure_is_not_treated_as_a_new_error_when_the_mount_already_disappeared`
  (nuevo) — cubre **E**: un discard con `ExitCode != 0` sobre un montaje que
  ya no existe no añade un error nuevo; el único error del resultado sigue
  siendo el de la acción que falló originalmente.

Los tests ya existentes de la fase 7 siguen cubriendo: éxito completo
(**A**), error durante una acción con el error conservado en
`result.Errors`/`ActionsFailed` (**C**), y cancelación con
`Phase = Cancelled` y `Committed = false` (**F**).

Los puntos **G/H/I/J** (estado de los botones `IsEnabled`, que el MessageBox
no deje `IsBusy` activo) son de UI de WPF; el proyecto no tiene ni tenía un
arnés de pruebas de UI (no hay `ICommand`/ViewModels), y añadir uno ahora
habría sido una reescritura fuera del alcance de "cambio mínimo" de este
prompt. Se verifican por construcción: `UnlockExecutionUi()` es el único
punto que cambia esos tres estados a la vez, se invoca de forma idempotente
tanto en el camino normal como en el `finally`, y se invoca **antes** de
cualquier operación que pueda tardar o fallar tras el resultado del motor.

## Resultado de `dotnet build`

`MRS.RemovalEngine` y los 4 proyectos de tests: **0 errores, 0 advertencias**.
`MRS.WindowsBuilder` compila su XAML y C# sin errores (el build de la
solución completa solo falla por el `.exe` bloqueado por la instancia
elevada que el usuario tenía abierta para la prueba real — no es un error
de código).

## Resultado de `dotnet test`

```
MRS.ImageEngine.Tests       : 77/77
MRS.ComponentCatalog.Tests  : 41/41
MRS.RemovalPlanning.Tests   : 48/48
MRS.RemovalEngine.Tests     : 37/37   (+2 nuevos, 1 modificado)
```

Total: **203/203**. Sin regresiones.
