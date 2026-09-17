using MRS.ImageEngine.Inventory;
using MRS.ImageEngine.Tests.Data;
using MRS.ImageEngine.Tests.Fakes;
using Xunit;

namespace MRS.ImageEngine.Tests;

/// <summary>
/// P22: progreso visible del inventariado. El porcentaje reportado representa
/// únicamente las FASES reales del proceso (workspace, montaje, cada
/// categoría DISM, desmontaje) — nunca un porcentaje interno de DISM, que no
/// existe de forma fiable para estas operaciones. Ningún test aquí depende de
/// WPF: <see cref="RecordingProgress"/> es un <see cref="IProgress{T}"/> de
/// prueba puro, mismo patrón que P10 (MRS.RemovalEngine.Tests).
/// </summary>
public sealed class ImageInventoryProgressTests : IDisposable
{
    private readonly string _workspaceRoot =
        Path.Combine(Path.GetTempPath(), "mrs-inventory-progress-tests", Guid.NewGuid().ToString("N"));

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
    public async Task Phases_are_reported_in_the_expected_order()
    {
        var runner = FullyPopulatedRunner();
        var progress = new RecordingProgress();

        await Service(runner).BuildInventoryAsync(@"X:\sources\install.wim", index: 1, default, progress);

        var stages = progress.Reports.Select(r => r.Stage).Distinct().ToArray();
        Assert.Equal(
            new[]
            {
                "Preparando inventariado",
                "Comprobando montajes",
                "Montando imagen",
                "Analizando paquetes",
                "Analizando características",
                "Analizando capacidades",
                "Analizando aplicaciones provisionadas",
                "Analizando controladores",
                "Construyendo catálogo",
                "Finalizando",
                "100 % completado",
            },
            stages);
    }

    [Fact]
    public async Task Percent_never_exceeds_100()
    {
        var runner = FullyPopulatedRunner();
        var progress = new RecordingProgress();

        await Service(runner).BuildInventoryAsync(@"X:\sources\install.wim", index: 1, default, progress);

        Assert.All(progress.Reports, r => Assert.True(r.Percent <= 100));
    }

    [Fact]
    public async Task Percent_is_never_negative()
    {
        var runner = FullyPopulatedRunner();
        var progress = new RecordingProgress();

        await Service(runner).BuildInventoryAsync(@"X:\sources\install.wim", index: 1, default, progress);

        Assert.All(progress.Reports, r => Assert.True(r.Percent >= 0));
    }

    [Fact]
    public async Task Percent_only_increases_across_reports()
    {
        var runner = FullyPopulatedRunner();
        var progress = new RecordingProgress();

        await Service(runner).BuildInventoryAsync(@"X:\sources\install.wim", index: 1, default, progress);

        for (var i = 1; i < progress.Reports.Count; i++)
            Assert.True(progress.Reports[i].Percent >= progress.Reports[i - 1].Percent,
                $"El progreso retrocedió de {progress.Reports[i - 1].Percent}% a {progress.Reports[i].Percent}% " +
                $"(fase '{progress.Reports[i - 1].Stage}' -> '{progress.Reports[i].Stage}').");
    }

    [Fact]
    public async Task The_final_report_on_success_is_100_percent()
    {
        var runner = FullyPopulatedRunner();
        var progress = new RecordingProgress();

        await Service(runner).BuildInventoryAsync(@"X:\sources\install.wim", index: 1, default, progress);

        var last = Assert.Single(progress.Reports, r => r.Stage == "100 % completado");
        Assert.Equal(100, last.Percent);
        Assert.Equal(InventoryProgressLevel.Success, last.Level);
        Assert.Equal(progress.Reports[^1], last); // es literalmente el último reporte emitido
    }

    [Fact]
    public async Task A_failed_category_never_falsely_reports_100_percent()
    {
        var runner = FullyPopulatedRunner();
        runner.Drivers = (string.Empty, 87);
        var progress = new RecordingProgress();

        await Assert.ThrowsAsync<ImageAnalysisException>(
            () => Service(runner).BuildInventoryAsync(@"X:\sources\install.wim", index: 1, default, progress));

        Assert.DoesNotContain(progress.Reports, r => r.Percent == 100);
        Assert.Contains(progress.Reports, r => r.Level == InventoryProgressLevel.Error);
    }

    [Fact]
    public async Task A_failed_mount_never_falsely_reports_100_percent()
    {
        var runner = FullyPopulatedRunner();
        runner.MountExitCode = 5;
        var progress = new RecordingProgress();

        await Assert.ThrowsAsync<ImageAnalysisException>(
            () => Service(runner).BuildInventoryAsync(@"X:\sources\install.wim", index: 1, default, progress));

        Assert.DoesNotContain(progress.Reports, r => r.Percent == 100);
        Assert.Contains(progress.Reports, r => r.Level == InventoryProgressLevel.Error);
    }

    [Fact]
    public async Task A_failed_category_reports_which_stage_failed()
    {
        var runner = FullyPopulatedRunner();
        runner.Capabilities = (string.Empty, 42);
        var progress = new RecordingProgress();

        await Assert.ThrowsAsync<ImageAnalysisException>(
            () => Service(runner).BuildInventoryAsync(@"X:\sources\install.wim", index: 1, default, progress));

        var failure = Assert.Single(progress.Reports, r => r.Level == InventoryProgressLevel.Error);
        Assert.Equal("Analizando capacidades", failure.Stage);
    }

    [Fact]
    public async Task The_progress_parameter_is_optional_and_does_not_change_existing_behavior()
    {
        var runner = FullyPopulatedRunner();

        // Sin progress (como hacían todos los llamadores antes de P22).
        var result = await Service(runner).BuildInventoryAsync(@"X:\sources\install.wim", index: 2);

        Assert.Equal(3, result.Inventory.PackageCount);
        Assert.False(result.WorkspaceKept);
    }

    [Fact]
    public async Task Progress_does_not_change_the_order_of_DISM_operations()
    {
        var runner = FullyPopulatedRunner();
        var progress = new RecordingProgress();

        await Service(runner).BuildInventoryAsync(@"X:\sources\install.wim", index: 2, default, progress);

        var relevant = runner.Calls
            .Select(c => c.StartsWith("mount:") ? "mount" : c.StartsWith("unmount:") ? "unmount" : c)
            .Where(c => c is "mount" or "packages" or "features" or "capabilities" or "apps" or "drivers" or "unmount")
            .ToArray();

        Assert.Equal(
            new[] { "mount", "packages", "features", "capabilities", "apps", "drivers", "unmount" },
            relevant);
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
