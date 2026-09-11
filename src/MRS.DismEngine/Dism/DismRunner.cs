using MRS.DismEngine.Logging;
using MRS.DismEngine.Processes;

namespace MRS.DismEngine.Dism;

/// <summary>
/// Implementación de <see cref="IDismRunner"/> sobre DISM.exe.
///
/// Cadena de responsabilidad:
///   ImageService / ImageInventoryService  ->  DismRunner  ->  IProcessRunner  ->  DISM.exe
///
/// Se fuerza <c>/English</c> para que la salida sea estable independientemente
/// del idioma de Windows. El comando ejecutado se registra en el log con el
/// nivel [DISM]; el usuario final no ve estos detalles técnicos.
/// </summary>
public sealed class DismRunner : IDismRunner
{
    private static readonly TimeSpan QueryTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan MountTimeout = TimeSpan.FromMinutes(20);
    private static readonly TimeSpan UnmountTimeout = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan ExportTimeout = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan ModifyTimeout = TimeSpan.FromMinutes(15);

    private readonly IProcessRunner _processRunner;
    private readonly IAppLogger _logger;
    private readonly string _dismPath;

    public DismRunner(IProcessRunner processRunner, IAppLogger? logger = null, string? dismPath = null)
    {
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
        _logger = logger ?? NullAppLogger.Instance;
        _dismPath = dismPath ?? ResolveDismPath();
    }

    public Task<ProcessRunResult> GetWimInfoAsync(string imagePath, CancellationToken cancellationToken = default)
        => ExecuteAsync($"/English /Get-WimInfo /WimFile:\"{imagePath}\"", QueryTimeout, cancellationToken);

    public Task<ProcessRunResult> GetWimInfoAsync(string imagePath, int index, CancellationToken cancellationToken = default)
        => ExecuteAsync($"/English /Get-WimInfo /WimFile:\"{imagePath}\" /Index:{index}", QueryTimeout, cancellationToken);

    public Task<ProcessRunResult> GetMountedWimInfoAsync(CancellationToken cancellationToken = default)
        => ExecuteAsync("/English /Get-MountedWimInfo", QueryTimeout, cancellationToken);

    public Task<ProcessRunResult> MountWimAsync(
        string wimFile, int index, string mountDir, bool readOnly = true,
        CancellationToken cancellationToken = default)
        => ExecuteAsync(
            $"/English /Mount-Wim /WimFile:\"{wimFile}\" /Index:{index} /MountDir:\"{mountDir}\"" +
            (readOnly ? " /ReadOnly" : string.Empty),
            MountTimeout, cancellationToken);

    public Task<ProcessRunResult> UnmountWimDiscardAsync(string mountDir, CancellationToken cancellationToken = default)
        => ExecuteAsync($"/English /Unmount-Wim /MountDir:\"{mountDir}\" /Discard", UnmountTimeout, cancellationToken);

    public Task<ProcessRunResult> UnmountWimCommitAsync(string mountDir, CancellationToken cancellationToken = default)
        => ExecuteAsync($"/English /Unmount-Wim /MountDir:\"{mountDir}\" /Commit", UnmountTimeout, cancellationToken);

    public Task<ProcessRunResult> ExportImageAsync(
        string sourceImageFile, int sourceIndex, string destinationImageFile, string? destinationName = null,
        CancellationToken cancellationToken = default)
        => ExecuteAsync(
            $"/English /Export-Image /SourceImageFile:\"{sourceImageFile}\" /SourceIndex:{sourceIndex} " +
            $"/DestinationImageFile:\"{destinationImageFile}\"" +
            (string.IsNullOrWhiteSpace(destinationName) ? string.Empty : $" /DestinationName:\"{destinationName}\""),
            ExportTimeout, cancellationToken);

    public Task<ProcessRunResult> GetPackagesAsync(string mountDir, CancellationToken cancellationToken = default)
        => ExecuteAsync($"/English /Image:\"{mountDir}\" /Get-Packages", QueryTimeout, cancellationToken);

    public Task<ProcessRunResult> GetFeaturesAsync(string mountDir, CancellationToken cancellationToken = default)
        => ExecuteAsync($"/English /Image:\"{mountDir}\" /Get-Features", QueryTimeout, cancellationToken);

    public Task<ProcessRunResult> GetCapabilitiesAsync(string mountDir, CancellationToken cancellationToken = default)
        => ExecuteAsync($"/English /Image:\"{mountDir}\" /Get-Capabilities", QueryTimeout, cancellationToken);

    public Task<ProcessRunResult> GetProvisionedAppxPackagesAsync(string mountDir, CancellationToken cancellationToken = default)
        => ExecuteAsync($"/English /Image:\"{mountDir}\" /Get-ProvisionedAppxPackages", QueryTimeout, cancellationToken);

    public Task<ProcessRunResult> GetDriversAsync(string mountDir, CancellationToken cancellationToken = default)
        => ExecuteAsync($"/English /Image:\"{mountDir}\" /Get-Drivers", QueryTimeout, cancellationToken);

    // --- Modificación de una imagen de trabajo montada en escritura (fase 7) ---

    public Task<ProcessRunResult> RemoveProvisionedAppxPackageAsync(string mountDir, string packageName, CancellationToken cancellationToken = default)
        => ExecuteAsync($"/English /Image:\"{mountDir}\" /Remove-ProvisionedAppxPackage /PackageName:\"{packageName}\"", ModifyTimeout, cancellationToken);

    public Task<ProcessRunResult> DisableFeatureAsync(string mountDir, string featureName, CancellationToken cancellationToken = default)
        => ExecuteAsync($"/English /Image:\"{mountDir}\" /Disable-Feature /FeatureName:\"{featureName}\"", ModifyTimeout, cancellationToken);

    public Task<ProcessRunResult> RemoveCapabilityAsync(string mountDir, string capabilityName, CancellationToken cancellationToken = default)
        => ExecuteAsync($"/English /Image:\"{mountDir}\" /Remove-Capability /CapabilityName:\"{capabilityName}\"", ModifyTimeout, cancellationToken);

    public Task<ProcessRunResult> RemovePackageAsync(string mountDir, string packageIdentity, CancellationToken cancellationToken = default)
        => ExecuteAsync($"/English /Image:\"{mountDir}\" /Remove-Package /PackageName:\"{packageIdentity}\"", ModifyTimeout, cancellationToken);

    private async Task<ProcessRunResult> ExecuteAsync(string arguments, TimeSpan timeout, CancellationToken cancellationToken)
    {
        _logger.Dism($"{_dismPath} {arguments}");

        var result = await _processRunner
            .RunAsync(_dismPath, arguments, cancellationToken, timeout)
            .ConfigureAwait(false);

        _logger.Dism($"ExitCode={result.ExitCode} Duracion={result.Duration.TotalSeconds:0.0}s");

        if (result.TimedOut)
            _logger.Warn("DISM no respondió dentro del tiempo máximo.");
        else if (result.ExitCode != 0)
            _logger.Warn($"DISM devolvió un código distinto de cero: {result.ExitCode}");

        return result;
    }

    private static string ResolveDismPath()
    {
        var system = Environment.GetFolderPath(Environment.SpecialFolder.System);
        var candidate = Path.Combine(system, "Dism.exe");
        return File.Exists(candidate) ? candidate : "Dism.exe";
    }
}
