using MRS.PostInstall.Exceptions;
using MRS.PostInstall.Models;
using MRS.PostInstall.Packaging;
using Xunit;

namespace MRS.PostInstall.Tests;

/// <summary>
/// P18, sección 12/13/18 (Generación + Validación previa + Seguridad): el
/// paquete generado contiene el runtime, PCPI y SetupComplete.cmd; aborta si
/// falta algo o si el workspace de salida ya existe con contenido; nunca
/// contiene ninguna ruta del equipo de desarrollo.
/// </summary>
public sealed class PostInstallPackageBuilderTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "mrs-postinstall-builder-tests", Guid.NewGuid().ToString("N"));
    private readonly string _dotnetInstaller;
    private readonly string _pcpiInstaller;
    private readonly PostInstallPackageBuilder _builder = new();

    public PostInstallPackageBuilderTests()
    {
        Directory.CreateDirectory(_dir);
        _dotnetInstaller = Path.Combine(_dir, "windowsdesktop-runtime-8.0.26-win-x64.exe");
        File.WriteAllText(_dotnetInstaller, "fake dotnet installer contents");
        _pcpiInstaller = Path.Combine(_dir, "PCPI-Retro-Minimals-Portable-0.0.5.exe");
        File.WriteAllText(_pcpiInstaller, "fake pcpi installer contents");
    }

    private PostInstallSourceFiles ValidSourceFiles() => new()
    {
        DotNetInstallerPath = _dotnetInstaller,
        PcpiInstallerPath = _pcpiInstaller,
    };

    [Fact]
    public void A_successful_build_produces_the_documented_OEM_layout()
    {
        var output = Path.Combine(_dir, "output");

        var result = _builder.Build(new PostInstallConfiguration(), ValidSourceFiles(), output);

        Assert.True(result.Success);
        Assert.NotNull(result.OemRootPath);

        var scriptsDir = Path.Combine(output, "$OEM$", "$$", "Setup", "Scripts");
        Assert.True(File.Exists(Path.Combine(scriptsDir, "SetupComplete.cmd")));
        Assert.True(File.Exists(Path.Combine(scriptsDir, "dotnet", "windowsdesktop-runtime-8.0.26-win-x64.exe")));
        Assert.True(File.Exists(Path.Combine(scriptsDir, "pcpi", "PCPI-Retro-Minimals-Portable-0.0.5.exe")));
    }

    [Fact]
    public void Disabled_configuration_produces_no_package_at_all()
    {
        var output = Path.Combine(_dir, "output-disabled");

        var result = _builder.Build(new PostInstallConfiguration { Enabled = false }, new PostInstallSourceFiles(), output);

        Assert.True(result.Success);
        Assert.Null(result.OemRootPath);
        Assert.False(Directory.Exists(output));
    }

    [Fact]
    public void Missing_dotnet_installer_aborts_before_creating_any_output()
    {
        var output = Path.Combine(_dir, "output-missing-dotnet");
        var sourceFiles = ValidSourceFiles() with { DotNetInstallerPath = Path.Combine(_dir, "no-existe.exe") };

        var result = _builder.Build(new PostInstallConfiguration(), sourceFiles, output);

        Assert.False(result.Success);
        Assert.NotEmpty(result.Errors);
        Assert.False(Directory.Exists(output)); // nunca se genera un paquete incompleto
    }

    [Fact]
    public void Missing_pcpi_installer_aborts_before_creating_any_output()
    {
        var output = Path.Combine(_dir, "output-missing-pcpi");
        var sourceFiles = ValidSourceFiles() with { PcpiInstallerPath = Path.Combine(_dir, "no-existe.exe") };

        var result = _builder.Build(new PostInstallConfiguration(), sourceFiles, output);

        Assert.False(result.Success);
        Assert.False(Directory.Exists(output));
    }

    [Fact]
    public void A_non_empty_existing_output_directory_is_rejected()
    {
        var output = Path.Combine(_dir, "output-existing");
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output, "algo.txt"), "contenido previo inesperado");

        Assert.Throws<PostInstallException>(() => _builder.Build(new PostInstallConfiguration(), ValidSourceFiles(), output));
    }

    [Fact]
    public void No_developer_machine_path_ever_appears_inside_the_generated_package()
    {
        var output = Path.Combine(_dir, "output-security");

        _builder.Build(new PostInstallConfiguration(), ValidSourceFiles(), output);

        var scriptContent = File.ReadAllText(Path.Combine(output, "$OEM$", "$$", "Setup", "Scripts", "SetupComplete.cmd"));
        Assert.DoesNotContain("B3RASCASA", scriptContent);
        Assert.DoesNotContain(@"C:\Users\", scriptContent);
        Assert.DoesNotContain(_dir, scriptContent); // ni siquiera el propio directorio de origen de los instaladores
    }

    [Fact]
    public void Reports_progress_from_validation_through_finalizing()
    {
        var output = Path.Combine(_dir, "output-progress");
        // IProgress<T> síncrono a propósito: System.Progress<T> reenvía a través de un
        // SynchronizationContext (o del ThreadPool si no hay ninguno), así que sus
        // reportes podrían no haber llegado todavía cuando el test hace sus asserts.
        var progress = new RecordingProgress();

        _builder.Build(new PostInstallConfiguration(), ValidSourceFiles(), output, progress);

        Assert.NotEmpty(progress.Reports);
        Assert.Equal(100, progress.Reports[^1].Percent);
        Assert.Contains(progress.Reports, r => r.Level == PostInstallProgressLevel.Success);
    }

    private sealed class RecordingProgress : IProgress<PostInstallProgressInfo>
    {
        public List<PostInstallProgressInfo> Reports { get; } = new();
        public void Report(PostInstallProgressInfo value) => Reports.Add(value);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); }
        catch { /* best-effort */ }
    }
}
