# Resultado - P23: permitir plan y generación con 0 eliminaciones

## Problema encontrado

El flujo obligaba a seleccionar al menos un componente para poder:

1. Abrir el plan (`ViewPlanButton.IsEnabled = (plan?.TotalSelected ?? 0) > 0`).
2. Continuar tras confirmarlo (`if (plan.TotalAllowed == 0) { ...; return; }`
   bloqueaba tanto "0 seleccionados" como "todo bloqueado" con el mismo
   mensaje genérico "no hay nada que aplicar").

Además, aunque se hubiera permitido continuar, `RemovalEngine.ExecuteAsync`
montaba la imagen en escritura y hacía `/Unmount-Wim /Commit` **incluso con
0 acciones** — un ciclo DISM completo sin ningún cambio real que confirmar.

Esto impedía validar el pipeline completo (ISO original → Pro → 0
eliminaciones → boot.wim → install.wim sin cambios → PostInstall → oscdimg)
con una imagen Pro "tal cual", que es un escenario legítimo y necesario para
probar el resto de la generación de forma aislada.

## Comportamiento anterior vs. nuevo

| | Antes | Ahora |
|---|---|---|
| "Ver plan" con 0 seleccionados | Deshabilitado | Habilitado (si hay un plan válido) |
| Confirmar con 0 seleccionados | Bloqueado ("no hay nada que aplicar") | Continúa hacia la ejecución |
| Confirmar con selección 100 % bloqueada | Bloqueado | Sigue bloqueado (sin cambios) |
| `RemovalEngine` con 0 seleccionados | Monta RW + commit sin acciones | No monta nada; conserva la imagen exportada |
| Plan con 0 seleccionados en el pipeline (`IsoGenerationPipeline`) | Ya funcionaba (no se tocó) | Sigue funcionando, ahora además sin el Mount/Commit vacío de RemovalEngine |

## Diferencia entre "0 eliminaciones" y "0 acciones permitidas"

Son estados distintos y ahora se distinguen explícitamente en toda la app:

- **0 eliminaciones** (`plan.TotalSelected == 0`): el usuario no marcó ningún
  componente. No es un error: significa "no se eliminará nada", y el flujo
  continúa con normalidad (banner informativo, no un bloqueo).
- **0 acciones permitidas por bloqueo** (`plan.TotalSelected > 0 &&
  plan.TotalAllowed == 0`): el usuario sí marcó componentes, pero todos
  están protegidos/bloqueados. Este caso **sigue bloqueando la generación**
  (mensaje "No hay ninguna acción permitida: todos los componentes
  seleccionados están bloqueados. No se aplicará ningún cambio."), porque no
  hay ninguna selección real que continuar hacia adelante como plan
  ejecutable — evita que el usuario crea que se aplicó lo que pidió cuando
  en realidad nada de lo pedido era viable.

Ambos casos producen `plan.Actions` vacío, así que la distinción **no** se
puede hacer mirando solo `TotalAllowed`/`Actions`: hace falta `TotalSelected`.

## Cambios realizados

**`MRS.RemovalEngine/RemovalEngine.cs`** (permitido explícitamente por la
sección 9: "RemovalEngine salvo el bypass limpio cuando no hay acciones"):
justo después de comprobar que `WorkingWim` existe, se añade una salida
temprana cuando `plan.TotalSelected == 0`: registra el resultado como
`Completed`/`Success = true`/`Committed = true` sin ninguna acción ejecutada
ni fallida, **sin llamar a ningún método de `IDismRunner`** (ni el chequeo de
índice, ni la recuperación de montajes huérfanos, ni el montaje, ni el
commit) — la ruta más directa posible, tal como pide la sección 10. La
imagen de trabajo ya salió de `WorkingImageFactory.CreateAsync`
(`Export-Image`) con la edición correcta; no hay ningún cambio pendiente que
confirmar. El caso "seleccionados pero todos bloqueados" **no** activa este
atajo (seguirá montando/comprometiendo con 0 acciones, como siempre), porque
la condición es exactamente `TotalSelected == 0`, no `Actions.Count == 0`.

No se ha modificado `RemovalPlan` ni `RemovalPlanValidator`: ya soportaban un
plan vacío como válido (`IsValid => Errors.Count == 0`, sin ningún mínimo de
componentes) — confirmado con el test ya existente
`Empty_selection_produces_an_empty_but_valid_plan` (P06), que sigue pasando
sin cambios.

No se ha modificado `IsoGenerationPipeline` (`MRS.ISOEngine`): ya reutilizaba
`request.RemovalPlan ?? new RemovalPlan()` y llamaba a
`IRemovalEngine.ExecuteAsync` igual que con cualquier otro plan, así que se
beneficia automáticamente del bypass añadido en `RemovalEngine` sin ningún
cambio de diseño (sección 6: "si el pipeline ya soporta el plan vacío,
reutilizarlo").

**`MRS.WindowsBuilder/MainWindow.xaml.cs`**:
- `UpdateSelectionSummary`: `ViewPlanButton.IsEnabled = plan is { IsValid:
  true }` (antes exigía `TotalSelected > 0`). No se habilita solo porque
  exista un catálogo: sigue exigiendo que `BuildPlanFromCurrentSelection()`
  haya podido construir un plan válido.
- `ShowPlan`: muestra/oculta `NoRemovalsBanner` según `plan.TotalSelected ==
  0`. Las opciones de instalación (`RefreshInstallationOptionsCheckboxes`)
  se siguen mostrando siempre, con o sin eliminaciones.
- `PlanConfirmButton_Click`: la condición de bloqueo pasa de `plan.TotalAllowed
  == 0` a `plan.TotalSelected > 0 && plan.TotalAllowed == 0` — así 0
  seleccionados ya no entra en ese bloqueo y continúa hacia la confirmación y
  `RunExecutionAsync`.
- `RunExecutionAsync`: el texto de estado antes de invocar `RemovalEngine`
  distingue "Sin eliminaciones: conservando la imagen exportada..." de
  "Montando imagen...", y el mensaje final de éxito dice "Imagen preparada
  sin eliminaciones de componentes." en vez de "Cambios aplicados
  correctamente (0 acción(es))." — evita el mensaje confuso de "0 acciones"
  como si algo se hubiera aplicado.

**`MRS.WindowsBuilder/MainWindow.xaml`**: nuevo banner `NoRemovalsBanner`
("SIN ELIMINACIONES DE COMPONENTES / La imagen se generará sin eliminar
componentes del sistema."), oculto por defecto, dentro de la misma tarjeta
donde antes solo vivía la lista de componentes del plan — sin rediseñar
ninguna otra pantalla.

**No se ha modificado**: `ComponentCatalog`, `ProtectionEngine`,
`ProfileEngine`, `SecurityOptions`, `InstallationOptions`, `LabConfig`,
`Autounattend`, `PostInstall`, `BootWimProvisioner`, reglas de protección,
ni el diseño general del pipeline de `ISOEngine`.

## Tests

**`tests/MRS.RemovalEngine.Tests/RemovalEngineTests.cs`** (4 nuevos, sección
"P23: 0 SELECCIONADOS"):
- `Zero_selected_components_is_a_successful_completed_result_without_mounting`
- `Zero_selected_components_never_calls_DISM_at_all` (ni mount, ni commit, ni
  chequeo de índice, ni recuperación de montajes huérfanos)
- `Selections_that_are_all_blocked_are_different_from_zero_selected_and_still_mount_and_commit`
  (`TotalSelected=1, TotalAllowed=0` sigue montando/comprometiendo)
- `A_plan_with_both_allowed_and_zero_selected_components_never_mixes_the_two_cases`
  (una selección real + una bloqueada sigue el flujo normal de eliminación)

**`tests/MRS.ISOEngine.Tests/IsoGenerationPipelineTests.cs`** (1 nuevo):
- `An_explicit_RemovalPlan_with_zero_selected_components_still_reaches_oscdimg`
  (confirma que el pipeline llega hasta `oscdimg` con un `RemovalPlan` vacío
  explícito, no solo con `RemovalPlan = null`)

**Ya cubiertos por tests existentes, sin necesidad de añadir nada nuevo**:
- "RemovalPlan vacío válido" → `RemovalPlanBuilderTests.Empty_selection_produces_an_empty_but_valid_plan` (P06)
- "Selecciones bloqueadas siguen bloqueando" → múltiples tests en
  `RemovalPlanBuilderTests` (`Protected_component_is_blocked`,
  `Protected_appx_is_blocked`, `Protected_feature_is_blocked`,
  `Protected_capability_is_blocked`, etc.) — ninguno se modificó porque no se
  tocó `ComponentCatalog`/`ProtectionEngine`.
- "Selecciones permitidas siguen usando el flujo actual" → los tests
  preexistentes de eliminación real (`Successful_appx_removal_...`,
  `Feature_removal_calls_disable_feature`, etc.) siguen pasando sin cambios.

No hay ningún test de UI para "0 seleccionados permite abrir plan" porque no
existe ningún proyecto de test para `MRS.WindowsBuilder` (decisión ya
documentada en fases anteriores); el cambio se limita a una condición de una
línea (`ViewPlanButton.IsEnabled`) verificable por lectura de código.

## Resultado de tests

```
dotnet test MRS-Windows-Builder.sln
```

```
MRS.InstallationOptions.Tests : 17/17
MRS.ProfileEngine.Tests       : 21/21
MRS.ImageEngine.Tests         : 88/88
MRS.PostInstall.Tests         : 44/44
MRS.ComponentCatalog.Tests    : 59/59
MRS.RemovalPlanning.Tests     : 48/48
MRS.RemovalEngine.Tests       : 52/52   (48 previos + 4 nuevos de P23)
MRS.ISOEngine.Tests           : 103/103 (102 previos + 1 nuevo de P23)
```

Total: **432/432**, 0 fallos. Todos los tests existentes se mantienen sin
modificar (excepto los `using` añadidos por compatibilidad de compilación).

## Resultado del build

```
dotnet build MRS-Windows-Builder.sln
```

Todos los proyectos de motor (`MRS.DismEngine`, `MRS.ImageEngine`,
`MRS.ComponentCatalog`, `MRS.RemovalPlanning`, `MRS.RemovalEngine`,
`MRS.ProfileEngine`, `MRS.InstallationOptions`, `MRS.PostInstall`,
`MRS.ISOEngine`) compilan sin ningún error ni advertencia nueva, y los 8
proyectos de test compilan y pasan sus 432 pruebas (`dotnet test` sobre la
misma solución, que no depende de `MRS.WindowsBuilder`, termina en verde sin
ningún error).

**`MRS.WindowsBuilder.csproj` no completó el paso final de copia de salida**
en esta sesión: quedó un proceso `MRS.WindowsBuilder.exe` (PID 21264)
abierto desde una verificación manual de una fase anterior (P21/P22), y el
paso de MSBuild que copia las DLL de dependencias al directorio de salida de
`MRS.WindowsBuilder` (`bin\Debug\net8.0-windows\`) falla con
`MSB3027`/`MSB3021` ("The process cannot access the file... being used by
another process") porque ese `.exe` sigue teniendo esas DLL cargadas. Se
intentó cerrar ese proceso por tres vías (`Stop-Process -Force`, `taskkill
/F`, `CloseMainWindow()`) y las tres fallaron con "Acceso denegado" — esta
sesión en sandbox no tiene permiso para terminarlo, aunque pertenece al mismo
usuario. **Esto es un artefacto del entorno de esta sesión, no un error de
compilación**: los 14 errores del log son exclusivamente `MSB3027`/`MSB3021`
(fallo de copia por bloqueo de archivo); no aparece ningún error `CSxxxx`
(de compilador) en ningún proyecto, incluido `MRS.WindowsBuilder` — su
propio código (`.cs`/`.xaml`) compiló correctamente; solo falló copiar las
DLL ya compiladas de sus dependencias sobre el directorio de salida.

Para obtener una compilación completamente limpia de la solución: cerrar
manualmente la ventana de `MRS.WindowsBuilder.exe` que quedó abierta (o
reiniciar la sesión) y repetir `dotnet build MRS-Windows-Builder.sln`.

## Commit recomendado

`git commit` con el resumen "P23: permitir plan y generación con 0
eliminaciones — RemovalEngine evita el Mount/Commit vacío cuando
TotalSelected==0 (distinto de 'todo bloqueado'), UI permite ver el plan y
continuar sin seleccionar componentes; 432/432 tests sin regresiones (build
de MRS.WindowsBuilder bloqueado solo por un proceso previo de esta sesión,
no por un error de código)" y `git push` a `main`.
