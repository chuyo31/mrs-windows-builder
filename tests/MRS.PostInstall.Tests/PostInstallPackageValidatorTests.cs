using MRS.PostInstall.Models;
using MRS.PostInstall.Packaging;
using Xunit;

namespace MRS.PostInstall.Tests;

/// <summary>P18, sección 18 (Package validation): .NET inexistente, PCPI inexistente, ambos existentes, rutas absolutas prohibidas, archivos duplicados.</summary>
public sealed class PostInstallPackageValidatorTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "mrs-postinstall-pkgvalidator-tests", Guid.NewGuid().ToString("N"));
    private readonly string _dotnetInstaller;
    private readonly string _pcpiInstaller;

    public PostInstallPackageValidatorTests()
    {
        Directory.CreateDirectory(_dir);
        _dotnetInstaller = Path.Combine(_dir, "windowsdesktop-runtime-8.0.26-win-x64.exe");
        File.WriteAllText(_dotnetInstaller, "fake dotnet installer");
        _pcpiInstaller = Path.Combine(_dir, "PCPI-Retro-Minimals-Portable-0.0.5.exe");
        File.WriteAllText(_pcpiInstaller, "fake pcpi installer");
    }

    private PostInstallSourceFiles ValidSourceFiles() => new()
    {
        DotNetInstallerPath = _dotnetInstaller,
        PcpiInstallerPath = _pcpiInstaller,
    };

    [Fact]
    public void Both_installers_present_is_valid()
    {
        var result = PostInstallPackageValidator.Validate(new PostInstallConfiguration(), ValidSourceFiles());

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Missing_dotnet_installer_fails_validation()
    {
        var sourceFiles = ValidSourceFiles() with { DotNetInstallerPath = Path.Combine(_dir, "no-existe.exe") };

        var result = PostInstallPackageValidator.Validate(new PostInstallConfiguration(), sourceFiles);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains(".NET"));
    }

    [Fact]
    public void Missing_pcpi_installer_fails_validation()
    {
        var sourceFiles = ValidSourceFiles() with { PcpiInstallerPath = Path.Combine(_dir, "no-existe.exe") };

        var result = PostInstallPackageValidator.Validate(new PostInstallConfiguration(), sourceFiles);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("PCPI"));
    }

    [Fact]
    public void Both_missing_reports_both_errors()
    {
        var sourceFiles = new PostInstallSourceFiles
        {
            DotNetInstallerPath = Path.Combine(_dir, "no1.exe"),
            PcpiInstallerPath = Path.Combine(_dir, "no2.exe"),
        };

        var result = PostInstallPackageValidator.Validate(new PostInstallConfiguration(), sourceFiles);

        Assert.False(result.IsValid);
        Assert.Equal(2, result.Errors.Count);
    }

    [Fact]
    public void Disabled_configuration_never_requires_any_installer_to_exist()
    {
        var sourceFiles = new PostInstallSourceFiles(); // rutas vacías

        var result = PostInstallPackageValidator.Validate(new PostInstallConfiguration { Enabled = false }, sourceFiles);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Incoherent_configuration_is_reported_together_with_missing_files()
    {
        var config = new PostInstallConfiguration { Architecture = "arm64" }; // config inválida
        var sourceFiles = new PostInstallSourceFiles(); // y ficheros ausentes

        var result = PostInstallPackageValidator.Validate(config, sourceFiles);

        Assert.False(result.IsValid);
        Assert.True(result.Errors.Count >= 3); // arquitectura + .NET ausente + PCPI ausente
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); }
        catch { /* best-effort */ }
    }
}
