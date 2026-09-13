# Resultado - P14: opciones avanzadas de seguridad en la pantalla inicial

## Problema encontrado

"OPCIONES AVANZADAS" (Mantener Microsoft Defender / Mantener Windows
Update, de P13) solo vivía en la pantalla COMPONENTES. Al elegir LIMPIO o
PERSONALIZADO en la pantalla inicial no pasaba nada visible: solo cambiaba
el perfil resaltado y aparecía el mensaje de log; había que llegar a
COMPONENTES para ver o tocar las casillas de seguridad.

## Solución

`_currentSecurityOptions` (P13) ya era la única fuente de verdad en
`MainWindow`; P14 no cambia eso ni la arquitectura de
ProfileEngine/ComponentCatalog (P12/P13), solo la integración de UI:

1. Se añadió en la pantalla inicial (tarjeta "Perfiles") el mismo panel de
   seguridad que ya existía en COMPONENTES (mismo texto, mismos avisos),
   con nombres de control propios (`PreSecurityOptionsPanel`,
   `PreKeepDefenderCheckBox`, `PreKeepWindowsUpdateCheckBox`,
   `PreDefenderWarningText`, `PreWindowsUpdateWarningText`) para no chocar
   con los de COMPONENTES.
2. Las 4 casillas (2 por pantalla) comparten el mismo manejador
   `SecurityOption_Changed`, que identifica cuál cambió por referencia al
   `sender`, actualiza `_currentSecurityOptions` y sincroniza
   inmediatamente las 4 casillas y las 4 advertencias
   (`SetSecurityCheckboxesWithoutTriggeringChange`) — así ambas pantallas
   siempre muestran el mismo estado, sin dos representaciones
   independientes.
3. `ApplyProfile` (ya existente desde P12, compartida por los 10 botones de
   perfil de ambas pantallas) ganó una regla explícita para decidir cuándo
   `_currentSecurityOptions` se reinicia a los valores por defecto del
   perfil y cuándo se conserva (ver "Comportamiento de cada perfil" más
   abajo) — sin tocar `ProfileEngine`/`ComponentCatalog`, solo esta
   decisión de orquestación en `MainWindow`.
4. `ShowComponents` conserva explícitamente el `_currentSecurityOptions`
   elegido en la pantalla inicial al generar el catálogo real (capturado
   antes de cualquier reinicio, y restaurado justo antes de reaplicar el
   perfil con `ApplyProfile(..., preserveCurrentSecurityOptions: true)`).

## Archivos modificados

- `src/MRS.WindowsBuilder/MainWindow.xaml`:
  - Panel derecho "Perfiles" (pantalla inicial): nuevo `Border PreSecurityOptionsPanel`
    con "OPCIONES AVANZADAS" + 2 casillas + 2 advertencias, oculto por defecto.
  - Texto de advertencia de COMPONENTES actualizado a la misma redacción
    ("⚠ Microsoft Defender/Windows Update se desactivará al generar la
    imagen.") para que ambas pantallas digan exactamente lo mismo.
- `src/MRS.WindowsBuilder/MainWindow.xaml.cs`:
  - `ApplyProfile` gana el parámetro `preserveCurrentSecurityOptions` y la
    regla de reinicio/conservación (ver más abajo); captura `previousProfile`
    antes de sobrescribir `_activeCatalogProfileId`.
  - `UpdateSecurityOptionsPanel` ahora actualiza también
    `PreSecurityOptionsPanel` (visible solo para Limpio/Personalizado, sin
    badges de "protegido" en la pantalla inicial, tal como pide el mockup
    de P14).
  - Nuevo `SetSecurityCheckboxesWithoutTriggeringChange` (sincroniza las 4
    casillas/4 advertencias desde `_currentSecurityOptions` sin disparar el
    manejador) y 4 propiedades auxiliares (`DefenderCheckBoxes`,
    `WindowsUpdateCheckBoxes`, `DefenderWarningTexts`,
    `WindowsUpdateWarningTexts`) que agrupan los controles de ambas
    pantallas.
  - `SecurityOption_Changed` reescrito para identificar la casilla por
    `sender` (sirve para las 4, no solo las de COMPONENTES), comparar con
    el valor anterior y solo registrar en el log / reconstruir el catálogo
    si hubo un cambio real (nunca por una sincronización entre pantallas).
    Formato de log ajustado a `[SECURITY] Mantener Microsoft Defender: false`
    / `[SECURITY] Mantener Windows Update: true`, una línea por opción
    realmente cambiada.
  - `ShowComponents` captura `_activeCatalogProfileId`/`_currentSecurityOptions`
    antes de `ResetProfileBar()` y los restaura justo antes de reaplicar el
    perfil preseleccionado.
  - `ResetProfileBar` también colapsa `PreSecurityOptionsPanel`.
- `README.md`: nuevo párrafo P14 y entrada de Roadmap (renumerada: el
  antiguo "P14" de PostInstall/ISOEngine pasa a P15).

No se ha tocado `MRS.ProfileEngine`, `MRS.ComponentCatalog`,
`MRS.RemovalPlanning`, `MRS.RemovalEngine`, el ciclo Mount/Execute/Verify/
Commit/Discard, `WorkingImageFactory`, `DismRunner`, `SafeUnmountAsync`, la
gestión de workspaces ni la telemetría de progreso de P10.

## Comportamiento de cada perfil

| Perfil | Opciones avanzadas | Al entrar desde... |
|---|---|---|
| Mínimo/Ligero/Recomendado | Ocultas en ambas pantallas (COMPONENTES muestra los badges "PROTEGIDO" de P13) | `_currentSecurityOptions` siempre se fuerza a `Safe` (true/true), sin excepción |
| Limpio/Personalizado, viniendo de Mínimo/Ligero/Recomendado (o sin perfil previo) | Visibles, casillas en `Safe` (ambas marcadas) | Los perfiles seguros siempre parten de valores seguros (Caso C del prompt) — nunca se hereda una configuración insegura |
| Limpio ↔ Personalizado (entre ellos) | Visibles, **se conserva** la configuración tal cual estaba | Caso A del prompt: cambiar de Limpio a Personalizado (o viceversa) no reinicia nada |
| Pantalla inicial → COMPONENTES (mismo perfil) | Visibles, **se conserva** la configuración elegida antes de pulsar "Continuar" | Caso del punto 2/8 del prompt: nunca vuelve a los valores por defecto solo por cambiar de pantalla |

Desmarcar cualquiera de las dos casillas (en cualquiera de las dos
pantallas) muestra su advertencia correspondiente inmediatamente y no
ejecuta ninguna acción sobre Windows ni sobre la imagen: solo cambia
`_currentSecurityOptions` y recalcula el catálogo en memoria sobre el mismo
`_inventory` ya obtenido (cuando existe), sin ninguna llamada a DISM
adicional.

## Tests

No se añadieron tests nuevos de código. Motivo, detallado a continuación:

- **Items 1-9, 12 de la lista del prompt** (Mínimo/Ligero/Recomendado
  fuerzan Defender/Windows Update protegidos; Limpio/Personalizado permiten
  modificarlos; cambiar a un perfil seguro restaura la protección; el
  comportamiento de P13 no se rompe) ya están cubiertos por los tests de
  P13 en `MRS.ProfileEngine.Tests`/`MRS.ComponentCatalog.Tests`
  (`Minimal_light_and_recommended_always_keep_Defender_and_WindowsUpdate`,
  `Clean_allows_changing_Defender_...`, `Clean_allows_changing_WindowsUpdate_...`,
  `Custom_allows_changing_both_...`, etc.) — P14 no cambia
  `ProfileEngine`/`ComponentCatalog`, así que esos tests siguen siendo la
  prueba correcta y se han vuelto a ejecutar sin ningún cambio.
- **Item 11** ("no se generan ComponentIds inventados") es una propiedad
  estructural: P14 no añade ningún camino nuevo que produzca o modifique
  ComponentId; sigue sin existir tal código.
- **Item 10** ("SecurityOptions se conserva entre selección inicial y
  COMPONENTES") es puramente orquestación de `MainWindow` (WPF): la regla
  vive en `ApplyProfile`/`ShowComponents`, no en `ProfileEngine` ni en
  `ComponentCatalog`, y este repositorio no tiene infraestructura de tests
  de UI para `MRS.WindowsBuilder` (ver P11/P12/P13: la misma limitación ya
  documentada). Siguiendo la instrucción explícita del prompt ("No crear
  tests WPF complejos si no existe infraestructura para ello"), se verificó
  con la traza manual del código (ver "Comportamiento de cada perfil" y los
  tres casos A/B/C del prompt, seguidos paso a paso contra el código) en
  vez de un test automatizado.

Se ejecutaron todos los tests existentes.

## Resultado de `dotnet test`

```
MRS.ProfileEngine.Tests     : 21/21
MRS.ImageEngine.Tests       : 78/78
MRS.ComponentCatalog.Tests  : 59/59
MRS.RemovalPlanning.Tests   : 48/48
MRS.RemovalEngine.Tests     : 48/48
```

Total: **254/254**. Sin regresiones (idénticos a los de P13: P14 no tocó
ningún proyecto con tests).

## Resultado de `dotnet build`

`dotnet build MRS-Windows-Builder.sln`: **0 errores, 0 advertencias** en
toda la solución.

## Prueba visual

No se dispuso de una sesión interactiva para hacer clic manualmente sobre
la aplicación WPF en ejecución (igual que en fases anteriores, la
verificación end-to-end sobre Windows real la ha hecho el usuario). En su
lugar se trazó el código paso a paso contra los 12 puntos de la prueba
manual mínima del prompt:

1-4. Recomendado (bloqueado, sin panel) → Limpio: `UpdateSecurityOptionsPanel`
oculta `SecurityLockedPanel`/badges y muestra `SecurityConfigurablePanel` +
`PreSecurityOptionsPanel`; `_currentSecurityOptions` se reinicia a `Safe`
(Caso C) — ambas casillas aparecen marcadas.

5-7. Desmarcar Defender / Windows Update: `SecurityOption_Changed` detecta
el cambio real, muestra la advertencia correspondiente
(`DefenderWarningText`/`WindowsUpdateWarningText` y sus equivalentes `Pre*`)
y sincroniza ambas pantallas.

8. Personalizado tras Limpio con casillas ya modificadas: `previousProfile`
(Limpio) no está bloqueado ⇒ se conserva `_currentSecurityOptions` (Caso A).

9. Volver a Recomendado: `profile.IsSecurityLocked` ⇒
`_currentSecurityOptions = Safe` sin condiciones; ambos paneles de casillas
se ocultan.

10. Volver a Limpio: `previousProfile` (Recomendado) está bloqueado ⇒ se
reinicia a los valores seguros por defecto del perfil (ambas marcadas).

11. "Continuar" hacia COMPONENTES: `ShowComponents` captura
`_activeCatalogProfileId`/`_currentSecurityOptions` antes de
`ResetProfileBar()` y los restaura con
`ApplyProfile(id, preserveCurrentSecurityOptions: true)` — la configuración
elegida en la pantalla inicial llega intacta a COMPONENTES.

12. Ninguna de las rutas anteriores llama a `IWorkingImageFactory`,
`IRemovalEngine` ni a ningún método de `IDismRunner`: solo
`ComponentCatalogService.BuildCatalog` (pura recomputación en memoria sobre
un inventario ya obtenido). No se ejecuta ninguna eliminación real.

Se recomienda que el usuario repita esta prueba de forma interactiva sobre
la aplicación en ejecución como confirmación final, como en fases
anteriores.

## Commit recomendado

`git commit` con el resumen "P14: opciones avanzadas de seguridad también
en la pantalla inicial (misma fuente de verdad que COMPONENTES)" y
`git push` a `main`.
