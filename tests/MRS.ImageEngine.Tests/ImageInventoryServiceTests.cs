using MRS.ImageEngine;
using MRS.ImageEngine.Inventory;
using MRS.ImageEngine.Tests.Data;
using MRS.ImageEngine.Tests.Fakes;
using Xunit;

namespace MRS.ImageEngine.Tests;

public sealed class ImageInventoryServiceTests : IDisposable
{
    private readonly string _workspaceRoot =
        Path.Combine(Path.GetTempPath(), "mrs-inventory-tests", Guid.NewGuid().ToString("N"));

    private FakeDismRunner FullyPopulatedRunner() => new()
    {
        Packages = (DismOutputs.Packages, 0),
        Features = (DismOutputs.Features, 0),
        Capabilities = (DismOutputs.Capabilities, 0),
        ProvisionedApps = (DismOutputs.ProvisionedApps, 0),
        Drivers = (DismOutputs.Drivers, 0),
        MountedImageInfoOutput = DismOutputs.MountedImageInfoEmpty,
    };

    private ImageInventoryService Service(FakeDismRunner runner)
        => new(runner, isoMounter: null, logger: null, workspaceRoot: _workspaceRoot);

    [Fact]
    public async Task BuildInventoryAsync_returns_a_full_inventory()
    {
        var runner = FullyPopulatedRunner();

        var result = await Service(runner).BuildInventoryAsync(@"X:\sources\install.wim", index: 2);

        Assert.Equal(3, result.Inventory.PackageCount);
        Assert.Equal(3, result.Inventory.FeatureCount);
        Assert.Equal(3, result.Inventory.CapabilityCount);
        Assert.Equal(2, result.Inventory.ProvisionedAppCount);
        Assert.Equal(2, result.Inventory.DriverCount);
        Assert.False(result.WorkspaceKept);
    }

    [Fact]
    public async Task BuildInventoryAsync_mounts_then_unmounts_and_cleans_workspace()
    {
        var runner = FullyPopulatedRunner();

        await Service(runner).BuildInventoryAsync(@"X:\sources\install.wim", index: 1);

        Assert.Contains(runner.Calls, c => c.StartsWith("mount:"));
        Assert.Contains(runner.Calls, c => c.StartsWith("unmount:"));
        Assert.True(runner.Calls.IndexOf(runner.Calls.First(c => c.StartsWith("mount:")))
                    < runner.Calls.IndexOf(runner.Calls.First(c => c.StartsWith("unmount:"))));

        // El workspace se elimina tras un inventario correcto.
        Assert.False(Directory.Exists(_workspaceRoot) && Directory.EnumerateDirectories(_workspaceRoot).Any());
    }

    [Fact]
    public async Task BuildInventoryAsync_uses_read_only_mount()
    {
        var runner = FullyPopulatedRunner();

        await Service(runner).BuildInventoryAsync(@"X:\sources\install.wim", index: 1);

        Assert.Contains(runner.Calls, c => c.StartsWith("mount:") && c.EndsWith("ro=True"));
    }

    [Fact]
    public async Task BuildInventoryAsync_propagates_dism_exit_code_when_mount_fails()
    {
        var runner = FullyPopulatedRunner();
        runner.MountExitCode = 1392;

        var ex = await Assert.ThrowsAsync<ImageAnalysisException>(
            () => Service(runner).BuildInventoryAsync(@"X:\sources\install.wim", index: 1));

        Assert.Equal(1392, ex.ExitCode);
        Assert.DoesNotContain(runner.Calls, c => c == "packages");
    }

    [Fact]
    public async Task BuildInventoryAsync_keeps_workspace_and_unmounts_when_a_category_fails()
    {
        var runner = FullyPopulatedRunner();
        runner.Drivers = (string.Empty, 87);

        var ex = await Assert.ThrowsAsync<ImageAnalysisException>(
            () => Service(runner).BuildInventoryAsync(@"X:\sources\install.wim", index: 1));

        Assert.Equal(87, ex.ExitCode);
        Assert.Contains(runner.Calls, c => c.StartsWith("unmount:")); // se desmonta pese al fallo
        Assert.True(Directory.EnumerateDirectories(_workspaceRoot).Any()); // workspace de diagnóstico conservado
    }

    [Fact]
    public async Task BuildInventoryAsync_recovers_only_orphan_mounts_under_our_workspace_root()
    {
        var ourOrphan = Path.Combine(_workspaceRoot, "deadbeef", "mount");
        var foreignMount = @"C:\OtroPrograma\mount";
        var runner = FullyPopulatedRunner();
        runner.MountedImageInfoOutput =
            DismOutputs.MountedImageInfoWith(ourOrphan) + "\n" +
            DismOutputs.MountedImageInfoWith(foreignMount);

        await Service(runner).BuildInventoryAsync(@"X:\sources\install.wim", index: 1);

        Assert.Contains(runner.Calls, c => c == $"unmount:{ourOrphan}");
        Assert.DoesNotContain(runner.Calls, c => c == $"unmount:{foreignMount}");
    }

    [Fact]
    public async Task BuildInventoryFromIsoAsync_requires_an_iso_mounter()
    {
        var runner = FullyPopulatedRunner();
        var service = new ImageInventoryService(runner, isoMounter: null, logger: null, workspaceRoot: _workspaceRoot);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.BuildInventoryFromIsoAsync("whatever.iso", 1));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_workspaceRoot))
                Directory.Delete(_workspaceRoot, recursive: true);
        }
        catch
        {
            // limpieza best-effort
        }
    }
}
