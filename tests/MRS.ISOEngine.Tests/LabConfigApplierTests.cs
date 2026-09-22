using MRS.ISOEngine.Configuration;
using MRS.ISOEngine.Tests.Fakes;
using Xunit;
using InstallationOptionsModel = MRS.InstallationOptions.Models.InstallationOptions;

namespace MRS.ISOEngine.Tests;

/// <summary>
/// P16, sección 3/16 (LabConfig): aplica únicamente los bypasses activados, no
/// inventa claves adicionales, retira los desactivados (idempotencia), y solo
/// registra en el log las opciones realmente aplicadas.
/// </summary>
public sealed class LabConfigApplierTests
{
    private const string HiveKey = "MRS_TEST_SYSTEM";
    private readonly FakeOfflineRegistryEditor _registry = new();
    private readonly LabConfigApplier _applier;

    public LabConfigApplierTests() => _applier = new LabConfigApplier(_registry);

    [Fact]
    public async Task TPM_enabled_sets_BypassTPMCheck_to_1()
    {
        await _applier.ApplyAsync(HiveKey, InstallationOptionsModel.Default with
        {
            BypassSecureBoot = false, BypassCpu = false, BypassRam = false,
        });

        Assert.Equal(1, _registry.GetValue(HiveKey, "Setup\\LabConfig", "BypassTPMCheck"));
    }

    [Fact]
    public async Task TPM_disabled_never_creates_the_key()
    {
        await _applier.ApplyAsync(HiveKey, new InstallationOptionsModel { BypassTpm = false, BypassSecureBoot = false, BypassCpu = false, BypassRam = false });

        Assert.Null(_registry.GetValue(HiveKey, "Setup\\LabConfig", "BypassTPMCheck"));
    }

    [Theory]
    [InlineData(true, "BypassSecureBootCheck")]
    [InlineData(false, "BypassSecureBootCheck")]
    public async Task SecureBoot_reflects_the_option_exactly(bool enabled, string valueName)
    {
        var options = new InstallationOptionsModel { BypassTpm = false, BypassSecureBoot = enabled, BypassCpu = false, BypassRam = false };

        await _applier.ApplyAsync(HiveKey, options);

        Assert.Equal(enabled ? 1 : (int?)null, _registry.GetValue(HiveKey, "Setup\\LabConfig", valueName));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CPU_reflects_the_option_exactly(bool enabled)
    {
        var options = new InstallationOptionsModel { BypassTpm = false, BypassSecureBoot = false, BypassCpu = enabled, BypassRam = false };

        await _applier.ApplyAsync(HiveKey, options);

        Assert.Equal(enabled ? 1 : (int?)null, _registry.GetValue(HiveKey, "Setup\\LabConfig", "BypassCPUCheck"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RAM_reflects_the_option_exactly(bool enabled)
    {
        var options = new InstallationOptionsModel { BypassTpm = false, BypassSecureBoot = false, BypassCpu = false, BypassRam = enabled };

        await _applier.ApplyAsync(HiveKey, options);

        Assert.Equal(enabled ? 1 : (int?)null, _registry.GetValue(HiveKey, "Setup\\LabConfig", "BypassRAMCheck"));
    }

    [Fact]
    public async Task All_four_enabled_sets_exactly_those_four_keys_no_more_no_less()
    {
        await _applier.ApplyAsync(HiveKey, InstallationOptionsModel.Default);

        Assert.Equal(1, _registry.GetValue(HiveKey, "Setup\\LabConfig", "BypassTPMCheck"));
        Assert.Equal(1, _registry.GetValue(HiveKey, "Setup\\LabConfig", "BypassSecureBootCheck"));
        Assert.Equal(1, _registry.GetValue(HiveKey, "Setup\\LabConfig", "BypassCPUCheck"));
        Assert.Equal(1, _registry.GetValue(HiveKey, "Setup\\LabConfig", "BypassRAMCheck"));
        // No inventa ninguna clave de almacenamiento ni de OOBE: nunca se llama fuera de ApplyAsync.
        Assert.Null(_registry.GetValue(HiveKey, "Setup\\OOBE", "BypassNRO"));
    }

    [Fact]
    public async Task Combination_TPM_and_RAM_only_leaves_exactly_those_two_keys()
    {
        var options = new InstallationOptionsModel { BypassTpm = true, BypassSecureBoot = false, BypassCpu = false, BypassRam = true };

        await _applier.ApplyAsync(HiveKey, options);

        Assert.Equal(1, _registry.GetValue(HiveKey, "Setup\\LabConfig", "BypassTPMCheck"));
        Assert.Equal(1, _registry.GetValue(HiveKey, "Setup\\LabConfig", "BypassRAMCheck"));
        Assert.Null(_registry.GetValue(HiveKey, "Setup\\LabConfig", "BypassSecureBootCheck"));
        Assert.Null(_registry.GetValue(HiveKey, "Setup\\LabConfig", "BypassCPUCheck"));
    }

    [Fact]
    public async Task Only_the_options_actually_applied_are_returned_as_log_lines()
    {
        var options = new InstallationOptionsModel { BypassTpm = true, BypassSecureBoot = false, BypassCpu = true, BypassRam = false };

        var applied = await _applier.ApplyAsync(HiveKey, options);

        Assert.Equal(new[] { "[COMPAT] BypassTPM applied", "[COMPAT] BypassCPU applied" }, applied);
    }

    [Fact]
    public async Task Applying_twice_with_the_same_options_is_idempotent()
    {
        await _applier.ApplyAsync(HiveKey, InstallationOptionsModel.Default);
        await _applier.ApplyAsync(HiveKey, InstallationOptionsModel.Default);

        Assert.Equal(1, _registry.GetValue(HiveKey, "Setup\\LabConfig", "BypassTPMCheck"));
    }

    [Fact]
    public async Task Applying_with_an_option_now_disabled_removes_the_previously_set_key()
    {
        await _applier.ApplyAsync(HiveKey, InstallationOptionsModel.Default); // BypassTpm=true la primera vez

        await _applier.ApplyAsync(HiveKey, InstallationOptionsModel.Default with { BypassTpm = false });

        Assert.Null(_registry.GetValue(HiveKey, "Setup\\LabConfig", "BypassTPMCheck"));
    }

    [Fact]
    public async Task VerifyAsync_succeeds_when_the_registry_matches_the_options()
    {
        var options = InstallationOptionsModel.Default;
        await _applier.ApplyAsync(HiveKey, options);

        var result = await _applier.VerifyAsync(HiveKey, options);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task VerifyAsync_fails_if_a_disabled_option_key_is_still_present()
    {
        var options = InstallationOptionsModel.Default;
        await _applier.ApplyAsync(HiveKey, options);

        // Simula que, tras aplicar, alguien puso manualmente una clave que no debería estar.
        var afterDisabling = options with { BypassCpu = false };

        var result = await _applier.VerifyAsync(HiveKey, afterDisabling);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("BypassCPUCheck"));
    }

    [Fact]
    public async Task ApplyOfflineOobeBypassAsync_is_never_called_by_ApplyAsync()
    {
        await _applier.ApplyAsync(HiveKey, InstallationOptionsModel.Default);

        Assert.Null(_registry.GetValue(HiveKey, "Setup\\OOBE", "BypassNRO"));
    }

    [Fact]
    public async Task ApplyOfflineOobeBypassAsync_sets_BypassNRO_to_1()
    {
        var applied = await _applier.ApplyOfflineOobeBypassAsync(HiveKey);

        Assert.True(applied);
        Assert.Equal(1, _registry.GetValue(HiveKey, "Setup\\OOBE", "BypassNRO"));
    }

    [Fact]
    public async Task VerifyOfflineOobeBypassAsync_succeeds_after_ApplyOfflineOobeBypassAsync()
    {
        await _applier.ApplyOfflineOobeBypassAsync(HiveKey);

        var result = await _applier.VerifyOfflineOobeBypassAsync(HiveKey);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task VerifyOfflineOobeBypassAsync_fails_when_BypassNRO_was_never_applied()
    {
        // P28: nunca se acepta como éxito que DISM/reg.exe terminaran sin error --
        // se confirma leyendo el valor de vuelta.
        var result = await _applier.VerifyOfflineOobeBypassAsync(HiveKey);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("BypassNRO"));
    }
}
