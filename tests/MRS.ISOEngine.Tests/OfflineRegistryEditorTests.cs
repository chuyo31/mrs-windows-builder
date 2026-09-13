using MRS.ISOEngine.Registry;
using MRS.ISOEngine.Tests.Fakes;
using Xunit;

namespace MRS.ISOEngine.Tests;

/// <summary>
/// P16, sección 4: <see cref="OfflineRegistryEditor"/> traduce cada operación al
/// comando <c>reg.exe</c> exacto, sin decidir éxito/fallo por el texto de la
/// salida (solo por <c>ExitCode</c>, verificado en <see cref="FakeProcessRunner"/>).
/// </summary>
public sealed class OfflineRegistryEditorTests
{
    private readonly FakeProcessRunner _process = new();
    private readonly OfflineRegistryEditor _editor;

    public OfflineRegistryEditorTests() => _editor = new OfflineRegistryEditor(_process, regPath: "reg.exe");

    [Fact]
    public async Task LoadHiveAsync_runs_reg_load_with_the_hive_key_and_file()
    {
        await _editor.LoadHiveAsync("MRS_TEST", @"C:\mount\Windows\System32\config\SYSTEM");

        var (fileName, arguments) = _process.Calls.Single();
        Assert.Equal("reg.exe", fileName);
        Assert.Equal(@"load HKLM\MRS_TEST ""C:\mount\Windows\System32\config\SYSTEM""", arguments);
    }

    [Fact]
    public async Task SetDwordAsync_runs_reg_add_with_REG_DWORD_and_force_overwrite()
    {
        await _editor.SetDwordAsync("MRS_TEST", "Setup\\LabConfig", "BypassTPMCheck", 1);

        var (_, arguments) = _process.Calls.Single();
        Assert.Equal(@"add ""HKLM\MRS_TEST\Setup\LabConfig"" /v BypassTPMCheck /t REG_DWORD /d 1 /f", arguments);
    }

    [Fact]
    public async Task DeleteValueAsync_runs_reg_delete_with_force()
    {
        await _editor.DeleteValueAsync("MRS_TEST", "Setup\\LabConfig", "BypassTPMCheck");

        var (_, arguments) = _process.Calls.Single();
        Assert.Equal(@"delete ""HKLM\MRS_TEST\Setup\LabConfig"" /v BypassTPMCheck /f", arguments);
    }

    [Fact]
    public async Task QueryValueAsync_runs_reg_query()
    {
        await _editor.QueryValueAsync("MRS_TEST", "Setup\\LabConfig", "BypassTPMCheck");

        var (_, arguments) = _process.Calls.Single();
        Assert.Equal(@"query ""HKLM\MRS_TEST\Setup\LabConfig"" /v BypassTPMCheck", arguments);
    }

    [Fact]
    public async Task UnloadHiveAsync_runs_reg_unload()
    {
        await _editor.UnloadHiveAsync("MRS_TEST");

        var (_, arguments) = _process.Calls.Single();
        Assert.Equal(@"unload HKLM\MRS_TEST", arguments);
    }

    [Fact]
    public async Task Success_is_decided_only_by_ExitCode_never_by_output_text()
    {
        _process.ExitCode = 1; // simula un fallo real de reg.exe

        var result = await _editor.SetDwordAsync("MRS_TEST", "Setup\\LabConfig", "BypassTPMCheck", 1);

        Assert.False(result.Succeeded);
        Assert.Equal(1, result.ExitCode);
    }
}
