using MRS.ComponentCatalog.Models;
using MRS.RemovalEngine.Models;
using MRS.RemovalEngine.Tests.Fakes;
using MRS.RemovalPlanning.Models;
using Xunit;

namespace MRS.RemovalEngine.Tests;

/// <summary>
/// P09: una operación de <see cref="global::MRS.RemovalEngine.RemovalEngine"/>
/// debe usar exactamente un workspace de principio a fin. Estas pruebas
/// demuestran que si <see cref="WorkingImage.MountPath"/> o
/// <see cref="WorkingImage.WorkingWimPath"/> perteneciesen a un workspace
/// distinto de <see cref="WorkingImage.WorkspacePath"/> — la combinación
/// cruzada que causó la entrada "Status: Invalid" en Get-MountedWimInfo — se
/// detecta y la operación se aborta antes de montar nada.
/// </summary>
public sealed class WorkspaceLifecycleTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "mrs-workspace-lifecycle-tests", Guid.NewGuid().ToString("N"));

    private WorkingImage ConsistentImage(string workspace)
    {
        Directory.CreateDirectory(workspace);
        var wim = Path.Combine(workspace, "source", "install.wim");
        Directory.CreateDirectory(Path.GetDirectoryName(wim)!);
        File.WriteAllText(wim, "fake");
        var mount = Path.Combine(workspace, "mount");
        Directory.CreateDirectory(mount);

        return new WorkingImage
        {
            SourcePath = Path.Combine(workspace, "orig.wim"),
            WorkingWimPath = wim,
            Index = 1,
            MountPath = mount,
            WorkspacePath = workspace,
        };
    }

    [Fact]
    public void A_normally_created_working_image_has_a_consistent_workspace()
    {
        var image = ConsistentImage(Path.Combine(_root, "A"));

        Assert.True(image.HasConsistentWorkspace());
        Assert.Equal("A", image.WorkspaceId);
    }

    [Fact]
    public void Detects_a_mount_dir_belonging_to_a_different_workspace_than_the_wim()
    {
        // Reproduce el bug real de P09: Mount Dir de un workspace <A>, WIM de un
        // workspace <B> distinto.
        var workspaceA = ConsistentImage(Path.Combine(_root, "A"));
        var workspaceB = ConsistentImage(Path.Combine(_root, "B"));

        var crossed = new WorkingImage
        {
            SourcePath = workspaceB.SourcePath,
            WorkingWimPath = workspaceB.WorkingWimPath, // pertenece a B
            Index = 1,
            MountPath = workspaceA.MountPath,            // pertenece a A
            WorkspacePath = workspaceB.WorkspacePath,    // se declara como B
        };

        Assert.False(crossed.HasConsistentWorkspace());
    }

    [Fact]
    public async Task RemovalEngine_refuses_to_mount_a_working_image_with_a_crossed_workspace()
    {
        var workspaceA = ConsistentImage(Path.Combine(_root, "A"));
        var workspaceB = ConsistentImage(Path.Combine(_root, "B"));

        var crossed = new WorkingImage
        {
            SourcePath = workspaceB.SourcePath,
            WorkingWimPath = workspaceB.WorkingWimPath,
            Index = 1,
            MountPath = workspaceA.MountPath,
            WorkspacePath = workspaceB.WorkspacePath,
        };

        var dism = new FakeDismRunner();
        var engine = new global::MRS.RemovalEngine.RemovalEngine(dism);
        var clipchamp = PlanFactory.Item("appx:Clipchamp", "Clipchamp", ComponentSourceType.Appx, RemovalActionType.RemoveAppx);

        var result = await engine.ExecuteAsync(crossed, PlanFactory.Plan(clipchamp));

        Assert.False(result.Success);
        Assert.Equal(RemovalExecutionPhase.PreFlight, result.Phase);
        Assert.DoesNotContain(dism.Calls, c => c.StartsWith("mount:")); // nunca se llega a montar nada
    }

    [Fact]
    public async Task A_full_successful_run_never_changes_the_working_images_workspace()
    {
        var image = ConsistentImage(Path.Combine(_root, "single"));
        var dism = new FakeDismRunner();
        var engine = new global::MRS.RemovalEngine.RemovalEngine(dism);
        var clipchamp = PlanFactory.Item("appx:Clipchamp", "Clipchamp", ComponentSourceType.Appx, RemovalActionType.RemoveAppx);

        var result = await engine.ExecuteAsync(image, PlanFactory.Plan(clipchamp));

        Assert.True(result.Success);
        Assert.True(result.WorkingImage!.HasConsistentWorkspace());
        Assert.Equal(image.WorkspaceId, result.WorkingImage.WorkspaceId);

        // Todas las llamadas de montaje/desmontaje de ESTA operación usan el mismo MountDir.
        var mountRelated = dism.Calls.Where(c => c.StartsWith("mount:") || c.StartsWith("commit:") || c.StartsWith("discard:"));
        Assert.All(mountRelated, c => Assert.Contains(image.MountPath, c));
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
        catch { /* best-effort */ }
    }
}
