using MRS.ISOEngine.BootWim;
using MRS.ISOEngine.Models;
using MRS.ISOEngine.Tests.Fakes;
using Xunit;
using InstallationOptionsModel = MRS.InstallationOptions.Models.InstallationOptions;

namespace MRS.ISOEngine.Tests;

/// <summary>
/// P16, sección 10/11/13/17: integra Planner → InstallationImageService →
/// BootWimModifier → AutounattendGenerator sobre un workspace real en disco
/// (con DISM/registro simulados). Comprueba idempotencia, aborto seguro cuando
/// el bypass de almacenamiento está activado, y que la ISO "original" (la
/// carpeta que hace de ISO montada) nunca se toca.
/// </summary>
public sealed class InstallationImageServiceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "mrs-installationimageservice-tests", Guid.NewGuid().ToString("N"));
    private readonly string _mountedIsoRoot;
    private readonly GenerationWorkspace _workspace;
    private readonly FakeDismRunner _dism = new();
    private readonly FakeOfflineRegistryEditor _registry = new();

    public InstallationImageServiceTests()
    {
        Directory.CreateDirectory(_dir);

        _mountedIsoRoot = Path.Combine(_dir, "mounted-iso");
        Directory.CreateDirectory(Path.Combine(_mountedIsoRoot, "sources"));
        File.WriteAllText(Path.Combine(_mountedIsoRoot, "sources", "boot.wim"), "fake boot.wim original");

        var workspaceRoot = Path.Combine(_dir, "workspace");
        Directory.CreateDirectory(Path.Combine(workspaceRoot, "sources"));
        Directory.CreateDirectory(Path.Combine(workspaceRoot, "mount"));

        _workspace = new GenerationWorkspace
        {
            SourceIsoPath = "fake-source.iso",
            WorkspacePath = workspaceRoot,
            BootWimPath = Path.Combine(workspaceRoot, "sources", "boot.wim"),
            InstallWimPath = Path.Combine(workspaceRoot, "sources", "install.wim"),
            MountPath = Path.Combine(workspaceRoot, "mount"),
            Index = 6,
            Architecture = "amd64",
        };
    }

    private InstallationImageService NewService()
        => new(new BootWimProvisioner(new FakeIsoMounter(_mountedIsoRoot)), new BootWimModifier(_dism, _registry));

    private static AutounattendConfiguration Account() => new() { AccountName = "Usuario", ComputerName = "MRS-PC" };

    /// <summary>
    /// <see cref="InstallationOptionsModel.Default"/> tiene BypassStorage=true (todos los
    /// bypasses empiezan habilitados, sección 2 del prompt) — eso es justo lo que
    /// InstallationExecutionValidator debe bloquear (sección 9). Para los escenarios de
    /// "todo funciona", se desactiva explícitamente, igual que tendría que hacer un
    /// llamador real que no quiera activar una opción sin mecanismo implementado.
    /// </summary>
    private static InstallationOptionsModel SafeOptions() => InstallationOptionsModel.Default with { BypassStorage = false };

    [Fact]
    public async Task A_successful_run_copies_boot_wim_applies_LabConfig_and_writes_autounattend()
    {
        var service = NewService();

        var result = await service.ApplyAsync("fake-source.iso", _workspace, SafeOptions(), Account());

        Assert.True(result.Success);
        Assert.True(File.Exists(_workspace.BootWimPath));
        Assert.NotNull(result.AutounattendPath);
        Assert.True(File.Exists(result.AutounattendPath));
        Assert.Contains("[INSTALL] Local account enabled", result.AppliedLogLines);
        Assert.Contains("[COMPAT] BypassTPM applied", result.AppliedLogLines);
    }

    [Fact]
    public async Task Storage_bypass_enabled_aborts_before_touching_the_workspace()
    {
        var service = NewService();
        var options = InstallationOptionsModel.Default with { BypassStorage = true };

        var result = await service.ApplyAsync("fake-source.iso", _workspace, options, Account());

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("almacenamiento"));
        Assert.False(File.Exists(_workspace.BootWimPath)); // nunca llegó a copiar boot.wim
        Assert.Empty(_dism.Calls); // nunca llegó a montar nada
    }

    [Fact]
    public async Task AllowLocalAccount_false_never_writes_an_autounattend_file()
    {
        var service = NewService();
        var options = SafeOptions() with { AllowLocalAccount = false };

        var result = await service.ApplyAsync("fake-source.iso", _workspace, options, Account());

        Assert.True(result.Success);
        Assert.Null(result.AutounattendPath);
        Assert.False(File.Exists(Path.Combine(_workspace.WorkspacePath, "autounattend.xml")));
    }

    [Fact]
    public async Task Disabling_local_account_after_a_previous_run_removes_the_stale_autounattend_file()
    {
        var service = NewService();
        await service.ApplyAsync("fake-source.iso", _workspace, SafeOptions(), Account());
        Assert.True(File.Exists(Path.Combine(_workspace.WorkspacePath, "autounattend.xml")));

        await service.ApplyAsync("fake-source.iso", _workspace, SafeOptions() with { AllowLocalAccount = false }, Account());

        Assert.False(File.Exists(Path.Combine(_workspace.WorkspacePath, "autounattend.xml")));
    }

    [Fact]
    public async Task Running_twice_with_the_same_options_never_duplicates_the_autounattend_file()
    {
        var service = NewService();

        await service.ApplyAsync("fake-source.iso", _workspace, SafeOptions(), Account());
        await service.ApplyAsync("fake-source.iso", _workspace, SafeOptions(), Account());

        var autounattendFiles = Directory.GetFiles(_workspace.WorkspacePath, "autounattend*.xml");
        Assert.Single(autounattendFiles);
    }

    [Fact]
    public async Task The_mounted_source_ISO_is_never_modified()
    {
        var service = NewService();
        var originalContent = File.ReadAllText(Path.Combine(_mountedIsoRoot, "sources", "boot.wim"));

        await service.ApplyAsync("fake-source.iso", _workspace, SafeOptions(), Account());

        Assert.Equal(originalContent, File.ReadAllText(Path.Combine(_mountedIsoRoot, "sources", "boot.wim")));
    }

    [Fact]
    public async Task A_failed_run_preserves_the_workspace_directory()
    {
        _registry.LoadExitCode = 1; // fuerza un fallo dentro de BootWimModifier
        var service = NewService();

        var result = await service.ApplyAsync("fake-source.iso", _workspace, SafeOptions(), Account());

        Assert.False(result.Success);
        Assert.True(Directory.Exists(_workspace.WorkspacePath)); // nunca se borra a ciegas
        Assert.True(File.Exists(_workspace.BootWimPath)); // la copia de boot.wim tampoco se borra
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); }
        catch { /* best-effort */ }
    }
}
