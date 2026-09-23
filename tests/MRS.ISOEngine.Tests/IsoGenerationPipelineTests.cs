using MRS.ISOEngine.Models;
using MRS.ISOEngine.Pipeline;
using MRS.ISOEngine.TreeCopy;
using MRS.PostInstall.Models;
using MRS.ISOEngine.Tests.Fakes;
using MRS.RemovalPlanning.Models;
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
    private readonly FakeInstallWimOobeConfigurator _installWimOobeConfigurator = new();
    private readonly FakePostInstallPackageBuilder _postInstallPackageBuilder = new();
    private readonly FakeOscdimgRunner _oscdimgRunner = new();
    private readonly FakeElevationChecker _elevationChecker = new();

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
        => new(_treeCopier, _isoMounter, _installationImageService, _workingImageFactory, _removalEngine,
            _installWimOobeConfigurator, _postInstallPackageBuilder, _oscdimgRunner, _elevationChecker);

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
        Assert.Equal(1, _installationImageService.ValidateFinalCallCount);
        // AllowOfflineOobe=true por defecto en ValidRequest(): el pipeline debe
        // configurar BypassNRO también en install.wim (P29), no solo en boot.wim.
        Assert.Equal(1, _installWimOobeConfigurator.CallCount);
    }

    [Fact]
    public async Task A_failed_final_compatibility_validation_aborts_before_oscdimg()
    {
        // P28: la re-verificación final (boot.wim índices 1/2 + autounattend.xml)
        // puede fallar incluso si PrepareBootWim reportó éxito -- nunca debe
        // llegar a generar una ISO final en ese caso.
        _installationImageService.FinalValidationSuccess = false;
        _installationImageService.FinalValidationErrors = new[] { "boot.wim Index 2: BypassTPMCheck missing" };
        var pipeline = NewPipeline();

        var result = await pipeline.GenerateAsync(ValidRequest());

        Assert.False(result.Success);
        Assert.Contains("boot.wim Index 2: BypassTPMCheck missing", result.Errors);
        Assert.Equal(0, _oscdimgRunner.CallCount);
    }

    [Fact]
    public async Task Missing_elevation_aborts_with_the_exact_required_message_before_validating_the_request()
    {
        _elevationChecker.Elevated = false;
        var pipeline = NewPipeline();

        var result = await pipeline.GenerateAsync(ValidRequest());

        Assert.False(result.Success);
        Assert.Null(result.Workspace);
        Assert.Contains("Se requieren privilegios de administrador para ejecutar DISM.", result.Errors);
        Assert.Equal(0, _treeCopier.CallCount);
    }

    [Fact]
    public async Task Missing_oscdimg_aborts_with_the_exact_required_message_before_touching_any_WIM()
    {
        _oscdimgRunner.Available = false;
        var pipeline = NewPipeline();

        var result = await pipeline.GenerateAsync(ValidRequest());

        Assert.False(result.Success);
        Assert.Null(result.Workspace);
        Assert.Contains("Windows ADK/oscdimg no está instalado.", result.Errors);
        Assert.Equal(0, _treeCopier.CallCount);
        Assert.Equal(0, _installationImageService.CallCount);
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
    public async Task An_explicit_RemovalPlan_with_zero_selected_components_still_reaches_oscdimg()
    {
        // P23: 0 eliminaciones no es un error -- el pipeline debe llegar hasta
        // el final (oscdimg) igual que con un plan con acciones reales.
        var pipeline = NewPipeline();
        var request = ValidRequest() with { RemovalPlan = new RemovalPlan() };

        var result = await pipeline.GenerateAsync(request);

        Assert.True(result.Success);
        Assert.Equal(0, request.RemovalPlan!.TotalSelected);
        Assert.Equal(1, _oscdimgRunner.CallCount);
        Assert.NotNull(result.OutputIsoPath);
    }

    [Fact]
    public async Task A_ReadOnly_install_wim_inherited_from_the_ISO_tree_copy_with_zero_eliminations_still_completes()
    {
        // P27: reproduce el bug real. IsoTreeCopier ya copia sources\install.wim
        // al workspace de generación (heredando ReadOnly de la ISO montada en
        // solo lectura) ANTES de que WorkingImageFactory/RemovalEngine terminen
        // su propia copia de trabajo independiente. La sustitución final
        // (File.Copy con overwrite:true sobre esa ruta del workspace) fallaba
        // con "Access to the path ... is denied." porque .NET no quita ReadOnly
        // por sí solo al sobrescribir. Caso con 0 eliminaciones.
        var mountedInstallWim = Path.Combine(_mountedIsoRoot, "sources", "install.wim");
        File.SetAttributes(mountedInstallWim, File.GetAttributes(mountedInstallWim) | FileAttributes.ReadOnly);
        try
        {
            var pipeline = NewPipeline();
            var request = ValidRequest() with { RemovalPlan = new RemovalPlan() };

            var result = await pipeline.GenerateAsync(request);

            Assert.True(result.Success);
            Assert.Equal(0, request.RemovalPlan!.TotalSelected);
            var finalInstallWimPath = Path.Combine(result.Workspace!.WorkspacePath, "sources", "install.wim");
            Assert.Equal(File.ReadAllText(_modifiedInstallWim), File.ReadAllText(finalInstallWimPath));
        }
        finally
        {
            File.SetAttributes(mountedInstallWim, FileAttributes.Normal); // para que Dispose() pueda borrar _dir
        }
    }

    [Fact]
    public async Task A_ReadOnly_install_wim_inherited_from_the_ISO_tree_copy_with_eliminations_still_completes()
    {
        // P27: mismo escenario que el test anterior, pero con un RemovalPlan
        // que sí contiene acciones (TotalSelected > 0) -- el bug de ReadOnly es
        // independiente de si hubo eliminaciones o no: ocurre siempre en la
        // sustitución final, después de que RemovalEngine (real o simulado)
        // ya terminó con éxito.
        var mountedInstallWim = Path.Combine(_mountedIsoRoot, "sources", "install.wim");
        File.SetAttributes(mountedInstallWim, File.GetAttributes(mountedInstallWim) | FileAttributes.ReadOnly);
        try
        {
            var pipeline = NewPipeline();
            var clipchamp = new RemovalPlanItem
            {
                ComponentId = "appx:Clipchamp",
                DisplayName = "Clipchamp",
                Allowed = true,
                Requested = true,
            };
            var plan = new RemovalPlan { Components = new[] { clipchamp } };
            var request = ValidRequest() with { RemovalPlan = plan };

            var result = await pipeline.GenerateAsync(request);

            Assert.True(result.Success);
            Assert.Equal(1, plan.TotalSelected);
            Assert.NotNull(_removalEngine.LastPlan);
            var finalInstallWimPath = Path.Combine(result.Workspace!.WorkspacePath, "sources", "install.wim");
            Assert.Equal(File.ReadAllText(_modifiedInstallWim), File.ReadAllText(finalInstallWimPath));
        }
        finally
        {
            File.SetAttributes(mountedInstallWim, FileAttributes.Normal);
        }
    }

    [Fact]
    public async Task The_mounted_ISO_install_wim_is_never_written_to_during_the_final_substitution()
    {
        // P27: el original (dentro de la ISO montada) nunca se toca -- solo la
        // copia del workspace de generación. Se confirma con el contenido Y con
        // que el atributo ReadOnly del "original" sigue intacto tras el run.
        var mountedInstallWim = Path.Combine(_mountedIsoRoot, "sources", "install.wim");
        var originalContent = File.ReadAllText(mountedInstallWim);
        File.SetAttributes(mountedInstallWim, File.GetAttributes(mountedInstallWim) | FileAttributes.ReadOnly);
        try
        {
            var pipeline = NewPipeline();
            var request = ValidRequest() with { RemovalPlan = new RemovalPlan() };

            await pipeline.GenerateAsync(request);

            Assert.Equal(originalContent, File.ReadAllText(mountedInstallWim));
            Assert.True((File.GetAttributes(mountedInstallWim) & FileAttributes.ReadOnly) != 0,
                "El ReadOnly del install.wim 'original' (ISO montada) nunca debe quitarse.");
        }
        finally
        {
            File.SetAttributes(mountedInstallWim, FileAttributes.Normal);
        }
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
    public async Task InstallationOptions_are_forwarded_to_the_boot_wim_stage_unmodified()
    {
        // P24, item 6: InstallationOptions se transmite tal cual (cuenta local,
        // OOBE offline, bypasses) -- nadie en el camino UI -> pipeline lo altera.
        var pipeline = NewPipeline();
        var options = InstallationOptionsModel.Default with
        {
            BypassStorage = false,
            AllowLocalAccount = false,
            BypassRam = false,
        };
        var request = ValidRequest() with { InstallationOptions = options };

        var result = await pipeline.GenerateAsync(request);

        Assert.True(result.Success);
        Assert.Equal(options, _installationImageService.LastOptions);
    }

    [Fact]
    public async Task AllowOfflineOobe_false_never_calls_the_install_wim_OOBE_configurator()
    {
        var pipeline = NewPipeline();
        var request = ValidRequest() with { InstallationOptions = InstallationOptionsModel.Default with { BypassStorage = false, AllowOfflineOobe = false } };

        var result = await pipeline.GenerateAsync(request);

        Assert.True(result.Success);
        Assert.Equal(0, _installWimOobeConfigurator.CallCount);
    }

    [Fact]
    public async Task AllowOfflineOobe_true_configures_the_same_working_image_that_RemovalEngine_already_used()
    {
        // P29: "trabajar únicamente sobre la working image que ya utiliza el
        // pipeline" -- nunca una copia distinta ni la ISO original.
        var pipeline = NewPipeline();

        await pipeline.GenerateAsync(ValidRequest());

        Assert.NotNull(_installWimOobeConfigurator.LastCall);
        Assert.Equal(_workingImageFactory.WorkingWimPathToReturn, _installWimOobeConfigurator.LastCall!.Value.InstallWimPath);
    }

    [Fact]
    public async Task A_failed_install_wim_OOBE_configuration_aborts_before_oscdimg()
    {
        _installWimOobeConfigurator.Success = false;
        _installWimOobeConfigurator.Errors = new[] { "BypassNRO no se pudo verificar en install.wim" };
        var pipeline = NewPipeline();

        var result = await pipeline.GenerateAsync(ValidRequest());

        Assert.False(result.Success);
        Assert.Contains("BypassNRO no se pudo verificar en install.wim", result.Errors);
        Assert.Equal(0, _oscdimgRunner.CallCount);
    }

    [Fact]
    public async Task Storage_bypass_is_rejected_before_creating_any_workspace()
    {
        // P24, item 5: si StorageBypass está activado, el pipeline rechaza la
        // generación de forma segura (P15/P16), reutilizando la validación ya
        // existente -- no crea ningún workspace ni copia nada.
        var pipeline = NewPipeline();
        var request = ValidRequest() with
        {
            InstallationOptions = InstallationOptionsModel.Default with { BypassStorage = true },
        };

        var result = await pipeline.GenerateAsync(request);

        Assert.False(result.Success);
        Assert.Null(result.Workspace);
        Assert.Equal(0, _treeCopier.CallCount);
    }

    [Fact]
    public async Task A_cancellation_during_the_ISO_tree_copy_propagates_instead_of_reporting_false_success()
    {
        // P24, item 9: una cancelación nunca debe traducirse en un resultado con
        // Success=true -- el pipeline deja que OperationCanceledException se
        // propague (no la atrapa junto al resto de excepciones inesperadas).
        _treeCopier.OnCopy = (_, _) => throw new OperationCanceledException();
        var pipeline = NewPipeline();

        await Assert.ThrowsAsync<OperationCanceledException>(() => pipeline.GenerateAsync(ValidRequest()));

        Assert.Equal(0, _oscdimgRunner.CallCount);
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
