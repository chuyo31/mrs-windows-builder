# Resultado - P11: Profile Engine

## Resumen

Se implementa `MRS.ProfileEngine`: un motor de perfiles que carga definiciones
desde `profiles/*.json` y produce una **selección de ComponentId candidatos**.
No ejecuta nada, no conoce DISM ni WPF, y no decide protección ni cascadas:
la decisión final de qué se puede eliminar sigue pasando siempre por
`MRS.ComponentCatalog` (ProtectionEngine) y `MRS.RemovalPlanning`. En la UI,
un perfil solo marca/desmarca casillas en la pantalla COMPONENTES —
exactamente como si el usuario lo hiciera a mano— y nunca dispara ninguna
modificación de la imagen.

`RemovalEngine` y el ciclo Mount/Execute/Verify/Commit/Discard de P07-P10
**no se han tocado en absoluto**.

## Archivos creados/modificados

**Nuevo proyecto `MRS.ProfileEngine`** (sin ninguna referencia a otros
proyectos: solo `System.Text.Json`):
- `src/MRS.ProfileEngine/Models/ProfileDefinition.cs`
- `src/MRS.ProfileEngine/Models/ProfileValidationErrorCode.cs`
- `src/MRS.ProfileEngine/Models/ProfileValidationError.cs`
- `src/MRS.ProfileEngine/Models/ProfileLoadResult.cs`
- `src/MRS.ProfileEngine/Models/ProfileSelectionResult.cs`
- `src/MRS.ProfileEngine/IProfileService.cs`
- `src/MRS.ProfileEngine/ProfileService.cs`
- (eliminado el `Class1.cs` de stub)

**Perfiles JSON** (nuevos, en `profiles/`):
- `profiles/minimal.json`, `light.json`, `recommended.json`, `clean.json`, `custom.json`

**Tests** (nuevo proyecto):
- `tests/MRS.ProfileEngine.Tests/MRS.ProfileEngine.Tests.csproj`
- `tests/MRS.ProfileEngine.Tests/ProfileServiceTests.cs` (12 tests)

**UI (`MRS.WindowsBuilder`)**:
- `src/MRS.WindowsBuilder/MRS.WindowsBuilder.csproj` — `ProjectReference` a `MRS.ProfileEngine`.
- `src/MRS.WindowsBuilder/MainWindow.xaml` — nueva barra "PERFILES" (5 botones + texto informativo) en la pantalla COMPONENTES, entre la cabecera y la búsqueda/filtro.
- `src/MRS.WindowsBuilder/MainWindow.xaml.cs` — `LoadProfiles()`/`ResolveProfilesDirectory()` (llamados desde el constructor), `ProfileButton_Click`, `HighlightActiveProfileButton`, `ResetProfileBar` (llamado desde `ShowComponents`).

**Solución**:
- `MRS-Windows-Builder.sln` — añadido `MRS.ProfileEngine.Tests` (el proyecto `MRS.ProfileEngine` ya estaba registrado como stub desde el arranque del repositorio).

No se ha tocado `MRS.RemovalEngine`, `MRS.RemovalPlanning`, `MRS.ComponentCatalog` ni ningún archivo relacionado con el ciclo Mount/Execute/Verify/Commit/Discard.

## Formato JSON

```json
{
  "id": "recommended",
  "name": "Recomendado",
  "description": "...",
  "version": 1,
  "componentIds": ["appx:...", "feature:...", "package:...", "capability:...", "driver:..."],
  "metadata": { "kind": "custom" }
}
```

`metadata` es opcional (diccionario `string`→`string`); solo se usa hoy para
marcar el perfil "Personalizado" (`"kind": "custom"`).

## Perfiles implementados

| Archivo | Id | Nombre | ComponentIds |
|---|---|---|---|
| `minimal.json` | `minimal` | Mínimo | `[]` (pendiente, ver más abajo) |
| `light.json` | `light` | Ligero | `[]` (pendiente) |
| `recommended.json` | `recommended` | Recomendado | `[]` (pendiente) |
| `clean.json` | `clean` | Limpio | `[]` (pendiente) |
| `custom.json` | `custom` | Personalizado | `[]` — por diseño: representa la selección manual, nunca una lista fija |

## ComponentId realmente utilizados

**Ninguno de los 4 perfiles predefinidos** (`minimal`/`light`/`recommended`/`clean`)
se ha rellenado con ComponentId reales. Motivo, explícito por instrucción del
propio prompt ("si el catálogo actual no contiene suficientes ComponentId
definitivos... crear los perfiles estructuralmente válidos pero dejar
documentados los elementos pendientes"):

- `ComponentDefinition.Id` (en `MRS.ComponentCatalog.Classification.CatalogClassifier`)
  se construye como `appx:<PackageName>`, `package:<PackageIdentity>`,
  `feature:<Name>`, `capability:<Identity>` o `driver:<PublishedName>` — y
  esos valores **dependen del inventario real de una ISO concreta** (DISM),
  no existen como catálogo estático en el repositorio.
- `catalog/win11/components.json` solo contiene **patrones de clasificación**
  (subcadenas como `"Clipchamp"`, `"Teams"`, `"Xbox"`) para asignar categoría,
  no identificadores completos de paquete/feature/capability tal y como los
  devolvería DISM en una imagen real.
- Los `"appx:Clipchamp"` que aparecen en los tests de `MRS.RemovalEngine.Tests`
  son fixtures de prueba (`PlanFactory`), no una fuente de verdad del catálogo.

Rellenar los 4 perfiles con IDs "razonables" habría significado inventar
nombres de paquete que podrían no coincidir con los reales de una imagen
concreta — exactamente lo que el prompt prohíbe explícitamente
("NO rellenar listas con IDs inventados"). Cada archivo JSON documenta esta
limitación en su propio campo `description`.

`custom.json` es la única excepción por diseño: su `componentIds` vacío no es
una limitación sino la definición correcta del perfil (ver más abajo).

## Decisiones arquitectónicas

1. **`MRS.ProfileEngine` no referencia ningún otro proyecto** (ni
   `MRS.ComponentCatalog` ni `MRS.RemovalPlanning`). Trabaja solo con
   `string` (ComponentId) y colecciones de `string` que le pasa el llamador.
   Esto mantiene la separación de responsabilidades exigida: el perfil no
   puede, ni por accidente, acoplarse al modelo interno del catálogo o
   evaluar protección por sí mismo. La comprobación de "¿existe este
   ComponentId en el catálogo real?" y "¿está protegido?" se hace en
   `MainWindow` pasando `IReadOnlyCollection<string>` (los `Id` de
   `_allComponentRows` filtrados por `Editable`/no-`Editable`), no dentro de
   `ProfileService`.

2. **`ProfileEngine.GetSelection` informa de conflictos, nunca decide.**
   Un ComponentId de un perfil que no exista en el catálogo actual se
   reporta en `UnknownComponentIds` y **nunca se selecciona**. Un
   ComponentId que sí existe pero está marcado como protegido por el
   llamador se reporta en `BlockedComponentIds` **a título informativo**
   (sigue apareciendo en `SelectedComponentIds`, porque desde el punto de
   vista de ProfileEngine "querer eliminarlo" y "poder eliminarlo" son cosas
   distintas). La aplicación real a la UI (`ProfileButton_Click`) es la que
   impone la garantía dura: `row.IsSelected = row.Editable && ...` — un
   componente protegido nunca queda marcado, sin excepción, sea lo que sea
   lo que reporte el perfil.

3. **"Personalizado" se detecta por convención, no por un tipo aparte.**
   `ProfileDefinition.IsCustom` es `true` si `Id == "custom"` o si
   `Metadata["kind"] == "custom"`. Al activar este perfil, `MainWindow` no
   toca ninguna casilla: solo dice al usuario que se mantiene su selección
   manual. Se prefirió esto a bifurcar el modelo de datos (`ProfileDefinition`
   vs. una clase `CustomProfile` distinta) porque el prompt pedía "la
   solución más sencilla sin romper el flujo existente".

4. **Carga de perfiles: un archivo inválido no bloquea el resto.**
   `ProfileService.LoadFromDirectory` recorre todos los `*.json` del
   directorio; cada archivo se valida de forma independiente (JSON
   malformado, `id` ausente, `id` duplicado, `version <= 0`) y un error en
   uno no impide cargar los demás. Los duplicados se resuelven por orden
   alfabético de archivo: el primero gana, los siguientes se reportan como
   error `DuplicateId` y no se añaden al resultado.

5. **Resolución de la carpeta `profiles/` en tiempo de ejecución.** Igual que
   `catalog/`, `profiles/` no se copia al `bin/` de `MRS.WindowsBuilder` (no
   se ha tocado ningún `.csproj` para añadir un `Content`/`CopyToOutputDirectory`
   fuera del alcance de P11). `ResolveProfilesDirectory()` busca una carpeta
   `profiles` con al menos un `*.json` subiendo desde `AppContext.BaseDirectory`
   hasta 8 niveles — funciona en desarrollo (repo clonado) sin necesitar
   recompilar ni copiar nada. Si no se encuentra, se registra un error
   `DirectoryNotFound` en el log y la barra de perfiles queda vacía de
   perfiles pero no rompe la aplicación.

## UI

Pantalla COMPONENTES, nueva sección "PERFILES" entre la cabecera y el
buscador: 5 botones (Mínimo / Ligero / Recomendado / Limpio / Personalizado)
y un texto informativo. Al pulsar un perfil no-personalizado:

1. Se calcula `GetSelection(profile, knownIds, protectedIds)` con los
   ComponentId del catálogo real ya cargado.
2. Se marca/desmarca cada fila (`ComponentRow.IsSelected`) según el
   resultado, respetando siempre `Editable` (protegidos nunca se marcan).
3. Se refresca la grilla y el resumen (`Seleccionados / Permitidos /
   Bloqueados / Advertencias`) exactamente igual que si el cambio viniera
   de un checkbox manual — no hay ningún camino nuevo hacia `RemovalPlan`.
4. El texto informativo muestra cuántos componentes selecciona, cuántos
   IDs del perfil no existen en este catálogo y cuántos quedarían
   bloqueados por protección.

Un catálogo nuevo (nueva ISO o edición) reinicia la barra
(`ResetProfileBar`, llamado desde `ShowComponents`). El botón "Personalizado"
no cambia ninguna casilla; solo informa de que se mantiene la selección
manual.

No se ejecuta ninguna modificación de la imagen en ningún punto de este
flujo: seguir hasta "Ver plan" y "Aplicar cambios" sigue siendo obligatorio
y sigue pasando por el aviso de P07 y por `RemovalPlanBuilder`.

## Tests

`tests/MRS.ProfileEngine.Tests/ProfileServiceTests.cs` (12 tests, sin
depender de WPF, DISM ni ningún catálogo real):

- Carga correcta de un JSON bien formado.
- `GetProfile` devuelve `null` para un id inexistente.
- JSON inválido se reporta como error sin bloquear la carga del resto.
- Ids duplicados se reportan como error (se conserva el primero, alfabético).
- Falta de `id` se reporta como error.
- Directorio inexistente se reporta como error sin lanzar excepción.
- `GetSelection` marca como desconocidos los ComponentId que no existen en
  la colección `knownComponentIds` y nunca los selecciona.
- `GetSelection` selecciona todo cuando no se pasa `knownComponentIds`.
- `GetSelection` marca como bloqueados (informativo) los ComponentId
  protegidos, sin dejar de reportarlos como seleccionados.
- Los perfiles son independientes entre sí (cargar dos no mezcla sus listas).
- El perfil "Personalizado" se detecta por convención (`IsCustom`) y no
  impone selección.
- Verificación de que `IProfileService` no expone ningún método de
  ejecución (`Execute`/`Run`): el contrato es solo "cargar" y "calcular una
  selección de strings".

## Resultado de `dotnet test`

```
MRS.ProfileEngine.Tests     : 12/12  (nuevo)
MRS.ImageEngine.Tests       : 78/78
MRS.ComponentCatalog.Tests  : 41/41
MRS.RemovalPlanning.Tests   : 48/48
MRS.RemovalEngine.Tests     : 48/48
```

Total: **227/227**. Sin regresiones en ningún test de P01-P10.

## Resultado de `dotnet build`

`MRS.ProfileEngine`, `MRS.ProfileEngine.Tests` y los 4 proyectos de test
existentes: **0 errores, 0 advertencias**. `MRS.WindowsBuilder` compila su
XAML y C# (incluida la nueva barra de perfiles) sin ningún `CS####`/`MC####`.
El build de la solución completa solo falla por `MSB3021`/`MSB3027`
(el `.exe` de la sesión elevada del usuario bloquea la copia del binario),
igual que en fases anteriores — no es un error de código.

## Problemas encontrados / limitaciones documentadas

- **Los 4 perfiles predefinidos no tienen ComponentId reales todavía**, por
  la razón explicada arriba (el catálogo depende del inventario real de una
  ISO concreta, y el repositorio no contiene ningún catálogo estático de
  referencia). Quedan estructuralmente válidos, cargables, seleccionables
  por id y listos para poblarse en el momento en que se disponga de un
  inventario real de referencia (por ejemplo, analizando una ISO concreta y
  copiando los `Id` reales que produzca `CatalogClassifier` para los
  componentes que se quieran incluir en cada perfil). Este es el único punto
  pendiente de P11; no se ha inventado ningún dato para disimularlo.
- No se ha añadido copia automática de `profiles/*.json` al `bin/` de
  `MRS.WindowsBuilder` (fuera del alcance de P11, y `catalog/*.json` tampoco
  se copia hoy); la resolución de ruta en tiempo de ejecución cubre el caso
  de desarrollo (repo clonado) sin tocar ningún `.csproj` de publicación.

## Commit recomendado

`git commit` con el resumen "P11: Profile Engine (perfiles JSON con
selección de ComponentId, sin tocar RemovalEngine)" y `git push` a `main`.
