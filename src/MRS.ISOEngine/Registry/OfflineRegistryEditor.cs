using MRS.DismEngine.Processes;

namespace MRS.ISOEngine.Registry;

/// <summary>
/// Implementación de <see cref="IOfflineRegistryEditor"/> sobre <c>reg.exe</c> (P16).
///
/// Cadena de responsabilidad, igual que <c>DismRunner</c> en MRS.DismEngine:
///   LabConfigApplier / BootWimModifier  ->  OfflineRegistryEditor  ->  IProcessRunner  ->  reg.exe
///
/// No decide éxito/fallo por el texto de la salida: siempre por <c>ExitCode</c>.
/// </summary>
public sealed class OfflineRegistryEditor : IOfflineRegistryEditor
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(2);

    private readonly IProcessRunner _processRunner;
    private readonly string _regPath;

    public OfflineRegistryEditor(IProcessRunner processRunner, string? regPath = null)
    {
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
        _regPath = regPath ?? ResolveRegPath();
    }

    public Task<ProcessRunResult> LoadHiveAsync(string hiveKeyName, string hiveFilePath, CancellationToken cancellationToken = default)
        => ExecuteAsync($"load HKLM\\{hiveKeyName} \"{hiveFilePath}\"", cancellationToken);

    public Task<ProcessRunResult> SetDwordAsync(string hiveKeyName, string subKeyPath, string valueName, int value, CancellationToken cancellationToken = default)
        => ExecuteAsync($"add \"HKLM\\{hiveKeyName}\\{subKeyPath}\" /v {valueName} /t REG_DWORD /d {value} /f", cancellationToken);

    public Task<ProcessRunResult> QueryValueAsync(string hiveKeyName, string subKeyPath, string valueName, CancellationToken cancellationToken = default)
        => ExecuteAsync($"query \"HKLM\\{hiveKeyName}\\{subKeyPath}\" /v {valueName}", cancellationToken);

    public Task<ProcessRunResult> DeleteValueAsync(string hiveKeyName, string subKeyPath, string valueName, CancellationToken cancellationToken = default)
        => ExecuteAsync($"delete \"HKLM\\{hiveKeyName}\\{subKeyPath}\" /v {valueName} /f", cancellationToken);

    public Task<ProcessRunResult> UnloadHiveAsync(string hiveKeyName, CancellationToken cancellationToken = default)
        => ExecuteAsync($"unload HKLM\\{hiveKeyName}", cancellationToken);

    private Task<ProcessRunResult> ExecuteAsync(string arguments, CancellationToken cancellationToken)
        => _processRunner.RunAsync(_regPath, arguments, cancellationToken, DefaultTimeout);

    private static string ResolveRegPath()
    {
        var system = Environment.GetFolderPath(Environment.SpecialFolder.System);
        var candidate = Path.Combine(system, "reg.exe");
        return File.Exists(candidate) ? candidate : "reg.exe";
    }
}
