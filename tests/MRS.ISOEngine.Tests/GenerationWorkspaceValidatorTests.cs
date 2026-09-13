using MRS.ISOEngine.Configuration;
using MRS.ISOEngine.Models;
using Xunit;

namespace MRS.ISOEngine.Tests;

/// <summary>
/// P15: <see cref="GenerationWorkspaceValidator"/> es la fase de validación previa
/// a generar la ISO final (sección 10 del prompt). Solo comprueba rutas; no monta
/// nada ni ejecuta DISM. Un workspace incompleto debe fallar la validación
/// (ABORTAR), nunca dar el visto bueno a algo aparentemente válido pero incompleto.
/// </summary>
public sealed class GenerationWorkspaceValidatorTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "mrs-isoengine-tests", Guid.NewGuid().ToString("N"));
    private readonly string _sourceIso;
    private readonly string _workspacePath;
    private readonly string _bootWim;
    private readonly string _installWim;

    public GenerationWorkspaceValidatorTests()
    {
        Directory.CreateDirectory(_dir);

        _sourceIso = Path.Combine(_dir, "source.iso");
        File.WriteAllText(_sourceIso, "fake iso");

        _workspacePath = Path.Combine(_dir, "workspace");
        Directory.CreateDirectory(_workspacePath);

        _bootWim = Path.Combine(_workspacePath, "boot.wim");
        File.WriteAllText(_bootWim, "fake boot.wim");

        _installWim = Path.Combine(_workspacePath, "install.wim");
        File.WriteAllText(_installWim, "fake install.wim");
    }

    private GenerationWorkspace ValidWorkspace() => new()
    {
        SourceIsoPath = _sourceIso,
        WorkspacePath = _workspacePath,
        BootWimPath = _bootWim,
        InstallWimPath = _installWim,
        Index = 6,
        Architecture = "amd64",
    };

    [Fact]
    public void A_complete_workspace_is_valid()
    {
        var result = GenerationWorkspaceValidator.Validate(ValidWorkspace());

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Missing_boot_wim_fails_validation()
    {
        var workspace = ValidWorkspace() with { BootWimPath = Path.Combine(_workspacePath, "no-existe-boot.wim") };

        var result = GenerationWorkspaceValidator.Validate(workspace);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("boot.wim"));
    }

    [Fact]
    public void Missing_install_wim_fails_validation()
    {
        var workspace = ValidWorkspace() with { InstallWimPath = Path.Combine(_workspacePath, "no-existe-install.wim") };

        var result = GenerationWorkspaceValidator.Validate(workspace);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("install.wim"));
    }

    [Fact]
    public void Missing_source_iso_fails_validation()
    {
        var workspace = ValidWorkspace() with { SourceIsoPath = Path.Combine(_dir, "no-existe.iso") };

        var result = GenerationWorkspaceValidator.Validate(workspace);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("ISO de origen"));
    }

    [Fact]
    public void Missing_workspace_directory_fails_validation()
    {
        var workspace = ValidWorkspace() with { WorkspacePath = Path.Combine(_dir, "no-existe") };

        var result = GenerationWorkspaceValidator.Validate(workspace);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("workspace"));
    }

    [Fact]
    public void Invalid_index_fails_validation()
    {
        var workspace = ValidWorkspace() with { Index = 0 };

        var result = GenerationWorkspaceValidator.Validate(workspace);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Índice"));
    }

    [Fact]
    public void Unsupported_architecture_fails_validation()
    {
        var workspace = ValidWorkspace() with { Architecture = "arm64" };

        var result = GenerationWorkspaceValidator.Validate(workspace);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("arquitectura", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void A_workspace_pointing_at_the_same_path_as_the_source_ISO_fails_validation()
    {
        // Nunca debe poder generarse "sobre" la ISO original: el workspace tiene
        // que ser siempre una copia.
        var workspace = ValidWorkspace() with { WorkspacePath = _sourceIso };

        var result = GenerationWorkspaceValidator.Validate(workspace);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("misma ruta"));
    }

    [Fact]
    public void Multiple_problems_are_all_reported_not_just_the_first()
    {
        var workspace = new GenerationWorkspace
        {
            SourceIsoPath = Path.Combine(_dir, "no-existe.iso"),
            WorkspacePath = Path.Combine(_dir, "tampoco-existe"),
            BootWimPath = string.Empty,
            InstallWimPath = string.Empty,
            Index = -1,
            Architecture = "x86",
        };

        var result = GenerationWorkspaceValidator.Validate(workspace);

        Assert.False(result.IsValid);
        Assert.True(result.Errors.Count >= 5);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); }
        catch { /* best-effort */ }
    }
}
