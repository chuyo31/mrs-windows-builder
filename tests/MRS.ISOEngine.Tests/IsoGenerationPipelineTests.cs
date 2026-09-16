using MRS.ISOEngine.Models;
using MRS.ISOEngine.Pipeline;
using MRS.ISOEngine.TreeCopy;
using MRS.PostInstall.Models;
using MRS.ISOEngine.Tests.Fakes;
using Xunit;
using InstallationOptionsModel = MRS.InstallationOptions.Models.InstallationOptions;

namespace MRS.ISOEngine.Tests;

/// <summary>
/// P19: integra Validación -&gt; Workspace -&gt; árbol de la ISO -&gt; boot.wim
/// (InstallationImageService, P16) -&gt; install.wim (WorkingImageFactory +
/// RemovalEngine, P07) -&gt; PostInstall (P18) -&gt; validación final -&gt; oscdimg,
/// con todas las dependencias externas simuladas. Ninguna prueba ejecuta DISM,
/// reg.exe ni oscdimg real.
/// </summary>
public sealed class IsoGenerationPipelineTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "mrs-isogenpipeline-tests", Guid.NewGuid().ToString("N"));
    private readonly string _mountedIsoRoot;
    private readonly string _sourceIso;
    private readonly string _modifiedInstallWim;

    private readonly FakeIsoTreeCopier _treeCopier = new();
    private readonly FakeIsoMounter _isoMounter;
    private readonly FakeInstallationImageService _installationImageService = new();
    private readonly FakeWorkingImageFactory _workingImageFactory = new();
    private readonly FakeRemovalEngine _removalEngine = new();
    private readonly FakePostInstallPackageBuilder _postInstallPackageBuilder = new();
    private readonly FakeOscdimgRunner _oscdimgRunner = new();

    public IsoGenerationPipelineTests()
    {
        Directory.CreateDirectory(_dir);

        _sourceIso = Path.Combine(_dir, "source.iso");
        File.WriteAllText(_sourceIso, "fake iso");

        _mountedIsoRoot = Path.Combine(_dir, "mounted-iso");
        Directory.CreateDirectory(Path.Combine(_mountedIsoRoot, "boot"));
        File.WriteAllText(Path.Combine(_mountedIsoRoot, "boot", "etfsboot.com"), "x");
        Directory.CreateDirectory(Path.Combine(_mountedIsoRoot, "efi", "microsoft", "boot"));
        File.WriteAllText(Path.Combine(_mountedIsoRoot, "efi", "microsoft", "boot", "efisys.bin"), "x");
        Directory.CreateDirectory(Path.Combine(_mountedIsoRoot, "sources"));
        File.WriteAllText(Path.Combine(_mountedIsoRoot, "sources", "boot.wim"), "fake boot.wim");
        File.WriteAllText(Path.Combine(_mountedIsoRoot, "sources", "install.wim"), "fake install.wim original");

        _isoMounter = new FakeIsoMounter(_mountedIsoRoot);

        // Simula lo que haría el IsoTreeCopier real: copiar el árbol montado al workspace.
        _treeCopier.OnCopy = (_, destination) => DirectoryCopyHelper.CopyAll(_mountedIsoRoot, destination);

        _modifiedInstallWim = Path.Combine(_dir, "working-image", "install.wim");
        Directory.CreateDirectory(Path.GetDirectoryName(_modifiedInstallWim)!);
        File.WriteAllText(_modifiedInstallWim, "fake install.wim modificado");
        _workingImageFactory.WorkingWimPathToReturn = _modifiedInstallWim;
    }

    private IsoGenerationPipeline NewPipeline()
        => new(_treeCopier, _isoMounter, _installationImageService, _workingImageFactory, _removalEngine, _postInstallPackageBuilder, _oscdimgRunner);

    private IsoGenerationRequest ValidRequest() => new()
    {
        SourceIsoPath = _sourceIso,
        EditionIndex = 6,
        Architecture = "amd64",
        InstallationOptions = InstallationOptionsModel.Default with { BypassStorage = false },
        AccountConfiguration = new AutounattendConfiguration { AccountName = "Usuario" },
        PostInstallConfiguration = new PostInstallConfiguration { Enabled = false },
        OutputIsoPath = Path.Combine(_dir, "output.iso"),
    };

    [Fact]
    public async Task A_full_successful_run_calls_every_stage_in_order_and_produces_the_final_ISO()
    {
        var pipeline = NewPipeline();

        var result = await pipeline.GenerateAsync(ValidRequest());

        Assert.True(result.Success);
        Assert.Equal(Path.Combine(_dir, "output.iso"), result.OutputIsoPath);
        Assert.Equal(1, _treeCopier.CallCount);
        Assert.Equal(1, _installationImageService.CallCount);
        Assert.Equal(1, _workingImageFactory.CallCount);
        Assert.Equal(1, _removalEngine.CallCount);
        Assert.Equal(1, _oscdimgRunner.CallCount);
    }

    [Fact]
    public async Task Invalid_request_aborts_before_creating_any_workspace()
    {
        var pipeline = NewPipeline();
        var request = ValidRequest() with { SourceIsoPath = Path.Combine(_dir, "no-existe.iso") };

        var result = await pipeline.GenerateAsync(request);

        Assert.False(result.Success);
        Assert.Null(result.Workspace);
        Assert.Equal(0, _treeCopier.CallCount);
    }

    [Fact]
    public async Task A_boot_wim_failure_aborts_before_touching_install_wim()
    {
        _installationImageService.Success = false;
        _installationImageService.Errors = new[] { "LabConfig verification failed" };
        var pipeline = NewPipeline();

        var result = await pipeline.GenerateAsync(ValidRequest());

        Assert.False(result.Success);
        Assert.Contains("LabConfig verification failed", result.Errors);
        Assert.Equal(0, _workingImageFactory.CallCount);
        Assert.Equal(0, _removalEngine.CallCount);
        Assert.Equal(0, _oscdimgRunner.CallCount);
    }

    [Fact]
    public async Task An_install_wim_failure_aborts_before_PostInstall_but_still_releases_the_ISO_mount()
    {
        _removalEngine.Success = false;
        _removalEngine.Errors = new[] { "DISM exit code 1603" };
        var pipeline = NewPipeline();

        var result = await pipeline.GenerateAsync(ValidRequest());

        Assert.False(result.Success);
        Assert.Contains("DISM exit code 1603", result.Errors);
        Assert.Equal(0, _postInstallPackageBuilder.CallCount);
        Assert.Equal(1, _isoMounter.DisposeCount); // el mount de instalación se libera aunque falle
    }

    [Fact]
    public async Task No_RemovalPlan_supplied_still_applies_an_empty_plan_selecting_only_the_Pro_edition()
    {
        var pipeline = NewPipeline();
        var request = ValidRequest() with { RemovalPlan = null };

        var result = await pipeline.GenerateAsync(request);

        Assert.True(result.Success);
        Assert.NotNull(_removalEngine.LastPlan);
        Assert.Empty(_removalEngine.LastPlan!.Actions);
    }

    [Fact]
    public async Task Disabled_PostInstall_never_calls_the_package_builder()
    {
        var pipeline = NewPipeline();

        var result = await pipeline.GenerateAsync(ValidRequest());

        Assert.True(result.Success);
        Assert.Equal(0, _postInstallPackageBuilder.CallCount);
    }

    [Fact]
    public async Task Enabled_PostInstall_merges_the_generated_OEM_folder_into_sources_OEM()
    {
        _postInstallPackageBuilder.OnBuild = oemRoot =>
        {
            Directory.CreateDirectory(Path.Combine(oemRoot, "$$", "Setup", "Scripts"));
            File.WriteAllText(Path.Combine(oemRoot, "$$", "Setup", "Scripts", "SetupComplete.cmd"), "@echo off");
        };
        var pipeline = NewPipeline();

        // PostInstallPackageValidator (P18) exige que .NET/PCPI existan de verdad
        // en cuanto Enabled=true: la propia validación del pipeline (P19) reutiliza
        // esa lógica sin duplicarla.
        var dotnetInstaller = Path.Combine(_dir, "windowsdesktop-runtime-8.0.26-win-x64.exe");
        File.WriteAllText(dotnetInstaller, "fake dotnet installer");
        var pcpiInstaller = Path.Combine(_dir, "PCPI-Retro-Minimals-Portable-0.0.5.exe");
        File.WriteAllText(pcpiInstaller, "fake pcpi installer");

        var request = ValidRequest() with
        {
            PostInstallConfiguration = new PostInstallConfiguration { Enabled = true },
            PostInstallSourceFiles = new PostInstallSourceFiles
            {
                DotNetInstallerPath = dotnetInstaller,
                PcpiInstallerPath = pcpiInstaller,
            },
        };

        var result = await pipeline.GenerateAsync(request);

        Assert.True(result.Success);
        Assert.Equal(1, _postInstallPackageBuilder.CallCount);
        var mergedScript = Path.Combine(result.Workspace!.WorkspacePath, "sources", "$OEM$", "$$", "Setup", "Scripts", "SetupComplete.cmd");
        Assert.True(File.Exists(mergedScript));
    }

    [Fact]
    public async Task Oscdimg_unavailable_aborts_with_a_clear_message_without_calling_BuildIsoAsync()
    {
        _oscdimgRunner.Available = false;
        var pipeline = NewPipeline();

        var result = await pipeline.GenerateAsync(ValidRequest());

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("oscdimg"));
        Assert.Equal(0, _oscdimgRunner.CallCount);
    }

    [Fact]
    public async Task Oscdimg_failure_is_reported_with_its_ExitCode()
    {
        _oscdimgRunner.ExitCode = 5;
        var pipeline = NewPipeline();

        var result = await pipeline.GenerateAsync(ValidRequest());

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("5"));
    }

    [Fact]
    public async Task The_final_ISO_is_never_produced_if_any_stage_fails()
    {
        _removalEngine.Success = false;
        var pipeline = NewPipeline();

        var result = await pipeline.GenerateAsync(ValidRequest());

        Assert.False(result.Success);
        Assert.Null(result.OutputIsoPath);
    }

    [Fact]
    public async Task The_source_ISO_is_never_modified_by_a_full_run()
    {
        var originalHash = File.ReadAllText(_sourceIso);
        var pipeline = NewPipeline();

        await pipeline.GenerateAsync(ValidRequest());

        Assert.Equal(originalHash, File.ReadAllText(_sourceIso));
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); }
        catch { /* best-effort */ }
    }
}
