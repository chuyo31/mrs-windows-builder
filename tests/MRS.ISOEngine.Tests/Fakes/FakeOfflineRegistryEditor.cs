using MRS.DismEngine.Processes;
using MRS.ISOEngine.Registry;

namespace MRS.ISOEngine.Tests.Fakes;

/// <summary>
/// <see cref="IOfflineRegistryEditor"/> controlado: simula un hive de registro con
/// un diccionario en memoria, sin ejecutar <c>reg.exe</c> real. Refleja el
/// comportamiento real de <c>reg query</c>/<c>reg delete</c> (ExitCode != 0 si el
/// valor no existe) para que los tests de <c>LabConfigApplier</c> sean realistas.
/// </summary>
internal sealed class FakeOfflineRegistryEditor : IOfflineRegistryEditor
{
    public int LoadExitCode { get; set; }
    public int UnloadExitCode { get; set; }
    public bool HiveLoaded { get; private set; }
    public List<string> Calls { get; } = new();

    private readonly Dictionary<string, int> _values = new(StringComparer.OrdinalIgnoreCase);

    public int? GetValue(string hiveKeyName, string subKeyPath, string valueName)
        => _values.TryGetValue(Key(hiveKeyName, subKeyPath, valueName), out var value) ? value : null;

    public Task<ProcessRunResult> LoadHiveAsync(string hiveKeyName, string hiveFilePath, CancellationToken cancellationToken = default)
    {
        Calls.Add($"load:{hiveKeyName}");
        if (LoadExitCode == 0)
            HiveLoaded = true;
        return Result(LoadExitCode);
    }

    public Task<ProcessRunResult> SetDwordAsync(string hiveKeyName, string subKeyPath, string valueName, int value, CancellationToken cancellationToken = default)
    {
        Calls.Add($"set:{hiveKeyName}\\{subKeyPath}\\{valueName}={value}");
        _values[Key(hiveKeyName, subKeyPath, valueName)] = value;
        return Result(0);
    }

    public Task<ProcessRunResult> QueryValueAsync(string hiveKeyName, string subKeyPath, string valueName, CancellationToken cancellationToken = default)
    {
        Calls.Add($"query:{hiveKeyName}\\{subKeyPath}\\{valueName}");
        var exists = _values.ContainsKey(Key(hiveKeyName, subKeyPath, valueName));
        return Result(exists ? 0 : 1);
    }

    public Task<ProcessRunResult> DeleteValueAsync(string hiveKeyName, string subKeyPath, string valueName, CancellationToken cancellationToken = default)
    {
        Calls.Add($"delete:{hiveKeyName}\\{subKeyPath}\\{valueName}");
        var existed = _values.Remove(Key(hiveKeyName, subKeyPath, valueName));
        return Result(existed ? 0 : 1); // reg.exe devuelve error si el valor no existía; no se trata como fallo real.
    }

    public Task<ProcessRunResult> UnloadHiveAsync(string hiveKeyName, CancellationToken cancellationToken = default)
    {
        Calls.Add($"unload:{hiveKeyName}");
        if (UnloadExitCode == 0)
            HiveLoaded = false;
        return Result(UnloadExitCode);
    }

    private static string Key(string hiveKeyName, string subKeyPath, string valueName) => $"{hiveKeyName}\\{subKeyPath}\\{valueName}";

    private static Task<ProcessRunResult> Result(int exitCode)
        => Task.FromResult(new ProcessRunResult("reg.exe", string.Empty, exitCode, string.Empty, string.Empty, TimeSpan.FromMilliseconds(5), false));
}
