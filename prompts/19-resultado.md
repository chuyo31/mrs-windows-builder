# Resultado - P19: ISOEngine — pipeline completo de generación

## Resumen

Se implementa el pipeline real de generación de ISO que el prompt describe
como diagrama: `MRS.ISOEngine` gana un `IsoGenerationPipeline` que une, en
un único flujo, todas las piezas ya construidas en fases anteriores —
análisis/inventario de ISO (P02-P04), catálogo/RemovalPlan (P05-P06),
RemovalEngine (P07), InstallationOptions/boot.wim/LabConfig/autounattend
(P15-P16), PostInstall (P18) — sin reescribir ninguna de ellas. Se añade
además la pieza que faltaba para que el resultado pudiera ser una ISO
arrancable de verdad: copiar el árbol **completo** de la ISO original
(bootmgr, `boot\`, `efi\`, etc.), no solo los WIM concretos que tocaban las
fases anteriores.

**No se pudo ejecutar el pipeline completo contra una ISO real** dentro de
esta sesión, por dos motivos independientes, ambos ya documentados en fases
anteriores y confirmados de nuevo aquí: (1) DISM exige privilegios elevados
(P17) — esta sesión no está elevada —, y (2) `oscdimg.exe` (Windows ADK)
no está instalado en este entorno. Todo el código está, en cambio,
**validado con 25 tests nuevos** usando DISM/registro/oscdimg simulados,
igual que en fases anteriores.

## El pipeline implementado

```
ISO ORIGINAL
    ↓
VALIDACIÓN                    IsoGenerationRequestValidator
    ↓                         (reutiliza InstallationExecutionValidator de P15/P16
    ↓                          y PostInstallPackageValidator de P18, sin duplicarlas)
GENERATION WORKSPACE          GenerationWorkspaceFactory (P15)
    ↓
PREPARACIÓN
    ├── árbol completo de la ISO   IsoTreeCopier (NUEVO — pieza que faltaba)
    ├── boot.wim                   (parte del árbol completo)
    ├── install.wim                (parte del árbol completo, se sustituye después)
    └── autounattend.xml           InstallationImageService (P16)
    ↓
MODIFICACIÓN BOOT.WIM
    ├── LabConfig                  InstallationImageService -> BootWimModifier -> LabConfigApplier (P16)
    └── Setup/OOBE                 InstallationImageService -> AutounattendGenerator (P16)
    ↓
MODIFICACIÓN INSTALL.WIM
    ├── selección Pro               WorkingImageFactory.CreateAsync (P07)
    └── RemovalPlan                 RemovalEngine.ExecuteAsync (P07) — plan ya construido, nunca decidido aquí
    ↓
INTEGRACIÓN POSTINSTALL
    ├── .NET                        PostInstallPackageBuilder (P18)
    └── PCPI                        PostInstallPackageBuilder (P18) -> copiado a sources\$OEM$\
    ↓
VALIDACIÓN                    GenerationWorkspaceValidator (P15)
    ↓
OSCDIMG                       OscdimgRunner (NUEVO)
    ↓
ISO FINAL
```

Cada paso reutiliza el motor ya existente **sin modificar su comportamiento**:
`InstallationImageService`/`BootWimModifier`/`LabConfigApplier`/
`AutounattendGenerator` (P16), `WorkingImageFactory`/`RemovalEngine` (P07),
`PostInstallPackageBuilder` (P18) se invocan tal cual, a través de sus
interfaces ya existentes. `MRS.ISOEngine` pasa a referenciar
`MRS.RemovalPlanning`, `MRS.RemovalEngine` y `MRS.PostInstall` (un único
sentido: ninguno de esos tres referencia `MRS.ISOEngine`) — es exactamente
el rol de "unir las piezas" que pide el prompt.

## La pieza que faltaba: copiar el árbol completo de la ISO

Las fases P15/P16/P18 solo copiaban archivos WIM concretos (`boot.wim`,
`install.wim`) o generaban archivos sueltos (`autounattend.xml`,
`SetupComplete.cmd`). Ninguna copiaba el **resto** del árbol de la ISO:
`bootmgr`, `boot\` (incluyendo `etfsboot.com`, el cargador de arranque
BIOS), `efi\microsoft\boot\` (incluyendo `efisys.bin`, el cargador UEFI),
`setup.exe`, etc. Sin esos archivos, `oscdimg` no podría producir una ISO
arrancable ni en BIOS ni en UEFI. `IsoTreeCopier` (nuevo, P19) monta la ISO
original en solo lectura (reutilizando `IIsoMounter`, igual que
`BootWimProvisioner`) y copia todo su contenido al workspace antes de que
las fases siguientes sustituyan `boot.wim`/`install.wim`/generen
`autounattend.xml`/`$OEM$`.

## Modelos y servicios nuevos

- `Models/IsoGenerationRequest.cs` — junta todo lo que el pipeline necesita:
  ISO origen, índice de edición Pro, `InstallationOptions` (P15),
  `AutounattendConfiguration` (P16), un `RemovalPlan` **ya construido**
  (puede ser `null` = solo seleccionar la edición Pro, sin eliminar nada —
  el pipeline nunca decide qué eliminar), `PostInstallConfiguration`/
  `PostInstallSourceFiles` (P18), y la ruta de la ISO final.
- `Models/IsoGenerationResult.cs`.
- `Pipeline/IsoGenerationRequestValidator.cs` — valida la solicitud
  reutilizando `InstallationExecutionValidator` (P15/P16, bloquea
  `BypassStorage`) y `PostInstallPackageValidator` (P18), más las
  comprobaciones propias (ISO origen existe, índice válido, ruta de salida
  distinta de la ISO origen).
- `Pipeline/IIsoGenerationPipeline.cs` / `IsoGenerationPipeline.cs` — el
  orquestador. Reporta progreso con `InstallationProgressInfo` (el mismo
  tipo de P16, reutilizado aquí sin problema porque ambos viven en el mismo
  proyecto `MRS.ISOEngine`), traduciendo internamente el progreso de
  `RemovalEngine` (`MRS.RemovalEngine.Models.ProgressInfo`) y de
  `PostInstallPackageBuilder` (`PostInstallProgressLevel`) a ese único tipo
  de cara al llamador.
- `TreeCopy/IIsoTreeCopier.cs` / `IsoTreeCopier.cs` — copia el árbol
  completo de la ISO (ver arriba).
- `TreeCopy/DirectoryCopyHelper.cs` — copia recursiva compartida (por
  `IsoTreeCopier` y por la fusión de `$OEM$` de PostInstall en el pipeline).
- `Oscdimg/IOscdimgRunner.cs` / `OscdimgRunner.cs` — genera la ISO final.
  `IsAvailable()` comprueba de verdad la existencia del ejecutable (no solo
  si se configuró una ruta): una ruta explícita que no existe se reporta
  como no disponible, igual que si no se hubiera encontrado ninguna.

## Único cambio en un proyecto ya existente (fuera de ISOEngine): `IPostInstallPackageBuilder`

`PostInstallPackageBuilder` (P18) no tenía una interfaz propia — fue el
único motor del proyecto sin ese patrón (`IRemovalEngine`,
`IWorkingImageFactory`, `IBootWimModifier`, `IInstallationImageService`
ya lo tenían todos). Se añadió `IPostInstallPackageBuilder` con la misma
firma exacta que el método `Build` ya existente, para que el pipeline de
P19 pueda depender de la abstracción en vez de la clase concreta — sin
cambiar ningún comportamiento de P18 (los 44 tests de P18 siguen pasando
sin ningún cambio).

## Decisiones arquitectónicas

1. **Ningún componente eliminado, ninguna regla nueva.** El pipeline recibe
   el `RemovalPlan` ya construido (por el flujo Catálogo -&gt; Protección
   -&gt; Dependencias -&gt; RemovalPlanBuilder de P05/P06, sin cambios) o,
   si no se proporciona ninguno, aplica un plan vacío — literalmente
   `new RemovalPlan()`, sin acciones — que `RemovalEngine` ya sabía manejar
   desde P07 (mount + commit sin cambios). "Selección Pro" y "RemovalPlan"
   son conceptualmente dos cosas separadas en el diagrama del prompt, y
   siguen siéndolo aquí: la primera siempre ocurre (exportar el índice
   elegido), la segunda es opcional.
2. **`install.wim` modificado vive primero en el workspace propio de
   `WorkingImageFactory`** (P07, bajo `%LOCALAPPDATA%\...\workspaces\<guid>\`),
   no directamente en el `GenerationWorkspace` de P19 (bajo
   `...\iso-workspaces\<guid>\`) — porque modificar `WorkingImageFactory`
   para que escriba en una ubicación distinta habría sido tocar su lógica
   sin necesidad real (prohibido explícitamente por el prompt). En vez de
   eso, el pipeline copia el resultado ya confirmado (`Commit` exitoso) al
   `GenerationWorkspace` una vez termina — mismo principio que P16 aplicó
   entre el workspace de `BootWimProvisioner` y el de `RemovalEngine`.
3. **`IsAvailable()` de `OscdimgRunner` revalida siempre con `File.Exists`**,
   nunca confía en que una ruta (explícita o autodetectada) siga siendo
   válida solo porque se resolvió una vez — se detectó y corrigió esto
   durante el propio desarrollo de P19 (un test lo expuso: una ruta
   explícita inexistente se reportaba como "disponible").
4. **Sin UI todavía.** El prompt no menciona ninguna sección de UI (a
   diferencia de P15/P16/P18); `MRS.WindowsBuilder` no referencia
   `MRS.ISOEngine` y no se ha tocado `MainWindow`. El pipeline queda listo,
   probado y disponible para cuando exista esa integración.

## Qué NO se ha hecho (según las restricciones explícitas del prompt)

- No se optimiza el tamaño de la ISO de ninguna forma.
- No se añade ninguna regla de eliminación nueva ni se modifica
  `ComponentCatalog`/`ProtectionEngine`.
- No se implementa el bypass de almacenamiento (sigue bloqueado por
  `InstallationExecutionValidator`, reutilizado sin cambios).
- No se modifica la lógica de `RemovalEngine`: solo se le invoca a través
  de su interfaz pública ya existente (`IRemovalEngine.ExecuteAsync`).

## Tests

**`IsoTreeCopierTests`** (3): copia cada archivo/subdirectorio preservando
la estructura, el montaje de la ISO se libera siempre, la ISO montada nunca
se modifica.

**`OscdimgRunnerTests`** (4): no disponible para una ruta explícita
inexistente, disponible para una ruta explícita existente, lanza una
excepción controlada si se intenta generar la ISO sin `oscdimg` disponible,
construye el comando de arranque dual BIOS/UEFI documentado por Microsoft
(`-bootdata:2#p0,e,b<etfsboot.com>#pEF,e,b<efisys.bin>`).

**`IsoGenerationRequestValidatorTests`** (7): solicitud bien formada válida,
ISO origen ausente, índice inválido, ruta de salida ausente, ruta de salida
igual a la de origen, bypass de almacenamiento bloqueado (reutilizando
P15/P16), PostInstall habilitado sin instaladores reales bloqueado
(reutilizando P18).

**`IsoGenerationPipelineTests`** (11): una ejecución completa exitosa llama
a cada etapa exactamente una vez y produce la ISO final; una solicitud
inválida aborta antes de crear ningún workspace; un fallo en boot.wim aborta
antes de tocar install.wim; un fallo en install.wim aborta antes de
PostInstall pero libera igualmente el montaje de la ISO; sin `RemovalPlan`
se aplica un plan vacío (verificado leyendo lo que recibió el
`IRemovalEngine` simulado); PostInstall deshabilitado nunca llama al
empaquetador; PostInstall habilitado fusiona el `$OEM$` generado dentro de
`sources\$OEM$\`; `oscdimg` no disponible aborta con un mensaje claro sin
intentar ejecutarlo; un fallo de `oscdimg` se reporta con su `ExitCode`; la
ISO final nunca se produce si cualquier etapa falla; la ISO de origen nunca
se modifica en una ejecución completa.

Se ejecutaron también todos los tests existentes (P07-P18): sin
regresiones.

## Resultado de `dotnet test`

```
MRS.ISOEngine.Tests            : 96/96  (+25 sobre P16/P18)
MRS.InstallationOptions.Tests  : 17/17
MRS.PostInstall.Tests          : 44/44
MRS.ProfileEngine.Tests        : 21/21
MRS.ImageEngine.Tests          : 78/78
MRS.ComponentCatalog.Tests     : 59/59
MRS.RemovalPlanning.Tests      : 48/48
MRS.RemovalEngine.Tests        : 48/48
```

Total: **411/411**. Sin regresiones.

## Resultado de `dotnet build`

`dotnet build MRS-Windows-Builder.sln`: **0 errores, 0 advertencias** en
toda la solución.

## Por qué no se pudo ejecutar de extremo a extremo contra una ISO real

Dos bloqueos independientes, ambos confirmados explícitamente en esta
sesión (no asumidos):

1. **DISM exige privilegios elevados** (igual que P17): `WorkingImageFactory`/
   `RemovalEngine`/`BootWimModifier` (dentro de `InstallationImageService`)
   necesitan `Mount-Wim`/`Export-Image`/`Unmount-Wim`, todos bloqueados sin
   elevación. Esta sesión sigue sin ejecutarse como administrador.
2. **`oscdimg.exe` no está instalado**: se comprobó explícitamente
   (`where oscdimg` y una búsqueda bajo `Windows Kits`) que el Windows ADK
   — Deployment Tools no está presente en este equipo. `OscdimgRunner.IsAvailable()`
   refleja esto correctamente (probado): el pipeline, ejecutado de verdad
   aquí, se detendría en el último paso con un mensaje claro, nunca
   fingiendo haber generado una ISO.

Ninguno de los dos bloqueos es un defecto del código: son requisitos del
entorno (privilegios + herramienta externa) que ya se documentaron como
pendientes en P17. El pipeline en sí — cada pieza, y su orquestación
completa — está validado con simulaciones realistas de DISM, registro y
`oscdimg`.

## Prueba recomendada (en una sesión elevada, con Windows ADK instalado)

1. Instalar el Windows ADK — Deployment Tools (incluye `oscdimg.exe`).
2. Ejecutar desde una consola elevada.
3. Construir un `IsoGenerationRequest` con una ISO real de Windows 11 26H2
   Pro, un `RemovalPlan` de ejemplo (o `null` para solo seleccionar la
   edición), y `PostInstallSourceFiles` apuntando a instaladores reales.
4. Llamar a `IsoGenerationPipeline.GenerateAsync` y confirmar: la ISO final
   existe y arranca (BIOS y UEFI); `Get-MountedWimInfo` no muestra ningún
   montaje de MRS al terminar; la ISO original no cambió; el `SetupComplete.cmd`
   generado en P18 se ejecuta tras la instalación.

## Commit recomendado

`git commit` con el resumen "P19: pipeline completo de generación de ISO
(IsoGenerationPipeline une análisis/RemovalPlan/RemovalEngine/
InstallationOptions/boot.wim/PostInstall/oscdimg) — ejecución real
pendiente por falta de sesión elevada y de Windows ADK" y `git push` a
`main`.
