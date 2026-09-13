using MRS.ComponentCatalog.Models;
using MRS.RemovalEngine.Models;
using MRS.RemovalEngine.Tests.Fakes;
using MRS.RemovalPlanning.Models;
using Xunit;

namespace MRS.RemovalEngine.Tests;

/// <summary>
/// P10: la telemetría de progreso (<see cref="ProgressInfo"/>) es una simple
/// clase de datos consumida vía <see cref="IProgress{T}"/>; ninguna de estas
/// pruebas necesita WPF ni ninguna UI concreta.
/// </summary>
public sealed class ProgressReportingTests : IDisposable
{
    private readonly string _workspace = Path.Combine(Path.GetTempPath(), "mrs-progress-tests", Guid.NewGuid().ToString("N"));
    private readonly string _workingWim;
    private readonly string _mountPath;

    public ProgressReportingTests()
    {
        Directory.CreateDirectory(_workspace);
        _workingWim = Path.Combine(_workspace, "install.wim");
        File.WriteAllText(_workingWim, "fake working wim");
        _mountPath = Path.Combine(_workspace, "mount");
        Directory.CreateDirectory(_mountPath);
    }

    private MRS.RemovalEngine.Models.WorkingImage NewImage() => new()
    {
        SourcePath = Path.Combine(_workspace, "source.wim"),
        WorkingWimPath = _workingWim,
        Index = 1,
        MountPath = _mountPath,
        WorkspacePath = _workspace,
    };

    [Fact]
    public void ProgressInfo_clamps_percent_to_0_100()
    {
        Assert.Equal(0, ProgressInfo.Create("x", -10, "msg").Percent);
        Assert.Equal(100, ProgressInfo.Create("x", 150, "msg").Percent);
        Assert.Equal(42, ProgressInfo.Create("x", 42, "msg").Percent);
    }

    [Fact]
    public async Task Successful_run_reports_stages_with_non_decreasing_percent_and_ends_at_100()
    {
        var dism = new FakeDismRunner();
        var engine = new global::MRS.RemovalEngine.RemovalEngine(dism);
        var progress = new RecordingProgress();

        var clipchamp = PlanFactory.Item("appx:Clipchamp", "Clipchamp", ComponentSourceType.Appx, RemovalActionType.RemoveAppx);
        var result = await engine.ExecuteAsync(NewImage(), PlanFactory.Plan(clipchamp), progress: progress);

        Assert.True(result.Success);
        Assert.NotEmpty(progress.Reports);

        var percents = progress.Reports.Select(r => r.Percent).ToList();
        for (var i = 1; i < percents.Count; i++)
            Assert.True(percents[i] >= percents[i - 1], $"El progreso retrocedió: {percents[i - 1]} -> {percents[i]}");

        Assert.Equal(100, percents[^1]);
        Assert.Contains(progress.Reports, r => r.Level == ProgressLevel.Success);
    }

    [Fact]
    public async Task Successful_run_reports_stages_in_the_expected_order()
    {
        var dism = new FakeDismRunner();
        var engine = new global::MRS.RemovalEngine.RemovalEngine(dism);
        var progress = new RecordingProgress();

        var clipchamp = PlanFactory.Item("appx:Clipchamp", "Clipchamp", ComponentSourceType.Appx, RemovalActionType.RemoveAppx);
        await engine.ExecuteAsync(NewImage(), PlanFactory.Plan(clipchamp), progress: progress);

        var distinctStagesInOrder = progress.Reports.Select(r => r.Stage).Distinct().ToList();

        Assert.Equal(
            new[] { "Montaje", "Aplicación de eliminaciones", "Validación", "Commit", "Finalización / desmontaje" },
            distinctStagesInOrder);
    }

    [Fact]
    public async Task A_dism_failure_emits_an_error_level_report()
    {
        var dism = new FakeDismRunner();
        dism.ActionExitCodes["Clipchamp"] = 123;
        var engine = new global::MRS.RemovalEngine.RemovalEngine(dism);
        var progress = new RecordingProgress();

        var clipchamp = PlanFactory.Item("appx:Clipchamp", "Clipchamp", ComponentSourceType.Appx, RemovalActionType.RemoveAppx);
        var result = await engine.ExecuteAsync(NewImage(), PlanFactory.Plan(clipchamp), progress: progress);

        Assert.False(result.Success);
        Assert.Contains(progress.Reports, r => r.Level == ProgressLevel.Error);
    }

    [Fact]
    public async Task Cancellation_reports_a_warning_and_reaches_100_percent()
    {
        using var cts = new CancellationTokenSource();
        var dism = new FakeDismRunner();
        dism.AfterAction["First"] = () => cts.Cancel();
        var engine = new global::MRS.RemovalEngine.RemovalEngine(dism);
        var progress = new RecordingProgress();

        var first = PlanFactory.Item("appx:First", "First", ComponentSourceType.Appx, RemovalActionType.RemoveAppx);
        var second = PlanFactory.Item("appx:Second", "Second", ComponentSourceType.Appx, RemovalActionType.RemoveAppx);

        var result = await engine.ExecuteAsync(NewImage(), PlanFactory.Plan(first, second), cts.Token, progress);

        Assert.Equal(RemovalExecutionPhase.Cancelled, result.Phase);
        Assert.Contains(progress.Reports, r => r.Level == ProgressLevel.Warning);
        Assert.Equal(100, progress.Reports[^1].Percent);
    }

    [Fact]
    public async Task Progress_is_optional_and_execution_behaves_identically_without_it()
    {
        var dism = new FakeDismRunner();
        var engine = new global::MRS.RemovalEngine.RemovalEngine(dism);
        var clipchamp = PlanFactory.Item("appx:Clipchamp", "Clipchamp", ComponentSourceType.Appx, RemovalActionType.RemoveAppx);

        var result = await engine.ExecuteAsync(NewImage(), PlanFactory.Plan(clipchamp)); // sin progress

        Assert.True(result.Success);
    }

    [Fact]
    public async Task WorkingImageFactory_reports_export_progress_up_to_25_percent()
    {
        var dism = new FakeDismRunner();
        var factory = new WorkingImageFactory(dism);
        var progress = new RecordingProgress();

        var source = Path.Combine(_workspace, "original.wim");
        File.WriteAllText(source, "fake");

        await factory.CreateAsync(source, 2, _workspace, progress: progress);

        Assert.NotEmpty(progress.Reports);
        Assert.All(progress.Reports, r => Assert.InRange(r.Percent, 0, 25));
        Assert.Equal(25, progress.Reports[^1].Percent);
        Assert.Contains(progress.Reports, r => r.Level == ProgressLevel.Success);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_workspace)) Directory.Delete(_workspace, recursive: true); }
        catch { /* best-effort */ }
    }
}
