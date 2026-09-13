using MRS.DismEngine.Dism;
using MRS.DismEngine.Processes;

namespace MRS.ISOEngine.Tests.Fakes;

/// <summary><see cref="IDismRunner"/> controlado para probar BootWimModifier sin ejecutar DISM real.</summary>
internal sealed class FakeDismRunner : IDismRunner
{
    public int MountExitCode { get; set; }
    public int UnmountDiscardExitCode { get; set; }
    public int UnmountCommitExitCode { get; set; }
    public List<string> Calls { get; } = new();

    public Task<ProcessRunResult> GetWimInfoAsync(string imagePath, CancellationToken cancellationToken = default) => Result(0);
    public Task<ProcessRunResult> GetWimInfoAsync(string imagePath, int index, CancellationToken cancellationToken = default) => Result(0);
    public Task<ProcessRunResult> ExportImageAsync(string sourceImageFile, int sourceIndex, string destinationImageFile, string? destinationName = null, CancellationToken cancellationToken = default) => Result(0);
    public Task<ProcessRunResult> GetMountedWimInfoAsync(CancellationToken cancellationToken = default) => Result(0);

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

    public Task<ProcessRunResult> GetPackagesAsync(string mountDir, CancellationToken cancellationToken = default) => Result(0);
    public Task<ProcessRunResult> GetFeaturesAsync(string mountDir, CancellationToken cancellationToken = default) => Result(0);
    public Task<ProcessRunResult> GetCapabilitiesAsync(string mountDir, CancellationToken cancellationToken = default) => Result(0);
    public Task<ProcessRunResult> GetProvisionedAppxPackagesAsync(string mountDir, CancellationToken cancellationToken = default) => Result(0);
    public Task<ProcessRunResult> GetDriversAsync(string mountDir, CancellationToken cancellationToken = default) => Result(0);
    public Task<ProcessRunResult> RemoveProvisionedAppxPackageAsync(string mountDir, string packageName, CancellationToken cancellationToken = default) => Result(0);
    public Task<ProcessRunResult> DisableFeatureAsync(string mountDir, string featureName, CancellationToken cancellationToken = default) => Result(0);
    public Task<ProcessRunResult> RemoveCapabilityAsync(string mountDir, string capabilityName, CancellationToken cancellationToken = default) => Result(0);
    public Task<ProcessRunResult> RemovePackageAsync(string mountDir, string packageIdentity, CancellationToken cancellationToken = default) => Result(0);

    private static Task<ProcessRunResult> Result(int exitCode)
        => Task.FromResult(new ProcessRunResult("Dism.exe", string.Empty, exitCode, string.Empty, string.Empty, TimeSpan.FromMilliseconds(5), false));
}
