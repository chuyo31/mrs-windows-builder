using System.Text.RegularExpressions;
using MRS.DismEngine.Dism;
using MRS.DismEngine.Logging;
using MRS.DismEngine.Processes;
using MRS.ImageEngine.Iso;
using MRS.ImageEngine.Models;
using MRS.ImageEngine.Parsing;

namespace MRS.ImageEngine.Inventory;

/// <summary>
/// Coordina el inventario de SOLO LECTURA de una edición de la imagen:
///
///   MainWindow  ->  ImageInventoryService  ->  DismRunner  ->  DISM.exe
///
/// Patrón: montar -> inventariar -> desmontar SIEMPRE. Si algo falla, se
/// conservan los logs y el workspace de diagnóstico y se intenta desmontar.
/// No ejecuta ningún comando de limpieza (/StartComponentCleanup, /ResetBase,
/// /Cleanup-Mountpoints).
/// </summary>
public sealed partial class ImageInventoryService
{
    private static readonly string[] InstallImageNames = { "install.wim", "install.esd" };

    private readonly IDismRunner _dism;
    private readonly IIsoMounter? _isoMounter;
    private readonly IAppLogger _logger;
    private readonly string? _workspaceRoot;

    private readonly DismPackageParser _packageParser = new();
    private readonly DismFeatureParser _featureParser = new();
    private readonly DismCapabilityParser _capabilityParser = new();
    private readonly DismProvisionedAppParser _appParser = new();
    private readonly DismDriverParser _driverParser = new();

    public ImageInventoryService(
        IDismRunner dismRunner,
        IIsoMounter? isoMounter = null,
        IAppLogger? logger = null,
        string? workspaceRoot = null)
    {
        _dism = dismRunner ?? throw new ArgumentNullException(nameof(dismRunner));
        _isoMounter = isoMounter;
        _logger = logger ?? NullAppLogger.Instance;
        _workspaceRoot = workspaceRoot;
    }

    /// <summary>
    /// Monta la ISO, localiza <c>sources\install.wim</c> y construye el inventario
    /// del índice indicado. Desmonta la ISO al terminar.
    /// </summary>
    public async Task<InventoryResult> BuildInventoryFromIsoAsync(string isoPath, int index, CancellationToken cancellationToken = default)
    {
        var mounter = _isoMounter
            ?? throw new InvalidOperationException("ImageInventoryService se creó sin IIsoMounter.");

        if (string.IsNullOrWhiteSpace(isoPath) || !File.Exists(isoPath))
            throw new FileNotFoundException("La ISO indicada no existe.", isoPath);

        await using var isoMount = await mounter.MountAsync(isoPath, cancellationToken).ConfigureAwait(false);

        var imagePath = LocateInstallImage(isoMount.RootPath)
            ?? throw new ImageAnalysisException(-1, @"La ISO no contiene sources\install.wim ni sources\install.esd.");

        if (ImageFormatDetector.FromPath(imagePath) == ImageFormat.Esd)
            throw new ImageAnalysisException(-1,
                "La imagen es ESD; DISM /Mount-Image requiere WIM (conversión en una fase posterior).");

        return await BuildInventoryAsync(imagePath, index, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Construye el inventario del índice indicado de un WIM accesible en disco.
    /// No requiere montaje de ISO (usado también por los tests).
    /// </summary>
    public async Task<InventoryResult> BuildInventoryAsync(string wimPath, int index, CancellationToken cancellationToken = default)
    {
        _logger.Info("Creando workspace...");
        var workspace = InventoryWorkspace.CreateNew(_workspaceRoot);
        var mountDir = workspace.MountPath;
        var mounted = false;
        var keepWorkspace = false;

        try
        {
            await RecoverOrphanMountsAsync(cancellationToken).ConfigureAwait(false);

            _logger.Info($"Montando imagen índice {index}...");
            var mount = await _dism.MountWimAsync(wimPath, index, mountDir, readOnly: true, cancellationToken).ConfigureAwait(false);
            EnsureDismSucceeded(mount, "Montaje de la imagen");
            mounted = true;

            var packages = await RunCategoryAsync(
                "paquetes", "Inventariando paquetes...",
                () => _dism.GetPackagesAsync(mountDir, cancellationToken), _packageParser.Parse).ConfigureAwait(false);

            var features = await RunCategoryAsync(
                "características", "Inventariando características...",
                () => _dism.GetFeaturesAsync(mountDir, cancellationToken), _featureParser.Parse).ConfigureAwait(false);

            var capabilities = await RunCategoryAsync(
                "capacidades", "Inventariando capacidades...",
                () => _dism.GetCapabilitiesAsync(mountDir, cancellationToken), _capabilityParser.Parse).ConfigureAwait(false);

            var apps = await RunCategoryAsync(
                "aplicaciones", "Inventariando aplicaciones...",
                () => _dism.GetProvisionedAppxPackagesAsync(mountDir, cancellationToken), _appParser.Parse).ConfigureAwait(false);

            var drivers = await RunCategoryAsync(
                "drivers", "Inventariando drivers...",
                () => _dism.GetDriversAsync(mountDir, cancellationToken), _driverParser.Parse).ConfigureAwait(false);

            var inventory = new ImageInventory
            {
                Packages = packages,
                Features = features,
                Capabilities = capabilities,
                ProvisionedApps = apps,
                Drivers = drivers,
            };

            _logger.Info("Inventario completado.");
            _logger.Info("Imagen inventariada correctamente.");
            return new InventoryResult(inventory, workspace.RootPath, WorkspaceKept: false);
        }
        catch
        {
            keepWorkspace = true;
            throw;
        }
        finally
        {
            if (mounted)
                await SafeUnmountAsync(mountDir, cancellationToken).ConfigureAwait(false);

            if (keepWorkspace)
            {
                _logger.Info($"Workspace conservado: {workspace.RootPath}");
            }
            else
            {
                try { workspace.Delete(); }
                catch (Exception ex) { _logger.Warn($"No se pudo eliminar el workspace: {ex.Message}"); }
            }
        }
    }

    private async Task<IReadOnlyList<T>> RunCategoryAsync<T>(
        string categoryForError,
        string startMessage,
        Func<Task<ProcessRunResult>> run,
        Func<string, IReadOnlyList<T>> parse)
    {
        _logger.Info(startMessage);

        var result = await run().ConfigureAwait(false);
        if (result.TimedOut || result.ExitCode != 0)
        {
            _logger.Error($"Inventario de {categoryForError} fallido.");
            _logger.Error($"Comando: {result.CommandLine}");
            _logger.Error($"ExitCode: {result.ExitCode}");
            LogDismDiagnostics(result);
            throw new ImageAnalysisException(result.ExitCode, $"Inventario de {categoryForError} fallido.");
        }

        return parse(result.StandardOutput);
    }

    private async Task SafeUnmountAsync(string mountDir, CancellationToken cancellationToken)
    {
        try
        {
            _logger.Info("Intentando desmontar imagen...");
            var unmount = await _dism.UnmountWimDiscardAsync(mountDir, cancellationToken).ConfigureAwait(false);

            if (!unmount.Succeeded)
            {
                _logger.Error($"Fallo al desmontar la imagen. ExitCode: {unmount.ExitCode}");
                LogDismDiagnostics(unmount);
                return;
            }

            var mountedInfo = await _dism.GetMountedWimInfoAsync(cancellationToken).ConfigureAwait(false);
            if (mountedInfo.Succeeded && MountDirPresent(mountedInfo.StandardOutput, mountDir))
                _logger.Warn("La imagen sigue apareciendo como montada; revisar manualmente.");
            else
                _logger.Info("Imagen desmontada correctamente. No quedan montajes de MRS.");
        }
        catch (Exception ex)
        {
            _logger.Error($"Error al desmontar la imagen: {ex.Message}");
        }
    }

    /// <summary>Vuelca en el log un extracto de stderr/stdout de DISM (sin ruido).</summary>
    private void LogDismDiagnostics(ProcessRunResult result)
    {
        var detail = !string.IsNullOrWhiteSpace(result.StandardError)
            ? result.StandardError
            : result.StandardOutput;

        detail = detail?.Trim() ?? string.Empty;
        if (detail.Length == 0)
            return;

        if (detail.Length > 600)
            detail = detail[..600] + " […]";

        foreach (var line in detail.Split('\n'))
        {
            var clean = line.TrimEnd('\r').Trim();
            if (clean.Length > 0)
                _logger.Error($"DISM: {clean}");
        }
    }

    /// <summary>
    /// Recupera de forma controlada montajes huérfanos que pertenezcan a NUESTRA
    /// carpeta de workspaces. Nunca desmonta imágenes de otros programas.
    /// </summary>
    private async Task RecoverOrphanMountsAsync(CancellationToken cancellationToken)
    {
        ProcessRunResult info;
        try
        {
            info = await _dism.GetMountedWimInfoAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.Warn($"No se pudo consultar el estado de los montajes: {ex.Message}");
            return;
        }

        if (!info.Succeeded)
            return;

        var ourRoot = NormalizePath(InventoryWorkspace.WorkspacesRoot(_workspaceRoot));

        foreach (var mountDir in ExtractMountDirs(info.StandardOutput))
        {
            if (!IsUnder(mountDir, ourRoot))
                continue;

            _logger.Warn($"Montaje huérfano relacionado con MRS detectado: {mountDir}. Intentando recuperar...");
            try
            {
                var recovery = await _dism.UnmountWimDiscardAsync(mountDir, cancellationToken).ConfigureAwait(false);
                _logger.Info(recovery.Succeeded
                    ? $"Montaje huérfano recuperado: {mountDir}"
                    : $"No se pudo recuperar el montaje huérfano {mountDir} (código {recovery.ExitCode}).");
            }
            catch (Exception ex)
            {
                _logger.Warn($"Error al recuperar el montaje huérfano {mountDir}: {ex.Message}");
            }
        }
    }

    private void EnsureDismSucceeded(ProcessRunResult result, string operation)
    {
        if (result.TimedOut)
        {
            _logger.Error($"{operation}: DISM no respondió dentro del tiempo máximo.");
            throw new ImageAnalysisException(-1, $"{operation} fallido (timeout).");
        }

        if (result.ExitCode != 0)
        {
            _logger.Error($"{operation} fallido.");
            _logger.Error($"Comando: {result.CommandLine}");
            _logger.Error($"ExitCode: {result.ExitCode}");
            LogDismDiagnostics(result);
            throw new ImageAnalysisException(result.ExitCode, $"{operation} fallido.");
        }
    }

    private static string? LocateInstallImage(string mountRoot)
    {
        foreach (var name in InstallImageNames)
        {
            var candidate = Path.Combine(mountRoot, "sources", name);
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    internal static IEnumerable<string> ExtractMountDirs(string output)
        => output.Split('\n')
            .Select(line => MountDirRegex().Match(line.TrimEnd('\r')))
            .Where(match => match.Success)
            .Select(match => match.Groups["dir"].Value.Trim())
            .Where(dir => dir.Length > 0);

    internal static bool MountDirPresent(string output, string mountDir)
        => ExtractMountDirs(output).Any(dir => PathsEqual(dir, mountDir));

    private static bool IsUnder(string path, string normalizedRoot)
    {
        var full = NormalizePath(path);
        return full.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || string.Equals(full, normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static bool PathsEqual(string a, string b)
        => string.Equals(NormalizePath(a), NormalizePath(b), StringComparison.OrdinalIgnoreCase);

    private static string NormalizePath(string path)
    {
        try
        {
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        }
        catch
        {
            return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
    }

    [GeneratedRegex(@"^\s*Mount Dir\s*:\s*(?<dir>.+?)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex MountDirRegex();
}
