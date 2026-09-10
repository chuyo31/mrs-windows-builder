# Resultado - P2: Análisis REAL de la ISO (fase de solo lectura)

## Archivos creados / modificados

### MRS.DismEngine (biblioteca)
Creados:
- `Logging/LogLevel.cs` — niveles `Info` / `Warn` / `Error` / `Dism`.
- `Logging/LogEntry.cs` — línea de log; `ToString()` → `[NIVEL] mensaje`.
- `Logging/IAppLogger.cs` — sistema de logging reutilizable (evento `Entry`).
- `Logging/AppLogger.cs` — implementación + `NullAppLogger`.
- `Processes/ProcessRunResult.cs` — resultado de proceso (stdout, stderr, ExitCode, duración, timeout).
- `Processes/IProcessRunner.cs` / `Processes/ProcessRunner.cs` — ejecutor robusto de procesos externos: captura asíncrona de stdout/stderr, código de salida, duración y tiempo máximo; nunca lanza por ExitCode ≠ 0.
- `Dism/IDismRunner.cs` / `Dism/DismRunner.cs` — invocación controlada de `DISM.exe /English /Get-WimInfo` (con y sin `/Index`). Registra el comando como `[DISM]`; usa `ExitCode` como criterio de éxito (no el texto "ERROR").

Eliminado: `Class1.cs`.
`MRS.DismEngine.csproj` sin cambios de dependencias.

### MRS.ImageEngine (biblioteca)
Creados:
- `Models/ImageFormat.cs` — `Unknown` / `Wim` / `Esd`.
- `Models/ImageArchitecture.cs` — `Unknown` / `X86` / `X64` / `Arm64`.
- `Models/ImageEdition.cs` — índice, nombre, descripción, arquitectura, versión, build, idioma, edición.
- `Models/ImageInfo.cs` — SO, versión comercial, versión cruda, build, arquitectura, idioma, formato y lista de ediciones.
- `Parsing/ImageFormatDetector.cs` — WIM/ESD por extensión.
- `Parsing/WindowsVersionResolver.cs` — deriva "Windows 10/11" y la versión comercial ("26H2", "24H2"…) a partir del nº de build; devuelve `null` si no reconoce la build (no inventa valores).
- `Parsing/DismWimInfoParser.cs` — parser genérico `clave : valor` de la salida `/English`, independiente del orden y del idioma de Windows; maneja líneas de continuación (`Languages :`).
- `Iso/IIsoMounter.cs` / `Iso/IsoMounter.cs` — montaje temporal de la ISO como unidad **de solo lectura** vía `Mount-DiskImage` / `Dismount-DiskImage`; se desmonta siempre al liberar.
- `Iso/IsoInspectionResult.cs` — resultado de localizar `sources\install.wim|esd`.
- `ImageService.cs` — orquestador `MainWindow → ImageService → DismRunner → DISM.exe`.
- `ImageAnalysisException.cs` — conserva el `ExitCode` de DISM.

Eliminado: `Class1.cs`.
`MRS.ImageEngine.csproj` — añadida referencia a `MRS.DismEngine`.

### MRS.WindowsBuilder (app)
Modificados:
- `MainWindow.xaml.cs` — sin lógica DISM: construye `ImageService` y delega. "Seleccionar ISO" comprueba existencia + monta/localiza la imagen; "Analizar imagen" ejecuta el análisis; el `ComboBox` de ediciones habilita "Continuar" y registra la edición elegida. El log de la UI se alimenta del `IAppLogger` (`[INFO]/[WARN]/[ERROR]/[DISM]`).
- `MainWindow.xaml` — `SelectionChanged` en el `ComboBox` de edición.
- `MRS.WindowsBuilder.csproj` — referencias a `MRS.ImageEngine` y `MRS.DismEngine`.

### tests/MRS.ImageEngine.Tests (nuevo proyecto, xUnit)
- `MRS.ImageEngine.Tests.csproj` (añadido a la solución).
- `Data/DismOutputs.cs` — salidas de DISM `/English` reproducidas (ninguna prueba usa una ISO real).
- `Fakes/FakeDismRunner.cs` — `IDismRunner` controlado (salidas y códigos de salida predefinidos).
- `DismWimInfoParserTests.cs`, `ImageFormatDetectorTests.cs`, `WindowsVersionResolverTests.cs`, `ImageServiceTests.cs`.

## Funcionalidad implementada

- **Seleccionar ISO**: solo `.iso`; comprueba que el archivo existe; monta la ISO
  (solo lectura), localiza `sources\install.wim` **o** `sources\install.esd`,
  desmonta y, si no hay ninguno, muestra error claro. Habilita "Analizar imagen".
- **Analizar imagen**: monta la ISO, ejecuta `DISM /English /Get-WimInfo` (listado
  + un `/Index:N` por edición), parsea y desmonta. Obtiene: nombre del sistema,
  versión comercial, versión y build, arquitectura, idioma, tipo WIM/ESD, índices
  disponibles y nombre/descripción de cada edición. Ninguno de estos valores está
  quemado en el código: todos salen de DISM (la versión comercial se deriva del nº
  de build).
- **Interfaz**: la tarjeta "Información de la imagen" se rellena tras el análisis;
  el `ComboBox` "Edición" se puebla con las ediciones; al elegir una se habilita
  "Continuar" y se registra en el log.
- **Errores**: si DISM falla, la UI muestra "No se ha podido analizar la imagen."
  y el registro añade `[ERROR] DISM no pudo analizar la imagen.` +
  `[ERROR] Código: XXXXX` (el código de salida no se oculta).
- **Logging**: `IAppLogger` reutilizable con `[INFO] [WARN] [ERROR] [DISM]`; la UI
  muestra los mensajes importantes y el comando DISM ejecutado.
- **Diseño a futuro**: modelos y parser preparados para ISO/WIM/ESD, Windows 10 y
  11, x86/x64/ARM64 y múltiples ediciones; no hay código específico de "Windows 11
  Pro 26H2".

## Resultado de `dotnet build`

```
dotnet build MRS-Windows-Builder.sln
Compilación correcta.
    0 Advertencia(s)
    0 Errores
```

(La aplicación se ejecuta correctamente.)

## Resultado de `dotnet test`

```
dotnet test MRS-Windows-Builder.sln
Correctas! - Con error: 0, Superado: 50, Omitido: 0, Total: 50 - MRS.ImageEngine.Tests.dll (net8.0)
```

Cobertura de los tests:
- parseo de información de WIM (campos base, arquitectura, versión/build, idioma en
  línea normal y en línea de continuación, uso de *fallbacks*);
- múltiples índices (listado de 1 y de 2 ediciones, orden preservado);
- detección WIM/ESD por ruta (incluye mayúsculas y rutas sin extensión / nulas);
- errores de DISM y **códigos de salida** (fallo en el listado, fallo en el
  detalle, *timeout*, salida sin índices);
- arquitectura (`x64`, `amd64`, `x86`, `i386`, `ARM64`, vacío, desconocida);
- versión/build (`ExtractBuildNumber`, nombre de producto y versión comercial por
  build, `null` para builds no reconocidas).

## Problemas encontrados

- El primer `dotnet build` falló solo porque una instancia de la app abierta en
  la fase P1 mantenía bloqueado `MRS.WindowsBuilder.exe`. Se cerró el proceso y la
  compilación quedó limpia. No es un error de código.
- DISM no expone directamente el nombre comercial ("Windows 11") ni la versión
  comercial ("26H2"); se derivan del número de build mediante una tabla en
  `WindowsVersionResolver`. Si aparece una build no mapeada, el campo se muestra
  como `--` en lugar de inventarse.
- El montaje/desmontaje de ISO (`IsoMounter`) usa PowerShell y requiere Windows
  real; no se cubre con tests unitarios por decisión del propio prompt
  (no crear tests que necesiten una ISO real).

## Fuera de alcance (siguiente fase)

Sin implementar: eliminación de componentes, perfiles, montaje del WIM,
modificación de registro offline, compresión y creación de ISO.
