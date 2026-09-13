# Resultado - P12: completar perfiles en la UI

## Resumen

La pantalla inicial (panel "Origen" / "Información de la imagen" / "Perfiles")
todavía mostraba un widget de perfiles **anterior a P11**: 3 `RadioButton`
con las etiquetas `NORMAL` / `LIGHT` / `MEDIUM`, que solo escribían un
`string` en un campo (`_selectedProfile`) sin llamar nunca a `ProfileService`
ni a nada del catálogo real. Coexistía con la barra de perfiles correcta de
P11 (Mínimo/Ligero/Recomendado/Limpio/Personalizado) que ya vive en la
pantalla COMPONENTES.

Se ha sustituido ese widget legado por los 5 botones reales de P11, usando
**el mismo manejador y la misma lógica de aplicación** que ya existía en
COMPONENTES (no se ha escrito una segunda implementación). Como la pantalla
inicial todavía no tiene catálogo, pulsar un perfil ahí queda como
preselección; en cuanto se genera el catálogo real (`ShowComponents`), esa
preselección se aplica automáticamente reutilizando la misma función.

No se ha creado ningún perfil nuevo, no se han tocado los JSON de
`profiles/`, y no se ha modificado `RemovalEngine`, `RemovalPlanning`,
`ComponentCatalog`, `ProtectionEngine`, el ciclo Mount/Execute/Verify/
Commit/Discard ni la telemetría de P10.

## Archivos modificados

- `src/MRS.WindowsBuilder/MainWindow.xaml`:
  - Eliminado el estilo `ProfileButton` (plantilla de `RadioButton`) y los 3
    `RadioButton` `ProfileNormal`/`ProfileLight`/`ProfileMedium` (`NORMAL`/
    `LIGHT`/`MEDIUM`) del panel derecho de la pantalla inicial.
  - Sustituidos por 5 `Button` (`ProfilePreMinimalButton`/`ProfilePreLightButton`/
    `ProfilePreRecommendedButton`/`ProfilePreCleanButton`/`ProfilePreCustomButton`)
    dentro de un `WrapPanel`, con el mismo `Tag` (id real del perfil) y el
    mismo `Click="ProfileButton_Click"` que ya usaba la barra de COMPONENTES.
  - La barra de perfiles de COMPONENTES cambia de `StackPanel Orientation="Horizontal"`
    a `WrapPanel`, para que los 5 botones envuelvan a una segunda línea en
    vez de recortarse si la ventana es estrecha (requisito de layout).
- `src/MRS.WindowsBuilder/MainWindow.xaml.cs`:
  - Eliminado `Profile_Checked` y el campo `_selectedProfile` (dead code:
    nunca llamaba a `ProfileService`).
  - `ProfileButton_Click` ahora solo extrae el `profileId` del `Tag` y delega
    en un nuevo método `ApplyProfile(string profileId)` que contiene toda la
    lógica que antes estaba inline (idéntica a la de P11): así los 10
    botones (5 en la pantalla inicial + 5 en COMPONENTES) comparten un único
    punto de lógica, no dos.
  - Nuevo `ProfileButtons` (propiedad que enumera los 10 botones por Tag) y
    `HighlightActiveProfileButton(string profileId)` (antes recibía un
    `Button`; ahora un `id`, y resalta en ambas zonas a la vez comparando
    `Tag`, no solo el botón pulsado).
  - `ShowComponents` guarda el perfil activo antes de `ResetProfileBar()` y,
    si había uno preseleccionado (desde la pantalla inicial, o de un
    catálogo anterior), vuelve a llamar a `ApplyProfile(...)` — ahora con
    ComponentId reales — inmediatamente después de generar el catálogo.

No se ha tocado ningún archivo de `MRS.ProfileEngine`, `MRS.RemovalEngine`,
`MRS.RemovalPlanning` ni `MRS.ComponentCatalog`.

## Perfiles mostrados

Ambas zonas (pantalla inicial y pantalla COMPONENTES) muestran exactamente:

```
MÍNIMO   LIGERO   RECOMENDADO   LIMPIO   PERSONALIZADO
```

Ya no queda ninguna referencia a `NORMAL`, `LIGHT` ni `MEDIUM` en el código
fuente (verificado con una búsqueda global en `src/MRS.WindowsBuilder`).

## Mapeo UI → ProfileDefinition

| Botón (`Content`) | `Tag` / Id real (`ProfileDefinition.Id`) | Archivo |
|---|---|---|
| Mínimo | `minimal` | `profiles/minimal.json` |
| Ligero | `light` | `profiles/light.json` |
| Recomendado | `recommended` | `profiles/recommended.json` |
| Limpio | `clean` | `profiles/clean.json` |
| Personalizado | `custom` | `profiles/custom.json` |

El mapeo es directo: `Button.Tag` es el `Id` real del perfil tal como lo
carga `ProfileService.LoadFromDirectory`, y `ProfileButton_Click` lo pasa
sin transformar a `_profileService.GetProfile(...)`. No hay ninguna tabla
de traducción adicional ni ningún id inventado.

## Comportamiento (idéntico en las dos zonas, sin duplicar lógica)

1. Se resuelve el perfil real con `ProfileService.GetProfile`.
2. Si es "Personalizado" (`ProfileDefinition.IsCustom`), no se toca ninguna
   casilla: se informa de que se mantiene la selección manual.
3. En caso contrario, se calcula `ProfileService.GetSelection(profile,
   knownIds, protectedIds)` con los ComponentId del catálogo real (vacío si
   todavía no hay catálogo) y se aplica a `_allComponentRows`, respetando
   siempre `row.Editable` — **un componente protegido nunca se marca**, sea
   lo que sea lo que pida el perfil.
4. Si se pulsa desde la pantalla inicial (sin catálogo todavía), el paso 3
   no tiene nada que marcar (no hay filas) y el resultado queda solo como
   preselección (`_activeCatalogProfileId`). En cuanto `ShowComponents`
   genera el catálogo real, se vuelve a invocar automáticamente el mismo
   `ApplyProfile`, esta vez con ComponentId reales.

## Tests

No se ha añadido ningún test nuevo. Motivo:

- El mapeo UI → perfil es una correspondencia 1:1 trivial entre `Button.Tag`
  y `ProfileDefinition.Id` (sin tabla de traducción, sin lógica condicional
  que pueda tener un caso mal cubierto); se verifica por inspección directa
  del XAML/code-behind y por la ausencia de errores de compilación.
- La lógica que sí tiene comportamiento no trivial (`GetProfile`,
  `GetSelection`, filtrado de desconocidos/protegidos, detección de
  "Personalizado") ya está cubierta por los 12 tests de
  `ProfileServiceTests` de P11, que no se han modificado ni necesitan
  cambios: `ApplyProfile` sigue llamando exactamente a los mismos métodos de
  `IProfileService` con los mismos argumentos que antes.
- Este repositorio no tiene un proyecto de tests de UI para
  `MRS.WindowsBuilder` (WPF) — introducir uno estaría fuera del alcance de
  P12 ("no hagas cambios fuera de este alcance").

Se han ejecutado todos los tests existentes:

```
MRS.ProfileEngine.Tests     : 12/12
MRS.ImageEngine.Tests       : 78/78
MRS.ComponentCatalog.Tests  : 41/41
MRS.RemovalPlanning.Tests   : 48/48
MRS.RemovalEngine.Tests     : 48/48
```

Total: **227/227**, sin regresiones.

## Resultado del build

`dotnet build src/MRS.WindowsBuilder/MRS.WindowsBuilder.csproj`: el XAML y el
C# compilan sin ningún `CS####`/`MC####` (verificado filtrando la salida).
Los únicos errores del build completo de la solución son los
`MSB3021`/`MSB3027` ya conocidos: la sesión elevada del usuario
(`MRS.WindowsBuilder.exe`, PID 8528 en esta ejecución) bloquea la copia de
los `.dll`/`.exe` de salida — no es un error de código, igual que en fases
anteriores.

## Problemas encontrados

Ninguno funcional. Única decisión de diseño a señalar: la pantalla inicial
no tenía ningún catálogo contra el que aplicar un perfil de verdad (no
existía antes de P11), así que en vez de dejar esos 5 botones sin ningún
efecto visible (lo que habría sido una "zona de perfiles" decorativa y
confusa) se implementó la preselección + aplicación automática al generar
el catálogo, reutilizando `ApplyProfile` sin crear una segunda lógica. Es el
mínimo cambio de comportamiento necesario para que los 5 botones de la
pantalla inicial tengan un efecto real y coherente con "mantener el
comportamiento implementado en P11".

## Commit recomendado

`git commit` con el resumen "P12: corrige los botones de perfil legados
(NORMAL/LIGHT/MEDIUM) por los 5 perfiles reales de P11 en toda la UI" y
`git push` a `main`.
