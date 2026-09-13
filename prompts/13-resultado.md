# Resultado - P13: perfiles avanzados y opciones de seguridad

## Resumen

Se añade `SecurityOptions` (KeepDefender/KeepWindowsUpdate) como una decisión
explícita de protección, integrada en `MRS.ProfileEngine` (parte del
perfil) y en `MRS.ComponentCatalog` (parte de `ProtectionEngine`), sin
tocar `MRS.RemovalEngine`, el ciclo Mount/Execute/Verify/Commit/Discard, ni
la telemetría de P10. Mínimo, Ligero y Recomendado mantienen siempre
Defender y Windows Update protegidos, sin ninguna forma de excepción
accidental. Limpio y Personalizado permiten decidirlo desde la UI; "permitido"
solo significa que esa protección concreta deja de aplicarse — el
`RemovalPlan` sigue sin poder ejecutar una desactivación real (eso queda
para una fase posterior, como pide el prompt).

## Nuevos modelos

**`MRS.ComponentCatalog`** (usado por `ProtectionEngine`/`ComponentCatalogService`):
- `Models/SecurityFeature.cs` — enum `{ Defender, WindowsUpdate }`. Marca qué
  regla de protección pertenece a cada característica; el resto de reglas
  (Servicing Stack, CBS, LCU, WinRE, OOBE, frameworks, red, USB, audio,
  impresión...) no llevan ningún valor y nunca se ven afectadas.
- `Models/SecurityOptions.cs` — record `{ KeepDefender = true, KeepWindowsUpdate = true }`
  con `SecurityOptions.Safe` estático.
- `Rules/ProtectionRule.cs` — nueva propiedad `SecurityFeature? SecurityFeature`.

**`MRS.ProfileEngine`** (usado por `ProfileDefinition`):
- `Models/SecurityOptions.cs` — mismo shape (`KeepDefender`/`KeepWindowsUpdate`/`Safe`),
  pero es un **tipo propio**, no una referencia al de `MRS.ComponentCatalog`.
  Ver "Decisiones arquitectónicas".
- `Models/ProfileDefinition.cs` — nueva propiedad `SecurityOptions SecurityOptions`
  (por defecto `Safe`), `bool IsSecurityLocked` (true solo para los ids
  `minimal`/`light`/`recommended`) y `SecurityOptions EffectiveSecurityOptions`
  (siempre `Safe` si `IsSecurityLocked`; si no, `SecurityOptions` tal cual).

## Cambios en ProfileEngine

- `ProfileService.LoadFromDirectory` ahora parsea `securityOptions` del JSON
  (`ProfileFileModel.SecurityOptions`, tipo `Models.SecurityOptions?`). Si el
  JSON no lo declara (perfil de antes de P13), el perfil cargado usa
  `SecurityOptions.Safe` — nunca un valor inseguro por omisión.
- `ProfileDefinition.IsSecurityLocked`/`EffectiveSecurityOptions` son la única
  vía de lectura recomendada: da igual lo que declare el JSON de
  `minimal`/`light`/`recommended`, `EffectiveSecurityOptions` siempre
  devuelve `Safe` para esos tres ids. No es una opción de metadata
  configurable: es una política fija identificada por Id, tal como pide el
  prompt ("no debe existir ninguna forma accidental de saltarse esta
  protección").
- No se ha tocado `IProfileService`/`ProfileService.GetSelection`: sigue
  operando solo sobre `ComponentIds` (selección), sin relación con
  `SecurityOptions` (que se consume aparte, en `ProtectionEngine`).

## Cambios en ProtectionEngine / ComponentCatalog

- `ProtectionEngine.Apply(component, securityOptions = null)` /
  `ApplyAll(components, securityOptions = null)`: si una regla coincide y
  tiene `SecurityFeature`, se comprueba `securityOptions` (o `Safe` si es
  `null`); si la opción correspondiente es `false`, esa regla concreta deja
  de proteger — el bucle sigue evaluando el resto de reglas normalmente, así
  que cualquier otra protección que también coincidiera (no debería, pero la
  guarda no depende de eso) seguiría aplicándose.
- `ComponentCatalogService.BuildCatalog(inventory, securityOptions = null)`:
  parámetro opcional adicional, propagado a `ProtectionEngine.ApplyAll`. No
  ejecuta DISM: es la misma recomputación en memoria de siempre, solo que
  ahora puede repetirse con distintas `SecurityOptions` sobre el mismo
  inventario ya obtenido.
- Solo dos reglas llevan `SecurityFeature` (`windows-defender` →
  `Defender`, `windows-update` → `WindowsUpdate`), tanto en
  `DefaultCatalogRules` (embebido) como en `catalog/win11/protection-rules.json`
  (mirror externo, actualizado por consistencia aunque hoy no está
  conectado a ningún `ComponentCatalogService` real — ver P09/P11). El resto
  de reglas relacionadas con `WindowsUpdate` como categoría (Servicing
  Stack, CBS, SSU, LCU) **no** llevan `SecurityFeature.WindowsUpdate`: son
  parte del servicing stack, no de "Windows Update" en sí, y el prompt exige
  explícitamente que sigan protegidas aunque `KeepWindowsUpdate=false`.
  `SecurityHealth` tampoco lleva `SecurityFeature.Defender` por el mismo
  motivo (es el módulo de "Seguridad de Windows", no el motor de Defender).

## Cambios en RemovalPlanning

**Ninguno.** `RemovalPlanBuilder.Evaluate` ya leía `component.Protection`
directamente del catálogo recibido (`ComponentCatalogResult`) para decidir
`Allowed`/`BlockReason`; en cuanto `ProtectionEngine` deja de proteger
Defender/Windows Update con `KeepDefender=false`/`KeepWindowsUpdate=false`,
el `RemovalPlanItem` correspondiente refleja automáticamente ese cambio (ya
no aparece "Componente protegido" como `BlockReason`) sin tocar una sola
línea de `MRS.RemovalPlanning`. Ver "Decisiones arquitectónicas" para el
comportamiento exacto una vez "desprotegido".

## Cambios de UI

- `MainWindow.xaml` (pantalla COMPONENTES, dentro del panel PERFILES):
  nuevo `Border SecurityOptionsPanel` con dos sub-paneles:
  - `SecurityLockedPanel` (Mínimo/Ligero/Recomendado): dos filas fijas
    "🛡 Microsoft Defender · PROTEGIDO" y "🔄 Windows Update · PROTEGIDO",
    sin ningún control.
  - `SecurityConfigurablePanel` (Limpio/Personalizado): "OPCIONES AVANZADAS
    · Seguridad" con `KeepDefenderCheckBox`/`KeepWindowsUpdateCheckBox`
    (ambas empiezan marcadas) y un `TextBlock` de advertencia bajo cada una,
    oculto salvo que la casilla correspondiente esté desmarcada.
- `MainWindow.xaml.cs`:
  - `ApplyProfile` calcula `_currentSecurityOptions` a partir de
    `profile.EffectiveSecurityOptions`, llama a `UpdateSecurityOptionsPanel`
    y reconstruye el catálogo (`RebuildCatalogWithCurrentSecurityOptions`)
    antes de calcular la selección de componentes del perfil.
  - `UpdateSecurityOptionsPanel(profile)` decide qué sub-panel mostrar y
    sincroniza las casillas con `_currentSecurityOptions` (desconectando
    temporalmente sus manejadores para no disparar una reconstrucción
    redundante).
  - `SecurityOption_Changed` (Checked/Unchecked de ambas casillas): actualiza
    `_currentSecurityOptions`, muestra/oculta la advertencia correspondiente
    y llama a `RebuildCatalogWithCurrentSecurityOptions()`. Nunca ejecuta
    nada sobre Windows ni sobre la imagen: solo cambia una recomputación en
    memoria. Incluye una guarda de defensa en profundidad
    (`profile.IsSecurityLocked` ⇒ no hace nada) aunque estas casillas no se
    muestran nunca para los perfiles bloqueados.
  - `RebuildCatalogWithCurrentSecurityOptions()` (nuevo): reconstruye
    `_catalog`/`_allComponentRows` llamando a `ComponentCatalogService.BuildCatalog(_inventory, _currentSecurityOptions)`,
    preservando la selección manual previa por ComponentId (con la misma
    guarda `row.Editable` de siempre). No hace nada si aún no hay imagen
    analizada.
  - `PopulateComponentRows` (extraído de `ShowComponents`, ahora compartido
    con `RebuildCatalogWithCurrentSecurityOptions` para no duplicar la
    construcción de filas).
  - `ResetProfileBar` también colapsa `SecurityOptionsPanel` y reinicia
    `_currentSecurityOptions` a `Safe`.
  - Nuevo alias `using CatalogSecurityOptions = MRS.ComponentCatalog.Models.SecurityOptions;`
    (ver "Decisiones arquitectónicas": hay dos tipos `SecurityOptions`
    distintos, uno por proyecto, y `MainWindow` es el único punto que conoce
    ambos).

Las casillas de la pantalla inicial (preselección de perfil, P12) no llevan
controles de seguridad: el prompt pide mostrarlos "en la pantalla
COMPONENTES", así que solo viven ahí.

## Cambios en JSON

Los 5 perfiles (`profiles/minimal.json`, `light.json`, `recommended.json`,
`clean.json`, `custom.json`) incluyen ahora:

```json
"securityOptions": { "keepDefender": true, "keepWindowsUpdate": true }
```

Todos empiezan seguros, tal como exige el prompt. No se ha rellenado ningún
`componentIds` (siguen vacíos y documentados desde P11: sin un inventario
real de referencia no hay ComponentId definitivos que usar sin inventarlos).

`catalog/win11/protection-rules.json` (mirror externo, no conectado a
ningún servicio real hoy — ver P11) recibe el campo `"securityFeature"` en
las dos reglas correspondientes, por consistencia con `DefaultCatalogRules`.

## Decisiones arquitectónicas

1. **Dos tipos `SecurityOptions`, uno por proyecto, sin project reference
   nueva entre `MRS.ProfileEngine` y `MRS.ComponentCatalog`.** Ambos son
   simples pares de booleanos; duplicar un tipo de datos tan pequeño es más
   seguro que introducir una dependencia cruzada entre dos motores que hasta
   ahora eran independientes (decisión de P11, reafirmada aquí). `MainWindow`
   es el único punto que conoce los dos mundos y traduce uno al otro
   (`ToCatalogSecurityOptions`).
2. **"Bloqueado" se decide por Id, no por metadata.** `IsSecurityLocked`
   comprueba una lista fija (`minimal`/`light`/`recommended`) en
   `ProfileDefinition`, no un campo del JSON: así ningún perfil (ni siquiera
   uno cargado desde un archivo `minimal.json` manipulado) puede
   "convencer" al sistema de que no está bloqueado. Es la garantía dura que
   pide el prompt ("no debe existir ninguna forma accidental de saltarse
   esta protección").
3. **"Ya no protegido" no es lo mismo que "eliminable".** Cuando
   `KeepDefender=false`, `ProtectionEngine` deja de marcar Defender como
   `Protected`, pero su categoría (`Security`) sigue teniendo
   `ComponentProtection.Recommended` por defecto (`CategoryDefaults`), y
   `RemovalPlanBuilder.Evaluate` solo permite eliminar AppX/Features/Capabilities
   marcados `Removable`/`Optional`. El resultado es exactamente lo que pide
   el prompt: el plan puede reflejar que Defender "dejó de estar protegido"
   (su `BlockReason` cambia de "Componente protegido" a algo como "Solo se
   permite eliminar aplicaciones marcadas como Removable"), pero sigue sin
   generar ninguna acción real — sin tocar `RemovalPlanBuilder`, que ya
   tenía exactamente esta regla desde antes de P13.
4. **Reconstrucción del catálogo en memoria, no una segunda fuente de
   verdad.** Cambiar de perfil o tocar una casilla de seguridad vuelve a
   llamar a `ComponentCatalogService.BuildCatalog` sobre el mismo
   `_inventory` ya obtenido (sin DISM), preservando la selección manual por
   ComponentId. Es la única forma de que el "PROTEGIDO"/"permitido" de la UI
   y el `RemovalPlan` posterior sean siempre consistentes con la
   configuración de seguridad vigente, sin mantener dos copias del estado de
   protección.

## Tests

**`MRS.ComponentCatalog.Tests`** (+18, sobre `ProtectionEngineTests.cs` y
`ComponentCatalogServiceTests.cs`):
- Defender protegido con `SecurityOptions` nulo, con `KeepDefender=true`, y
  ya no bloqueado por esta regla con `KeepDefender=false`.
- Windows Update protegido con `KeepWindowsUpdate=true` y ya no bloqueado
  por esta regla con `KeepWindowsUpdate=false`.
- 8 protecciones críticas no relacionadas (Servicing Stack, WinRE, OOBE,
  red, audio, USB, impresión, framework) siguen protegidas aunque ambas
  opciones sean `false`.
- CBS/SSU/LCU/ServicingStack siguen protegidas con `KeepWindowsUpdate=false`
  (no son "Windows Update" en sí).
- `SecurityHealth` sigue protegido con `KeepDefender=false` (no es el motor
  de Defender).
- `BuildCatalog` de extremo a extremo (con `FakeInventory.Sample()`, que
  incluye un paquete de Defender): protegido con opciones nulas, ya no
  protegido con `KeepDefender=false`, y ServicingStack/WinRE siguen
  protegidos con ambas opciones en `false`.

**`MRS.ProfileEngine.Tests`** (+9, sobre `ProfileServiceTests.cs`):
- Un JSON sin `securityOptions` usa valores seguros por defecto.
- Un JSON puede declarar `securityOptions` explícitamente.
- Mínimo, Ligero y Recomendado (`[Theory]`) mantienen siempre Defender y
  Windows Update vía `EffectiveSecurityOptions`, aunque el JSON intente lo
  contrario.
- Limpio permite cambiar Defender / Windows Update por separado.
- Personalizado permite cambiar ambos.
- `SecurityOptions` por defecto (constructor sin argumentos) es siempre
  segura y equivale a `SecurityOptions.Safe`.

No se ha añadido ningún test que ejecute una desactivación real (no existe
tal código): la ausencia misma de una vía de ejecución en
`IProfileService`/`ProtectionEngine`/`ComponentCatalogService` para
"desactivar Defender/Windows Update" es la garantía de "P13 es
configuración + planificación + UI + protección, nunca ejecución".

Se ejecutaron también los tests ya existentes.

## Resultado de `dotnet test`

```
MRS.ProfileEngine.Tests     : 21/21  (+9)
MRS.ComponentCatalog.Tests  : 59/59  (+18)
MRS.ImageEngine.Tests       : 78/78
MRS.RemovalPlanning.Tests   : 48/48
MRS.RemovalEngine.Tests     : 48/48
```

Total: **254/254**. Sin regresiones.

## Resultado de `dotnet build`

`dotnet build MRS-Windows-Builder.sln`: **0 errores, 0 advertencias** en
toda la solución (incluido `MRS.WindowsBuilder`). A diferencia de fases
anteriores, en esta ejecución no había ninguna sesión elevada del usuario
bloqueando los binarios de salida, así que el build completo terminó limpio
sin necesidad de filtrar ningún `MSB3021`/`MSB3027`.

## Problemas encontrados

Ninguno funcional. Dos matices documentados arriba como decisiones, no como
problemas: (1) la duplicación deliberada del tipo `SecurityOptions` entre
`MRS.ProfileEngine` y `MRS.ComponentCatalog`, y (2) que "ya no protegido" no
se traduce automáticamente en "permitido eliminar" sin cambiar
`RemovalPlanBuilder` — que es exactamente lo que P13 pide no hacer todavía.

## README

Actualizado: cabecera de "Estado actual", nuevo párrafo P13 (y también uno
de P12, que se había quedado sin reflejar en el README por decisión
explícita de mantenerse estrictamente en el alcance de aquel prompt), y
Roadmap corregido — la numeración real de los prompts ya no coincidía con
la del Roadmap (P12 real = corrección de UI, no `PostInstall`/`ISOEngine`
como decía la lista); se renumeraron las entradas futuras a P14 en
adelante.

## Commit recomendado

`git commit` con el resumen "P13: perfiles avanzados y opciones de
seguridad (Defender/Windows Update, sin ejecutar ninguna desactivación
real)" y `git push` a `main`.
