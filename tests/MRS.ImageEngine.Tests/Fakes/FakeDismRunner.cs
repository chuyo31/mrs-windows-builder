using MRS.DismEngine.Dism;
using MRS.DismEngine.Processes;

namespace MRS.ImageEngine.Tests.Fakes;

/// <summary>
/// <see cref="IDismRunner"/> controlado: devuelve salidas y códigos de salida
/// predefinidos sin ejecutar DISM.
/// </summary>
internal sealed class FakeDismRunner : IDismRunner
{
    // --- /Get-WimInfo -----------------------------------------------------------
    public string ListOutput { get; set; } = string.Empty;
    public int ListExitCode { get; set; }
    public bool ListTimedOut { get; set; }
    public Dictionary<int, (string Output, int ExitCode)> Details { get; } = new();
    public int DefaultDetailExitCode { get; set; }
    public bool DetailTimedOut { get; set; }

    // --- Montaje --------------------------------------------------------------
    public string MountedImageInfoOutput { get; set; } = string.Empty;
    public int MountedImageInfoExitCode { get; set; }
    public int MountExitCode { get; set; }
    public bool MountTimedOut { get; set; }
    public int UnmountExitCode { get; set; }

    // --- Inventario --------------------------------------------------------------
    public (string Output, int ExitCode) Packages { get; set; } = (string.Empty, 0);
    public (string Output, int ExitCode) Features { get; set; } = (string.Empty, 0);
    public (string Output, int ExitCode) Capabilities { get; set; } = (string.Empty, 0);
    public (string Output, int ExitCode) ProvisionedApps { get; set; } = (string.Empty, 0);
    public (string Output, int ExitCode) Drivers { get; set; } = (string.Empty, 0);

    public List<string> Calls { get; } = new();

    public Task<ProcessRunResult> GetWimInfoAsync(string imagePath, CancellationToken cancellationToken = default)
    {
        Calls.Add($"list:{imagePath}");
        return Result("/Get-WimInfo", ListExitCode, ListOutput, ListTimedOut);
    }

    public Task<ProcessRunResult> GetWimInfoAsync(string imagePath, int index, CancellationToken cancellationToken = default)
    {
        Calls.Add($"detail:{index}");
        var (output, exitCode) = Details.TryGetValue(index, out var entry)
            ? entry
            : (string.Empty, DefaultDetailExitCode);
        return Result($"/Get-WimInfo /Index:{index}", exitCode, output, DetailTimedOut);
    }

    public Task<ProcessRunResult> GetMountedWimInfoAsync(CancellationToken cancellationToken = default)
    {
        Calls.Add("mountedinfo");
        return Result("/Get-MountedWimInfo", MountedImageInfoExitCode, MountedImageInfoOutput, false);
    }

    public Task<ProcessRunResult> MountWimAsync(string wimFile, int index, string mountDir, bool readOnly = true, CancellationToken cancellationToken = default)
    {
        Calls.Add($"mount:{mountDir}:{index}:ro={readOnly}");
        return Result("/Mount-Wim", MountExitCode, string.Empty, MountTimedOut);
    }

    public Task<ProcessRunResult> UnmountWimDiscardAsync(string mountDir, CancellationToken cancellationToken = default)
    {
        Calls.Add($"unmount:{mountDir}");
        return Result("/Unmount-Wim /Discard", UnmountExitCode, string.Empty, false);
    }

    public Task<ProcessRunResult> GetPackagesAsync(string mountDir, CancellationToken cancellationToken = default)
    {
        Calls.Add("packages");
        return Result("/Get-Packages", Packages.ExitCode, Packages.Output, false);
    }

    public Task<ProcessRunResult> GetFeaturesAsync(string mountDir, CancellationToken cancellationToken = default)
    {
        Calls.Add("features");
        return Result("/Get-Features", Features.ExitCode, Features.Output, false);
    }

    public Task<ProcessRunResult> GetCapabilitiesAsync(string mountDir, CancellationToken cancellationToken = default)
    {
        Calls.Add("capabilities");
        return Result("/Get-Capabilities", Capabilities.ExitCode, Capabilities.Output, false);
    }

    public Task<ProcessRunResult> GetProvisionedAppxPackagesAsync(string mountDir, CancellationToken cancellationToken = default)
    {
        Calls.Add("apps");
        return Result("/Get-ProvisionedAppxPackages", ProvisionedApps.ExitCode, ProvisionedApps.Output, false);
    }

    public Task<ProcessRunResult> GetDriversAsync(string mountDir, CancellationToken cancellationToken = default)
    {
        Calls.Add("drivers");
        return Result("/Get-Drivers", Drivers.ExitCode, Drivers.Output, false);
    }

    private static Task<ProcessRunResult> Result(string arguments, int exitCode, string output, bool timedOut)
        => Task.FromResult(new ProcessRunResult(
            "Dism.exe", arguments, exitCode, output, string.Empty, TimeSpan.FromMilliseconds(5), timedOut));
}
