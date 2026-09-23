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
        // P25: ValidateBeforeMountAsync exige un tamaño mínimo de cordura antes
        // de Mount-Wim; se repite el contenido para superarlo sin dejar de ser
        // legible como texto (se compara con File.ReadAllText más abajo).
        File.WriteAllText(Path.Combine(_mountedIsoRoot, "sources", "boot.wim"),
            string.Concat(Enumerable.Repeat("fake boot.wim original ", 300)));

        var workspaceRoot = Path.Combine(_dir, "workspace");
        Directory.CreateDirectory(Path.Combine(workspaceRoot, "sources"));
        Directory.CreateDirectory(Path.Combine(workspaceRoot, "mount"));

        // P29: ValidateFinalAsync también revalida install.wim cuando
        // AllowOfflineOobe está activo; este proyecto de tests no ejercita el
        // pipeline completo (que sí lo crearía vía WorkingImageFactory/
        // RemovalEngine), así que se deja un placeholder igual de grande que el
        // de boot.wim para que ValidateBeforeMountAsync no lo rechace por tamaño.
        File.WriteAllText(Path.Combine(workspaceRoot, "sources", "install.wim"),
            string.Concat(Enumerable.Repeat("fake install.wim de trabajo ", 300)));

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
        // P28: se pasa siempre _dism/_registry (sobrecarga nueva) para que
        // ValidateFinalAsync esté disponible en todos los tests, no solo en los
        // que la ejercitan explícitamente -- el comportamiento de ApplyAsync no
        // cambia por recibir estos dos parámetros adicionales.
        => new(new BootWimProvisioner(new FakeIsoMounter(_mountedIsoRoot)), new BootWimModifier(_dism, _registry), _dism, _registry);

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
    public async Task A_boot_wim_already_placed_in_the_workspace_by_IsoTreeCopier_with_ReadOnly_still_mounts_successfully()
    {
        // P26: reproduce el bug real de producción. IsoGenerationPipeline ejecuta
        // IsoTreeCopier (copia el árbol COMPLETO de la ISO, incluido
        // sources\boot.wim) ANTES de invocar InstallationImageService -- así que
        // en el flujo real, boot.wim casi siempre YA EXISTE en el workspace
        // cuando BootWimProvisioner.EnsureBootWimCopyAsync se ejecuta, y entra por
        // la rama idempotente ("no se vuelve a copiar"). File.Copy conserva
        // ReadOnly del origen montado en solo lectura, así que ese archivo
        // pre-copiado llega ReadOnly. P25 solo preparaba (quitaba ReadOnly) la
        // rama de "copio yo mismo" -- este test simula exactamente lo que hace
        // IsoTreeCopier antes de llamar a ApplyAsync, y habría fallado con P25.
        Directory.CreateDirectory(Path.GetDirectoryName(_workspace.BootWimPath)!);
        File.Copy(Path.Combine(_mountedIsoRoot, "sources", "boot.wim"), _workspace.BootWimPath);
        File.SetAttributes(_workspace.BootWimPath, File.GetAttributes(_workspace.BootWimPath) | FileAttributes.ReadOnly);

        var service = NewService();

        var result = await service.ApplyAsync("fake-source.iso", _workspace, SafeOptions(), Account());

        Assert.True(result.Success);
        Assert.Contains(_dism.Calls, c => c.StartsWith("mount:"));
        Assert.False((File.GetAttributes(_workspace.BootWimPath) & FileAttributes.ReadOnly) != 0);
    }

    [Fact]
    public async Task ApplyAsync_applies_LabConfig_to_both_boot_wim_index_1_and_index_2()
    {
        // P28: auditoría real confirmó índice 1 = WinPE, índice 2 = Windows
        // Setup -- el bypass de hardware solo surte efecto real si se aplica
        // también al índice 2, que es donde Setup ejecuta la comprobación.
        var service = NewService();

        var result = await service.ApplyAsync("fake-source.iso", _workspace, SafeOptions(), Account());

        Assert.True(result.Success);
        Assert.Contains(_dism.Calls, c => c.StartsWith("mount:") && c.Contains(":1:"));
        Assert.Contains(_dism.Calls, c => c.StartsWith("mount:") && c.Contains(":2:"));
    }

    [Fact]
    public async Task AllowOfflineOobe_true_applies_BypassNRO_only_on_boot_wim_index_2()
    {
        var service = NewService();
        var options = SafeOptions() with { AllowOfflineOobe = true };

        var result = await service.ApplyAsync("fake-source.iso", _workspace, options, Account());

        Assert.True(result.Success);
        Assert.Contains(result.AppliedLogLines, l => l.Contains("BypassNRO"));

        // El hive de cada índice usa una clave temporal distinta (GUID), así que
        // se comprueba indirectamente: solo debe haber UNA operación "set" para
        // BypassNRO en total (no una por índice).
        var bypassNroSets = _registry.Calls.Count(c => c.StartsWith("set:") && c.Contains("BypassNRO"));
        Assert.Equal(1, bypassNroSets);
    }

    [Fact]
    public async Task AllowOfflineOobe_false_never_applies_BypassNRO()
    {
        var service = NewService();
        var options = SafeOptions() with { AllowOfflineOobe = false };

        var result = await service.ApplyAsync("fake-source.iso", _workspace, options, Account());

        Assert.True(result.Success);
        Assert.DoesNotContain(result.AppliedLogLines, l => l.Contains("BypassNRO"));
        Assert.DoesNotContain(_registry.Calls, c => c.Contains("BypassNRO"));
    }

    [Fact]
    public async Task ValidateFinalAsync_succeeds_after_a_successful_ApplyAsync()
    {
        // P28: re-verifica de forma independiente, volviendo a montar de solo
        // lectura -- no se limita a devolver lo que ApplyAsync ya reportó.
        var service = NewService();
        var options = SafeOptions() with { AllowOfflineOobe = true };
        var applyResult = await service.ApplyAsync("fake-source.iso", _workspace, options, Account());
        Assert.True(applyResult.Success);

        // Este proyecto de tests solo ejercita InstallationImageService
        // (boot.wim/autounattend); el paso que aplica BypassNRO a install.wim
        // (InstallWimOobeConfigurator, P29) vive en el pipeline y se prueba por
        // separado -- aquí se simula que ya se aplicó, para poder confirmar que
        // ValidateFinalAsync sabe leerlo de vuelta.
        await _registry.SetDwordAsync("MRS_TEST_PRESET", "Setup\\OOBE", "BypassNRO", 1);

        var validation = await service.ValidateFinalAsync(_workspace, options, Account());

        Assert.True(validation.IsValid, string.Join("; ", validation.Errors));
        // Confirma que sí volvió a montar (no solo reusó el resultado de ApplyAsync):
        // dos montajes de solo lectura adicionales, uno por índice.
        Assert.Contains(_dism.Calls, c => c.StartsWith("mount:") && c.Contains(":1:") && c.EndsWith("ro=True"));
        Assert.Contains(_dism.Calls, c => c.StartsWith("mount:") && c.Contains(":2:") && c.EndsWith("ro=True"));
    }

    [Fact]
    public async Task ValidateFinalAsync_fails_if_a_disabled_bypass_key_is_still_present_on_re_read()
    {
        // Simula una discrepancia entre lo aplicado y lo que de verdad quedó en
        // boot.wim: ValidateFinalAsync debe detectarla releyendo, no confiando
        // en el resultado ya reportado por ApplyAsync.
        var service = NewService();
        var applyOptions = SafeOptions() with { BypassCpu = true };
        await service.ApplyAsync("fake-source.iso", _workspace, applyOptions, Account());

        var differentOptions = applyOptions with { BypassCpu = false };
        var validation = await service.ValidateFinalAsync(_workspace, differentOptions, Account());

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, e => e.Contains("BypassCPUCheck"));
    }

    [Fact]
    public async Task ValidateFinalAsync_without_a_wired_IDismRunner_returns_valid_without_checking_anything()
    {
        // Documentado explícitamente: sin IDismRunner/IOfflineRegistryEditor
        // (constructor de 3 argumentos, compatibilidad con P16-P24), la
        // validación final se omite en vez de lanzar.
        var legacyService = new InstallationImageService(
            new BootWimProvisioner(new FakeIsoMounter(_mountedIsoRoot)), new BootWimModifier(_dism, _registry));

        var validation = await legacyService.ValidateFinalAsync(_workspace, SafeOptions(), Account());

        Assert.True(validation.IsValid);
    }

    [Fact]
    public async Task ValidateFinalAsync_fails_if_the_configured_account_name_does_not_match_what_was_generated()
    {
        // P30: ValidateAutounattend ahora compara el <Name> real del XML con el
        // nombre configurado, no solo comprueba que "existe alguna cuenta".
        var service = NewService();
        var applyResult = await service.ApplyAsync("fake-source.iso", _workspace, SafeOptions(), Account() with { AccountName = "Carlos" });
        Assert.True(applyResult.Success);

        var validation = await service.ValidateFinalAsync(_workspace, SafeOptions(), Account() with { AccountName = "OtroNombre" });

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, e => e.Contains("Carlos") || e.Contains("OtroNombre"));
    }

    [Fact]
    public async Task ValidateFinalAsync_succeeds_and_logs_password_configured_when_a_password_was_set()
    {
        // P30: nunca se registra el valor real de la contraseña, solo si está
        // "configured"/"empty". Se usa un valor de prueba explícito (nunca un
        // valor por defecto de producto) solo para este test de laboratorio.
        var service = NewService();
        var account = Account() with { AccountName = "Carlos", Password = "1234", ConfirmPassword = "1234" };
        var applyResult = await service.ApplyAsync("fake-source.iso", _workspace, SafeOptions(), account);
        Assert.True(applyResult.Success);

        var validation = await service.ValidateFinalAsync(_workspace, SafeOptions(), account);

        Assert.True(validation.IsValid, string.Join("; ", validation.Errors));
    }

    [Fact]
    public async Task ValidateFinalAsync_fails_if_expected_password_state_does_not_match_what_was_generated()
    {
        // El autounattend se generó SIN contraseña, pero se revalida esperando
        // que SÍ hubiera una -- debe detectarse la discrepancia.
        var service = NewService();
        var applyResult = await service.ApplyAsync("fake-source.iso", _workspace, SafeOptions(), Account() with { AccountName = "Carlos", Password = null, ConfirmPassword = null });
        Assert.True(applyResult.Success);

        var validation = await service.ValidateFinalAsync(_workspace, SafeOptions(), Account() with { AccountName = "Carlos", Password = "1234", ConfirmPassword = "1234" });

        Assert.False(validation.IsValid);
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
