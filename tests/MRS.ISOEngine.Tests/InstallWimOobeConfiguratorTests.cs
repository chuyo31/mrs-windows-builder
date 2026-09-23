using MRS.ISOEngine.Exceptions;
using MRS.ISOEngine.InstallWim;
using MRS.ISOEngine.Tests.Fakes;
using Xunit;

namespace MRS.ISOEngine.Tests;

/// <summary>
/// P29: ciclo Mount-Wim(RW) → cargar hive SYSTEM offline → aplicar BypassNRO →
/// verificar → descargar hive → Commit/Discard sobre la imagen de trabajo de
/// install.wim -- mismo patrón transaccional que <c>BootWimModifierTests</c>
/// (P16/P25/P26), aplicado a install.wim en vez de boot.wim.
/// </summary>
public sealed class InstallWimOobeConfiguratorTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "mrs-installwimoobe-tests", Guid.NewGuid().ToString("N"));
    private readonly string _installWimPath;
    private readonly string _mountDir;

    public InstallWimOobeConfiguratorTests()
    {
        Directory.CreateDirectory(_dir);
        _installWimPath = Path.Combine(_dir, "install.wim");
        File.WriteAllBytes(_installWimPath, new byte[8192]);
        _mountDir = Path.Combine(_dir, "mount");
    }

    private InstallWimOobeConfigurator NewConfigurator(FakeDismRunner dism, FakeOfflineRegistryEditor registry)
        => new(dism, registry);

    [Fact]
    public async Task A_successful_run_applies_BypassNRO_commits_and_unloads_the_hive()
    {
        var dism = new FakeDismRunner();
        var registry = new FakeOfflineRegistryEditor();
        var configurator = NewConfigurator(dism, registry);

        var result = await configurator.ApplyOfflineOobeBypassAsync(_installWimPath, 1, _mountDir);

        Assert.True(result.Success);
        Assert.Contains(result.AppliedLogLines, l => l.Contains("BypassNRO"));
        Assert.Contains(dism.Calls, c => c.StartsWith("commit:"));
        Assert.DoesNotContain(dism.Calls, c => c.StartsWith("discard:"));
        Assert.False(registry.HiveLoaded);
    }

    [Fact]
    public async Task BypassNRO_is_actually_set_to_1_in_the_registry()
    {
        var dism = new FakeDismRunner();
        var registry = new FakeOfflineRegistryEditor();
        var configurator = NewConfigurator(dism, registry);

        await configurator.ApplyOfflineOobeBypassAsync(_installWimPath, 1, _mountDir);

        Assert.Equal(1, registry.GetValue("any", "Setup\\OOBE", "BypassNRO"));
    }

    [Fact]
    public async Task Applying_twice_is_idempotent()
    {
        var dism = new FakeDismRunner();
        var registry = new FakeOfflineRegistryEditor();
        var configurator = NewConfigurator(dism, registry);

        var first = await configurator.ApplyOfflineOobeBypassAsync(_installWimPath, 1, _mountDir);
        var second = await configurator.ApplyOfflineOobeBypassAsync(_installWimPath, 1, _mountDir);

        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.Equal(1, registry.GetValue("any", "Setup\\OOBE", "BypassNRO"));
    }

    [Fact]
    public async Task A_mount_failure_throws_and_never_touches_the_registry()
    {
        var dism = new FakeDismRunner { MountExitCode = 1 };
        var registry = new FakeOfflineRegistryEditor();
        var configurator = NewConfigurator(dism, registry);

        await Assert.ThrowsAsync<IsoEngineException>(
            () => configurator.ApplyOfflineOobeBypassAsync(_installWimPath, 1, _mountDir));

        Assert.Empty(registry.Calls);
    }

    [Fact]
    public async Task A_hive_load_failure_discards_and_never_marks_the_hive_as_loaded()
    {
        var dism = new FakeDismRunner();
        var registry = new FakeOfflineRegistryEditor { LoadExitCode = 1 };
        var configurator = NewConfigurator(dism, registry);

        var result = await configurator.ApplyOfflineOobeBypassAsync(_installWimPath, 1, _mountDir);

        Assert.False(result.Success);
        Assert.Contains(dism.Calls, c => c.StartsWith("discard:"));
        Assert.DoesNotContain(dism.Calls, c => c.StartsWith("commit:"));
        Assert.False(registry.HiveLoaded);
    }

    [Fact]
    public async Task An_unload_failure_marks_the_result_as_unsuccessful_and_discards()
    {
        var dism = new FakeDismRunner();
        var registry = new FakeOfflineRegistryEditor { UnloadExitCode = 1 };
        var configurator = NewConfigurator(dism, registry);

        var result = await configurator.ApplyOfflineOobeBypassAsync(_installWimPath, 1, _mountDir);

        Assert.False(result.Success);
        Assert.Contains(dism.Calls, c => c.StartsWith("discard:"));
    }

    [Fact]
    public async Task Missing_install_wim_throws_before_mounting_anything()
    {
        var dism = new FakeDismRunner();
        var registry = new FakeOfflineRegistryEditor();
        var configurator = NewConfigurator(dism, registry);
        var missingPath = Path.Combine(_dir, "no-existe-install.wim");

        await Assert.ThrowsAsync<IsoEngineException>(
            () => configurator.ApplyOfflineOobeBypassAsync(missingPath, 1, _mountDir));

        Assert.Empty(dism.Calls);
    }

    [Fact]
    public async Task A_suspiciously_small_install_wim_throws_before_mounting_anything()
    {
        var dism = new FakeDismRunner();
        var registry = new FakeOfflineRegistryEditor();
        var configurator = NewConfigurator(dism, registry);
        var tinyPath = Path.Combine(_dir, "tiny-install.wim");
        File.WriteAllBytes(tinyPath, new byte[10]);

        await Assert.ThrowsAsync<IsoEngineException>(
            () => configurator.ApplyOfflineOobeBypassAsync(tinyPath, 1, _mountDir));

        Assert.Empty(dism.Calls);
    }

    [Fact]
    public async Task DISM_reporting_an_invalid_WIM_throws_before_mounting_anything()
    {
        var dism = new FakeDismRunner { ListExitCode = 87 };
        var registry = new FakeOfflineRegistryEditor();
        var configurator = NewConfigurator(dism, registry);

        await Assert.ThrowsAsync<IsoEngineException>(
            () => configurator.ApplyOfflineOobeBypassAsync(_installWimPath, 1, _mountDir));

        Assert.Contains(dism.Calls, c => c.StartsWith("list:"));
        Assert.DoesNotContain(dism.Calls, c => c.StartsWith("mount:"));
    }

    [Fact]
    public async Task The_value_persists_after_unmount_when_re_read_in_a_new_session()
    {
        // P29, sección 7: "la validación debe leer físicamente el WIM
        // resultante, no confiar solamente en el estado interno del servicio" --
        // se simula releyendo con un hiveKeyName (alias) distinto, igual que
        // haría una validación final independiente tras el commit.
        var dism = new FakeDismRunner();
        var registry = new FakeOfflineRegistryEditor();
        var configurator = NewConfigurator(dism, registry);

        await configurator.ApplyOfflineOobeBypassAsync(_installWimPath, 1, _mountDir);

        Assert.Equal(1, registry.GetValue("un-alias-completamente-distinto", "Setup\\OOBE", "BypassNRO"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); }
        catch { /* best-effort */ }
    }
}
