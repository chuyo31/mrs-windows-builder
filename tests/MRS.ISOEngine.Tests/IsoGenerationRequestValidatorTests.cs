using MRS.ISOEngine.Models;
using MRS.ISOEngine.Pipeline;
using MRS.PostInstall.Models;
using Xunit;
using InstallationOptionsModel = MRS.InstallationOptions.Models.InstallationOptions;

namespace MRS.ISOEngine.Tests;

/// <summary>P19, "VALIDACIÓN" inicial: combina las validaciones de P15/P16 (InstallationOptions) y P18 (PostInstall) con las propias del pipeline (ISO origen, índice, ruta de salida).</summary>
public sealed class IsoGenerationRequestValidatorTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "mrs-isogenreqvalidator-tests", Guid.NewGuid().ToString("N"));
    private readonly string _sourceIso;

    public IsoGenerationRequestValidatorTests()
    {
        Directory.CreateDirectory(_dir);
        _sourceIso = Path.Combine(_dir, "source.iso");
        File.WriteAllText(_sourceIso, "fake iso");
    }

    private IsoGenerationRequest ValidRequest() => new()
    {
        SourceIsoPath = _sourceIso,
        EditionIndex = 6,
        Architecture = "amd64",
        InstallationOptions = InstallationOptionsModel.Default with { BypassStorage = false },
        PostInstallConfiguration = new PostInstallConfiguration { Enabled = false },
        OutputIsoPath = Path.Combine(_dir, "output.iso"),
    };

    [Fact]
    public void A_well_formed_request_is_valid()
    {
        var result = IsoGenerationRequestValidator.Validate(ValidRequest());

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Missing_source_iso_fails_validation()
    {
        var request = ValidRequest() with { SourceIsoPath = Path.Combine(_dir, "no-existe.iso") };

        var result = IsoGenerationRequestValidator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("ISO de origen"));
    }

    [Fact]
    public void Invalid_edition_index_fails_validation()
    {
        var request = ValidRequest() with { EditionIndex = 0 };

        var result = IsoGenerationRequestValidator.Validate(request);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Missing_output_path_fails_validation()
    {
        var request = ValidRequest() with { OutputIsoPath = "" };

        var result = IsoGenerationRequestValidator.Validate(request);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Output_path_equal_to_source_path_fails_validation()
    {
        var request = ValidRequest() with { OutputIsoPath = _sourceIso };

        var result = IsoGenerationRequestValidator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("misma ruta"));
    }

    [Fact]
    public void Storage_bypass_enabled_fails_validation_reusing_P15_P16_logic()
    {
        var request = ValidRequest() with { InstallationOptions = InstallationOptionsModel.Default with { BypassStorage = true } };

        var result = IsoGenerationRequestValidator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("almacenamiento"));
    }

    [Fact]
    public void Enabled_PostInstall_without_real_installers_fails_validation_reusing_P18_logic()
    {
        var request = ValidRequest() with
        {
            PostInstallConfiguration = new PostInstallConfiguration { Enabled = true },
            PostInstallSourceFiles = new PostInstallSourceFiles(), // rutas vacías
        };

        var result = IsoGenerationRequestValidator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains(".NET") || e.Contains("PCPI"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); }
        catch { /* best-effort */ }
    }
}
