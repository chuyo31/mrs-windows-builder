using MRS.DismEngine.Dism;
using MRS.DismEngine.Processes;

namespace MRS.RemovalEngine.Tests.Fakes;

/// <summary>
/// <see cref="IDismRunner"/> controlado para probar <see cref="MRS.RemovalEngine.RemovalEngine"/>
/// y <see cref="MRS.RemovalEngine.WorkingImageFactory"/> sin ejecutar DISM ni usar una ISO real.
/// </summary>
internal sealed class FakeDismRunner : IDismRunner
{
    public int ListExitCode { get; set; }
    public int IndexCheckExitCode { get; set; }
    public int ExportExitCode { get; set; }
    public int MountExitCode { get; set; }
    public int UnmountDiscardExitCode { get; set; }
    public int UnmountCommitExitCode { get; set; }
    public string MountedWimInfoOutput { get; set; } = string.Empty;
    public int MountedWimInfoExitCode { get; set; }

    public Dictionary<string, int> ActionExitCodes { get; } = new(StringComparer.Ordinal);
    public int DefaultActionExitCode { get; set; }

    /// <summary>Se invoca justo después de que una acción concreta (por Target) termine con éxito; útil para simular cancelaciones a mitad de ejecución.</summary>
    public Dictionary<string, Action> AfterAction { get; } = new(StringComparer.Ordinal);

    public List<string> Calls { get; } = new();

    public Task<ProcessRunResult> GetWimInfoAsync(string imagePath, CancellationToken cancellationToken = default)
    {
        Calls.Add("list");
        return Result(ListExitCode);
    }

    public Task<ProcessRunResult> GetWimInfoAsync(string imagePath, int index, CancellationToken cancellationToken = default)
    {
        Calls.Add($"indexcheck:{index}");
        return Result(IndexCheckExitCode);
    }

    public Task<ProcessRunResult> ExportImageAsync(string sourceImageFile, int sourceIndex, string destinationImageFile, string? destinationName = null, CancellationToken cancellationToken = default)
    {
        Calls.Add($"export:{sourceIndex}");
        return Result(ExportExitCode);
    }

    public Task<ProcessRunResult> GetMountedWimInfoAsync(CancellationToken cancellationToken = default)
    {
        Calls.Add("mountedinfo");
        return Result(MountedWimInfoExitCode, MountedWimInfoOutput);
    }

    public Task<ProcessRunResult> MountWimAsync(string wimFile, int index, string mountDir, bool readOnly = true, CancellationToken cancellationToken = default)
    {
        Calls.Add($"mount:{mountDir}:{index}:ro={readOnly}");
        return Result(MountExitCode);
    }

    public Task<ProcessRunResult> UnmountWimDiscardAsync(string mountDir, CancellationToken cancellationToken = default)
    {
        Calls.Add($"discard:{mountDir}");
        return Result(UnmountDiscardExitCode);
    }

    public Task<ProcessRunResult> UnmountWimCommitAsync(string mountDir, CancellationToken cancellationToken = default)
    {
        Calls.Add($"commit:{mountDir}");
        return Result(UnmountCommitExitCode);
    }

    public Task<ProcessRunResult> GetPackagesAsync(string mountDir, CancellationToken cancellationToken = default)
    {
        Calls.Add("packages");
        return Result(0);
    }

    public Task<ProcessRunResult> GetFeaturesAsync(string mountDir, CancellationToken cancellationToken = default)
    {
        Calls.Add("features");
        return Result(0);
    }

    public Task<ProcessRunResult> GetCapabilitiesAsync(string mountDir, CancellationToken cancellationToken = default)
    {
        Calls.Add("capabilities");
        return Result(0);
    }

    public Task<ProcessRunResult> GetProvisionedAppxPackagesAsync(string mountDir, CancellationToken cancellationToken = default)
    {
        Calls.Add("apps");
        return Result(0);
    }

    public Task<ProcessRunResult> GetDriversAsync(string mountDir, CancellationToken cancellationToken = default)
    {
        Calls.Add("drivers");
        return Result(0);
    }

    public Task<ProcessRunResult> RemoveProvisionedAppxPackageAsync(string mountDir, string packageName, CancellationToken cancellationToken = default)
        => ExecuteActionAsync("remove-appx", packageName);

    public Task<ProcessRunResult> DisableFeatureAsync(string mountDir, string featureName, CancellationToken cancellationToken = default)
        => ExecuteActionAsync("disable-feature", featureName);

    public Task<ProcessRunResult> RemoveCapabilityAsync(string mountDir, string capabilityName, CancellationToken cancellationToken = default)
        => ExecuteActionAsync("remove-capability", capabilityName);

    public Task<ProcessRunResult> RemovePackageAsync(string mountDir, string packageIdentity, CancellationToken cancellationToken = default)
        => ExecuteActionAsync("remove-package", packageIdentity);

    private Task<ProcessRunResult> ExecuteActionAsync(string verb, string target)
    {
        Calls.Add($"{verb}:{target}");
        var exitCode = ActionExitCodes.TryGetValue(target, out var code) ? code : DefaultActionExitCode;

        if (exitCode == 0 && AfterAction.TryGetValue(target, out var callback))
            callback();

        return Result(exitCode);
    }

    private static Task<ProcessRunResult> Result(int exitCode, string output = "")
        => Task.FromResult(new ProcessRunResult("Dism.exe", string.Empty, exitCode, output, string.Empty, TimeSpan.FromMilliseconds(5), false));
}
