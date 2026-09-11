using MRS.ComponentCatalog.Models;
using MRS.RemovalEngine.Models;
using MRS.RemovalEngine.Tests.Fakes;
using MRS.RemovalPlanning.Models;
using Xunit;

namespace MRS.RemovalEngine.Tests;

public sealed class RemovalEngineTests : IDisposable
{
    private readonly string _workspace = Path.Combine(Path.GetTempPath(), "mrs-removalengine-tests", Guid.NewGuid().ToString("N"));
    private readonly string _workingWim;
    private readonly string _mountPath;

    public RemovalEngineTests()
    {
        Directory.CreateDirectory(_workspace);
        _workingWim = Path.Combine(_workspace, "install.wim");
        File.WriteAllText(_workingWim, "fake working wim");
        _mountPath = Path.Combine(_workspace, "mount");
        Directory.CreateDirectory(_mountPath);
    }

    private WorkingImage NewImage() => new()
    {
        SourcePath = Path.Combine(_workspace, "source.wim"),
        WorkingWimPath = _workingWim,
        Index = 1,
        MountPath = _mountPath,
        WorkspacePath = _workspace,
    };

    // ================= PREFLIGHT =================

    [Fact]
    public async Task Invalid_plan_never_mounts()
    {
        var dism = new FakeDismRunner();
        var engine = new global::MRS.RemovalEngine.RemovalEngine(dism);

        var result = await engine.ExecuteAsync(NewImage(), PlanFactory.InvalidPlan());

        Assert.False(result.Success);
        Assert.Equal(RemovalExecutionPhase.PreFlight, result.Phase);
        Assert.DoesNotContain(dism.Calls, c => c.StartsWith("mount:"));
    }

    [Fact]
    public async Task Missing_working_wim_fails_preflight()
    {
        var dism = new FakeDismRunner();
        var engine = new global::MRS.RemovalEngine.RemovalEngine(dism);
        var image = NewImage();
        File.Delete(image.WorkingWimPath);

        var clipchamp = PlanFactory.Item("appx:Clipchamp", "Clipchamp", ComponentSourceType.Appx, RemovalActionType.RemoveAppx);
        var result = await engine.ExecuteAsync(image, PlanFactory.Plan(clipchamp));

        Assert.Equal(RemovalExecutionPhase.PreFlight, result.Phase);
        Assert.DoesNotContain(dism.Calls, c => c.StartsWith("mount:"));
    }

    [Fact]
    public async Task Invalid_index_fails_preflight()
    {
        var dism = new FakeDismRunner { IndexCheckExitCode = 87 };
        var engine = new global::MRS.RemovalEngine.RemovalEngine(dism);

        var clipchamp = PlanFactory.Item("appx:Clipchamp", "Clipchamp", ComponentSourceType.Appx, RemovalActionType.RemoveAppx);
        var result = await engine.ExecuteAsync(NewImage(), PlanFactory.Plan(clipchamp));

        Assert.Equal(RemovalExecutionPhase.PreFlight, result.Phase);
        Assert.DoesNotContain(dism.Calls, c => c.StartsWith("mount:"));
    }

    [Fact]
    public async Task Orphan_mount_under_our_workspace_is_recovered_before_mounting()
    {
        var orphan = Path.Combine(_workspace, "old-run", "mount");
        var dism = new FakeDismRunner
        {
            MountedWimInfoOutput = $"Mount Dir : {orphan}\nImage File : X:\\install.wim\nStatus : Ok",
        };
        var engine = new global::MRS.RemovalEngine.RemovalEngine(dism);

        var clipchamp = PlanFactory.Item("appx:Clipchamp", "Clipchamp", ComponentSourceType.Appx, RemovalActionType.RemoveAppx);
        await engine.ExecuteAsync(NewImage(), PlanFactory.Plan(clipchamp));

        Assert.Contains($"discard:{orphan}", dism.Calls);
    }

    // ================= APPX / FEATURE / CAPABILITY / PACKAGE =================

    [Fact]
    public async Task Successful_appx_removal_uses_the_catalog_package_name_as_target()
    {
        var dism = new FakeDismRunner();
        var engine = new global::MRS.RemovalEngine.RemovalEngine(dism);

        var clipchamp = PlanFactory.Item("appx:MicrosoftCorporationII.MicrosoftClipchamp_1.0_x64", "Clipchamp",
            ComponentSourceType.Appx, RemovalActionType.RemoveAppx);
        var result = await engine.ExecuteAsync(NewImage(), PlanFactory.Plan(clipchamp));

        Assert.True(result.Success);
        Assert.Contains("remove-appx:MicrosoftCorporationII.MicrosoftClipchamp_1.0_x64", dism.Calls);
        Assert.Equal(0, result.ActionsExecuted.Single().ExitCode);
    }

    [Fact]
    public async Task Feature_removal_calls_disable_feature()
    {
        var dism = new FakeDismRunner();
        var engine = new global::MRS.RemovalEngine.RemovalEngine(dism);

        var feature = PlanFactory.Item("feature:MediaPlayback", "MediaPlayback",
            ComponentSourceType.Feature, RemovalActionType.DisableFeature, protection: ComponentProtection.Optional);
        var result = await engine.ExecuteAsync(NewImage(), PlanFactory.Plan(feature));

        Assert.True(result.Success);
        Assert.Contains("disable-feature:MediaPlayback", dism.Calls);
    }

    [Fact]
    public async Task Capability_removal_calls_remove_capability()
    {
        var dism = new FakeDismRunner();
        var engine = new global::MRS.RemovalEngine.RemovalEngine(dism);

        var capability = PlanFactory.Item("capability:OpenSSH.Client~~~~0.0.1.0", "OpenSSH Client",
            ComponentSourceType.Capability, RemovalActionType.RemoveCapability, protection: ComponentProtection.Optional);
        var result = await engine.ExecuteAsync(NewImage(), PlanFactory.Plan(capability));

        Assert.True(result.Success);
        Assert.Contains("remove-capability:OpenSSH.Client~~~~0.0.1.0", dism.Calls);
    }

    [Fact]
    public async Task Package_removal_calls_remove_package_and_never_resetbase_or_cleanup()
    {
        var dism = new FakeDismRunner();
        var engine = new global::MRS.RemovalEngine.RemovalEngine(dism);

        var package = PlanFactory.Item("package:Contoso.Sample~amd64~~1.0.0.0", "Contoso.Sample",
            ComponentSourceType.Package, RemovalActionType.RemovePackage);
        var result = await engine.ExecuteAsync(NewImage(), PlanFactory.Plan(package));

        Assert.True(result.Success);
        Assert.Contains("remove-package:Contoso.Sample~amd64~~1.0.0.0", dism.Calls);
        Assert.DoesNotContain(dism.Calls, c => c.Contains("resetbase", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(dism.Calls, c => c.Contains("cleanup", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Failed_appx_removal_aborts_and_discards()
    {
        var dism = new FakeDismRunner();
        dism.ActionExitCodes["Clipchamp"] = 123;
        var engine = new global::MRS.RemovalEngine.RemovalEngine(dism);

        var clipchamp = PlanFactory.Item("appx:Clipchamp", "Clipchamp", ComponentSourceType.Appx, RemovalActionType.RemoveAppx);
        var result = await engine.ExecuteAsync(NewImage(), PlanFactory.Plan(clipchamp));

        Assert.False(result.Success);
        Assert.Equal(RemovalExecutionPhase.Failed, result.Phase);
        Assert.Single(result.ActionsFailed);
        Assert.Equal(123, result.ActionsFailed[0].ExitCode);
        Assert.Contains(dism.Calls, c => c.StartsWith("discard:"));
        Assert.DoesNotContain(dism.Calls, c => c.StartsWith("commit:"));
        Assert.False(result.Committed);
        Assert.True(result.Discarded);
    }

    // ================= SEGURIDAD =================

    [Fact]
    public async Task Protected_component_is_never_executed_even_if_present_in_actions()
    {
        var dism = new FakeDismRunner();
        var engine = new global::MRS.RemovalEngine.RemovalEngine(dism);

        // Plan inconsistente a propósito: el item está protegido pero se cuela una acción para él.
        var vclibs = PlanFactory.Item("appx:VCLibs", "VCLibs", ComponentSourceType.Appx, RemovalActionType.RemoveAppx,
            allowed: true, protection: ComponentProtection.Protected);
        var plan = PlanFactory.Plan(vclibs) with
        {
            Actions = new[] { PlanFactory.Action(vclibs) },
        };

        var result = await engine.ExecuteAsync(NewImage(), plan);

        Assert.False(result.Success);
        Assert.DoesNotContain(dism.Calls, c => c.StartsWith("remove-appx:"));
        Assert.Contains(dism.Calls, c => c.StartsWith("discard:"));
    }

    [Fact]
    public async Task Empty_target_is_never_executed()
    {
        var dism = new FakeDismRunner();
        var engine = new global::MRS.RemovalEngine.RemovalEngine(dism);

        var item = PlanFactory.Item("appx:Clipchamp", "Clipchamp", ComponentSourceType.Appx, RemovalActionType.RemoveAppx);
        var plan = PlanFactory.Plan(item) with
        {
            Actions = new[] { PlanFactory.Action(item, target: "") },
        };

        var result = await engine.ExecuteAsync(NewImage(), plan);

        Assert.False(result.Success);
        Assert.DoesNotContain(dism.Calls, c => c.StartsWith("remove-appx:"));
    }

    [Fact]
    public async Task Action_incompatible_with_source_type_is_never_executed()
    {
        var dism = new FakeDismRunner();
        var engine = new global::MRS.RemovalEngine.RemovalEngine(dism);

        // El item es un Package pero declara una acción de Appx: incompatible.
        var item = PlanFactory.Item("package:Contoso", "Contoso", ComponentSourceType.Package, RemovalActionType.RemoveAppx);
        var plan = PlanFactory.Plan(item);

        var result = await engine.ExecuteAsync(NewImage(), plan);

        Assert.False(result.Success);
        Assert.DoesNotContain(dism.Calls, c => c.StartsWith("remove-appx:"));
        Assert.DoesNotContain(dism.Calls, c => c.StartsWith("remove-package:"));
    }

    // ================= ORDEN =================

    [Fact]
    public async Task Actions_execute_in_appx_feature_capability_package_order()
    {
        var dism = new FakeDismRunner();
        var engine = new global::MRS.RemovalEngine.RemovalEngine(dism);

        var package = PlanFactory.Item("package:Pkg", "Pkg", ComponentSourceType.Package, RemovalActionType.RemovePackage);
        var appx = PlanFactory.Item("appx:App", "App", ComponentSourceType.Appx, RemovalActionType.RemoveAppx);
        var feature = PlanFactory.Item("feature:Feat", "Feat", ComponentSourceType.Feature, RemovalActionType.DisableFeature, protection: ComponentProtection.Optional);
        var capability = PlanFactory.Item("capability:Cap", "Cap", ComponentSourceType.Capability, RemovalActionType.RemoveCapability, protection: ComponentProtection.Optional);

        // Deliberadamente en un orden "incorrecto" en el plan.
        var plan = PlanFactory.Plan(package, appx, feature, capability) with
        {
            Actions = new[] { PlanFactory.Action(package), PlanFactory.Action(appx), PlanFactory.Action(feature), PlanFactory.Action(capability) },
        };

        await engine.ExecuteAsync(NewImage(), plan);

        var order = dism.Calls.Where(c => c.StartsWith("remove-appx:") || c.StartsWith("disable-feature:") ||
                                           c.StartsWith("remove-capability:") || c.StartsWith("remove-package:"))
            .Select(c => c.Split(':')[0]).ToList();

        Assert.Equal(new[] { "remove-appx", "disable-feature", "remove-capability", "remove-package" }, order);
    }

    // ================= CANCELACIÓN =================

    [Fact]
    public async Task Cancellation_after_first_action_stops_before_the_next_one_and_discards()
    {
        using var cts = new CancellationTokenSource();
        var dism = new FakeDismRunner();
        dism.AfterAction["First"] = () => cts.Cancel();
        var engine = new global::MRS.RemovalEngine.RemovalEngine(dism);

        var first = PlanFactory.Item("appx:First", "First", ComponentSourceType.Appx, RemovalActionType.RemoveAppx);
        var second = PlanFactory.Item("appx:Second", "Second", ComponentSourceType.Appx, RemovalActionType.RemoveAppx);

        var result = await engine.ExecuteAsync(NewImage(), PlanFactory.Plan(first, second), cts.Token);

        Assert.Equal(RemovalExecutionPhase.Cancelled, result.Phase);
        Assert.False(result.Committed);
        Assert.Contains(dism.Calls, c => c.StartsWith("discard:"));
        Assert.DoesNotContain(dism.Calls, c => c == "remove-appx:Second");
        Assert.Contains(result.Warnings, w => w.Contains("cancelada", StringComparison.OrdinalIgnoreCase));
    }

    // ================= COMMIT =================

    [Fact]
    public async Task All_actions_succeeding_results_in_a_single_commit_and_no_discard()
    {
        var dism = new FakeDismRunner();
        var engine = new global::MRS.RemovalEngine.RemovalEngine(dism);

        var clipchamp = PlanFactory.Item("appx:Clipchamp", "Clipchamp", ComponentSourceType.Appx, RemovalActionType.RemoveAppx);
        var result = await engine.ExecuteAsync(NewImage(), PlanFactory.Plan(clipchamp));

        Assert.True(result.Success);
        Assert.True(result.Committed);
        Assert.False(result.Discarded);
        Assert.Equal(1, dism.Calls.Count(c => c.StartsWith("commit:")));
        Assert.DoesNotContain(dism.Calls, c => c.StartsWith("discard:"));
    }

    [Fact]
    public async Task Commit_failure_is_reported_as_failed_and_not_committed()
    {
        var dism = new FakeDismRunner { UnmountCommitExitCode = 87 };
        var engine = new global::MRS.RemovalEngine.RemovalEngine(dism);

        var clipchamp = PlanFactory.Item("appx:Clipchamp", "Clipchamp", ComponentSourceType.Appx, RemovalActionType.RemoveAppx);
        var result = await engine.ExecuteAsync(NewImage(), PlanFactory.Plan(clipchamp));

        Assert.False(result.Success);
        Assert.False(result.Committed);
        Assert.Equal(RemovalExecutionPhase.Failed, result.Phase);
    }

    // ================= POST-VALIDATION =================

    [Fact]
    public async Task Mounted_wim_info_is_checked_after_commit()
    {
        var dism = new FakeDismRunner();
        var engine = new global::MRS.RemovalEngine.RemovalEngine(dism);

        var clipchamp = PlanFactory.Item("appx:Clipchamp", "Clipchamp", ComponentSourceType.Appx, RemovalActionType.RemoveAppx);
        await engine.ExecuteAsync(NewImage(), PlanFactory.Plan(clipchamp));

        var commitIndex = dism.Calls.FindIndex(c => c.StartsWith("commit:"));
        Assert.Contains("mountedinfo", dism.Calls.Skip(commitIndex + 1));
    }

    [Fact]
    public async Task Mounted_wim_info_is_checked_after_discard()
    {
        var dism = new FakeDismRunner();
        dism.ActionExitCodes["Clipchamp"] = 5;
        var engine = new global::MRS.RemovalEngine.RemovalEngine(dism);

        var clipchamp = PlanFactory.Item("appx:Clipchamp", "Clipchamp", ComponentSourceType.Appx, RemovalActionType.RemoveAppx);
        await engine.ExecuteAsync(NewImage(), PlanFactory.Plan(clipchamp));

        var discardIndex = dism.Calls.FindIndex(c => c.StartsWith("discard:"));
        Assert.Contains("mountedinfo", dism.Calls.Skip(discardIndex + 1));
    }

    // ================= WORKSPACE =================

    [Fact]
    public async Task Workspace_is_preserved_after_a_failure()
    {
        var dism = new FakeDismRunner();
        dism.ActionExitCodes["Clipchamp"] = 5;
        var engine = new global::MRS.RemovalEngine.RemovalEngine(dism);
        var image = NewImage();

        await engine.ExecuteAsync(image, PlanFactory.Plan(
            PlanFactory.Item("appx:Clipchamp", "Clipchamp", ComponentSourceType.Appx, RemovalActionType.RemoveAppx)));

        Assert.True(Directory.Exists(image.WorkspacePath));
    }

    [Fact]
    public async Task Workspace_is_preserved_after_success_because_it_holds_the_working_image()
    {
        var dism = new FakeDismRunner();
        var engine = new global::MRS.RemovalEngine.RemovalEngine(dism);
        var image = NewImage();

        await engine.ExecuteAsync(image, PlanFactory.Plan(
            PlanFactory.Item("appx:Clipchamp", "Clipchamp", ComponentSourceType.Appx, RemovalActionType.RemoveAppx)));

        Assert.True(Directory.Exists(image.WorkspacePath));
        Assert.True(File.Exists(image.WorkingWimPath));
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_workspace)) Directory.Delete(_workspace, recursive: true); }
        catch { /* best-effort */ }
    }
}
