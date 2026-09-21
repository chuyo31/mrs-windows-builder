using MRS.ISOEngine.BootWim;
using MRS.ISOEngine.Exceptions;
using MRS.ISOEngine.Tests.Fakes;
using Xunit;
using InstallationOptionsModel = MRS.InstallationOptions.Models.InstallationOptions;

namespace MRS.ISOEngine.Tests;

/// <summary>
/// P16, sección 2/4/13: ciclo Mount-Wim → cargar hive → aplicar → verificar →
/// descargar hive → Commit/Discard. Nunca deja un hive cargado ni decide el
/// resultado por el texto de la salida.
/// </summary>
public sealed class BootWimModifierTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "mrs-bootwimmodifier-tests", Guid.NewGuid().ToString("N"));
    private readonly string _bootWimPath;
    private readonly string _mountDir;

    public BootWimModifierTests()
    {
        Directory.CreateDirectory(_dir);
        _bootWimPath = Path.Combine(_dir, "boot.wim");
        // P25: ValidateBeforeMountAsync exige un tamaño mínimo de cordura antes
        // de intentar Mount-Wim; el fixture debe superarlo para que estos tests
        // seleccionen el mismo camino que antes (mount -> hive -> commit/discard).
        File.WriteAllBytes(_bootWimPath, new byte[8192]);
        _mountDir = Path.Combine(_dir, "mount");
    }

    [Fact]
    public async Task A_successful_run_commits_and_unloads_the_hive()
    {
        var dism = new FakeDismRunner();
        var registry = new FakeOfflineRegistryEditor();
        var modifier = new BootWimModifier(dism, registry);

        var result = await modifier.ApplyLabConfigAsync(_bootWimPath, 1, InstallationOptionsModel.Default, _mountDir);

        Assert.True(result.Success);
        Assert.Contains(dism.Calls, c => c.StartsWith("commit:"));
        Assert.DoesNotContain(dism.Calls, c => c.StartsWith("discard:"));
        Assert.False(registry.HiveLoaded);
    }

    [Fact]
    public async Task A_mount_failure_throws_and_never_touches_the_registry()
    {
        var dism = new FakeDismRunner { MountExitCode = 1 };
        var registry = new FakeOfflineRegistryEditor();
        var modifier = new BootWimModifier(dism, registry);

        await Assert.ThrowsAsync<IsoEngineException>(
            () => modifier.ApplyLabConfigAsync(_bootWimPath, 1, InstallationOptionsModel.Default, _mountDir));

        Assert.Empty(registry.Calls);
    }

    [Fact]
    public async Task A_hive_load_failure_discards_and_never_marks_the_hive_as_loaded()
    {
        var dism = new FakeDismRunner();
        var registry = new FakeOfflineRegistryEditor { LoadExitCode = 1 };
        var modifier = new BootWimModifier(dism, registry);

        var result = await modifier.ApplyLabConfigAsync(_bootWimPath, 1, InstallationOptionsModel.Default, _mountDir);

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
        var modifier = new BootWimModifier(dism, registry);

        var result = await modifier.ApplyLabConfigAsync(_bootWimPath, 1, InstallationOptionsModel.Default, _mountDir);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("descargar el hive"));
        Assert.Contains(dism.Calls, c => c.StartsWith("discard:"));
    }

    [Fact]
    public async Task Missing_boot_wim_throws_before_mounting_anything()
    {
        var dism = new FakeDismRunner();
        var registry = new FakeOfflineRegistryEditor();
        var modifier = new BootWimModifier(dism, registry);
        var missingPath = Path.Combine(_dir, "no-existe-boot.wim");

        await Assert.ThrowsAsync<IsoEngineException>(
            () => modifier.ApplyLabConfigAsync(missingPath, 1, InstallationOptionsModel.Default, _mountDir));

        Assert.Empty(dism.Calls);
    }

    [Fact]
    public async Task A_suspiciously_small_boot_wim_throws_before_mounting_anything()
    {
        var dism = new FakeDismRunner();
        var registry = new FakeOfflineRegistryEditor();
        var modifier = new BootWimModifier(dism, registry);
        var tinyPath = Path.Combine(_dir, "tiny-boot.wim");
        File.WriteAllBytes(tinyPath, new byte[10]);

        await Assert.ThrowsAsync<IsoEngineException>(
            () => modifier.ApplyLabConfigAsync(tinyPath, 1, InstallationOptionsModel.Default, _mountDir));

        Assert.Empty(dism.Calls);
    }

    [Fact]
    public async Task A_boot_wim_still_marked_ReadOnly_throws_before_mounting_anything()
    {
        // No debería ocurrir nunca en la práctica (BootWimProvisioner ya lo
        // arregla al copiar), pero si ocurriera, debe fallar aquí con un mensaje
        // claro en vez de dejar que DISM lo intente ("access denied").
        var dism = new FakeDismRunner();
        var registry = new FakeOfflineRegistryEditor();
        var modifier = new BootWimModifier(dism, registry);
        var readOnlyPath = Path.Combine(_dir, "readonly-boot.wim");
        File.WriteAllBytes(readOnlyPath, new byte[8192]);
        File.SetAttributes(readOnlyPath, File.GetAttributes(readOnlyPath) | FileAttributes.ReadOnly);

        try
        {
            await Assert.ThrowsAsync<IsoEngineException>(
                () => modifier.ApplyLabConfigAsync(readOnlyPath, 1, InstallationOptionsModel.Default, _mountDir));

            Assert.Empty(dism.Calls);
        }
        finally
        {
            File.SetAttributes(readOnlyPath, FileAttributes.Normal); // para que Dispose() pueda borrar _dir
        }
    }

    [Fact]
    public async Task DISM_reporting_an_invalid_WIM_throws_before_mounting_anything()
    {
        var dism = new FakeDismRunner { ListExitCode = 87 };
        var registry = new FakeOfflineRegistryEditor();
        var modifier = new BootWimModifier(dism, registry);

        await Assert.ThrowsAsync<IsoEngineException>(
            () => modifier.ApplyLabConfigAsync(_bootWimPath, 1, InstallationOptionsModel.Default, _mountDir));

        Assert.Contains(dism.Calls, c => c.StartsWith("list:"));
        Assert.DoesNotContain(dism.Calls, c => c.StartsWith("mount:"));
    }

    [Fact]
    public async Task Only_the_enabled_options_are_reported_as_applied_log_lines()
    {
        var dism = new FakeDismRunner();
        var registry = new FakeOfflineRegistryEditor();
        var modifier = new BootWimModifier(dism, registry);
        var options = new InstallationOptionsModel { BypassTpm = true, BypassSecureBoot = false, BypassCpu = true, BypassRam = false };

        var result = await modifier.ApplyLabConfigAsync(_bootWimPath, 1, options, _mountDir);

        Assert.Equal(new[] { "[COMPAT] BypassTPM applied", "[COMPAT] BypassCPU applied" }, result.AppliedLogLines);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); }
        catch { /* best-effort */ }
    }
}
