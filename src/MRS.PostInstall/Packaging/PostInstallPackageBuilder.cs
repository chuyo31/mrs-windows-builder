using MRS.DismEngine.Logging;
using MRS.PostInstall.Exceptions;
using MRS.PostInstall.Models;
using MRS.PostInstall.Scripts;

namespace MRS.PostInstall.Packaging;

/// <summary>
/// Construye un paquete PostInstall listo para que una fase posterior de
/// ISOEngine lo copie a <c>&lt;workspace&gt;\sources\$OEM$\</c> (P18, sección 12/14).
/// No genera ninguna ISO. Entrada: <see cref="PostInstallConfiguration"/> +
/// <see cref="PostInstallSourceFiles"/> (rutas reales de los instaladores).
/// Salida: un directorio con la estructura
///
///   &lt;output&gt;/$OEM$/$$/Setup/Scripts/
///       SetupComplete.cmd
///       dotnet/&lt;instalador de .NET&gt;
///       pcpi/&lt;instalador de PCPI&gt;
///
/// Aborta (lanza <see cref="PostInstallException"/>) si la validación previa
/// falla — nunca produce un paquete aparentemente completo pero incompleto.
/// </summary>
public sealed class PostInstallPackageBuilder : IPostInstallPackageBuilder
{
    private readonly IAppLogger _logger;

    public PostInstallPackageBuilder(IAppLogger? logger = null)
        => _logger = logger ?? NullAppLogger.Instance;

    public PostInstallPackageResult Build(
        PostInstallConfiguration config, PostInstallSourceFiles sourceFiles, string outputDirectory,
        IProgress<PostInstallProgressInfo>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(sourceFiles);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        const string validatingStage = "Validando paquetes";
        const string dotnetStage = "Preparando .NET";
        const string pcpiStage = "Preparando PCPI";
        const string configStage = "Generando configuración";
        const string postValidateStage = "Validando PostInstall";
        const string finalizingStage = "Finalizando";

        progress?.Report(PostInstallProgressInfo.Create(validatingStage, 2, "Validando configuración y archivos..."));

        if (!config.Enabled)
        {
            _logger.Info("[POSTINSTALL] PostInstall deshabilitado: no se genera ningún paquete.");
            progress?.Report(PostInstallProgressInfo.Create(finalizingStage, 100, "PostInstall deshabilitado", PostInstallProgressLevel.Info));
            return new PostInstallPackageResult(true, null, Array.Empty<string>());
        }

        var validation = PostInstallPackageValidator.Validate(config, sourceFiles);
        if (!validation.IsValid)
        {
            var message = string.Join(" ", validation.Errors);
            _logger.Error($"[POSTINSTALL] Validation failed: {message}");
            progress?.Report(PostInstallProgressInfo.Create(validatingStage, 2, message, PostInstallProgressLevel.Error));
            return new PostInstallPackageResult(false, null, validation.Errors);
        }

        _logger.Info("[POSTINSTALL] Validation: OK");
        progress?.Report(PostInstallProgressInfo.Create(validatingStage, 10, "Validation: OK", PostInstallProgressLevel.Success));

        _logger.Info("[POSTINSTALL] Preparing package");
        _logger.Info($"[POSTINSTALL] Runtime: .NET Desktop Runtime {config.DotNetRuntimeVersion} {config.Architecture}");
        _logger.Info($"[POSTINSTALL] PCPI: {config.PcpiFileName}");

        if (Directory.Exists(outputDirectory) && Directory.EnumerateFileSystemEntries(outputDirectory).Any())
            throw new PostInstallException($"El directorio de salida '{outputDirectory}' ya existe y no está vacío.");

        var scriptsDir = Path.Combine(outputDirectory, "$OEM$", "$$", "Setup", "Scripts");
        var dotnetDir = Path.Combine(scriptsDir, "dotnet");
        var pcpiDir = Path.Combine(scriptsDir, "pcpi");
        Directory.CreateDirectory(dotnetDir);
        Directory.CreateDirectory(pcpiDir);

        progress?.Report(PostInstallProgressInfo.Create(dotnetStage, 15, "Copiando instalador de .NET Desktop Runtime..."));
        var dotnetDestination = Path.Combine(dotnetDir, config.DotNetInstallerFileName);
        File.Copy(sourceFiles.DotNetInstallerPath, dotnetDestination, overwrite: true);
        progress?.Report(PostInstallProgressInfo.Create(dotnetStage, 25, "Instalador de .NET copiado", PostInstallProgressLevel.Success));

        progress?.Report(PostInstallProgressInfo.Create(pcpiStage, 35, "Copiando PCPI..."));
        var pcpiDestination = Path.Combine(pcpiDir, config.PcpiFileName);
        File.Copy(sourceFiles.PcpiInstallerPath, pcpiDestination, overwrite: true);
        progress?.Report(PostInstallProgressInfo.Create(pcpiStage, 50, "PCPI copiado", PostInstallProgressLevel.Success));

        progress?.Report(PostInstallProgressInfo.Create(configStage, 55, "Generando SetupComplete.cmd..."));
        var script = SetupCompleteScriptGenerator.Generate(config);
        var scriptPath = Path.Combine(scriptsDir, "SetupComplete.cmd");
        File.WriteAllText(scriptPath, script);
        progress?.Report(PostInstallProgressInfo.Create(configStage, 70, "SetupComplete.cmd generado", PostInstallProgressLevel.Success));

        progress?.Report(PostInstallProgressInfo.Create(postValidateStage, 75, "Verificando el paquete generado..."));
        var packageErrors = VerifyGeneratedPackage(scriptsDir, dotnetDestination, pcpiDestination, scriptPath);
        if (packageErrors.Count > 0)
        {
            var message = string.Join(" ", packageErrors);
            _logger.Error($"[POSTINSTALL] Package verification failed: {message}");
            progress?.Report(PostInstallProgressInfo.Create(postValidateStage, 75, message, PostInstallProgressLevel.Error));
            return new PostInstallPackageResult(false, null, packageErrors);
        }

        progress?.Report(PostInstallProgressInfo.Create(postValidateStage, 90, "Paquete verificado", PostInstallProgressLevel.Success));

        _logger.Info("[POSTINSTALL] Package prepared successfully");
        progress?.Report(PostInstallProgressInfo.Create(finalizingStage, 100, "Paquete PostInstall preparado", PostInstallProgressLevel.Success));

        var oemRoot = Path.Combine(outputDirectory, "$OEM$");
        return new PostInstallPackageResult(true, oemRoot, Array.Empty<string>());
    }

    private static List<string> VerifyGeneratedPackage(string scriptsDir, string dotnetDestination, string pcpiDestination, string scriptPath)
    {
        var errors = new List<string>();

        if (!File.Exists(dotnetDestination))
            errors.Add("El instalador de .NET no se copió correctamente al paquete.");

        if (!File.Exists(pcpiDestination))
            errors.Add("PCPI no se copió correctamente al paquete.");

        if (!File.Exists(scriptPath))
            errors.Add("SetupComplete.cmd no se generó correctamente.");

        return errors;
    }
}
