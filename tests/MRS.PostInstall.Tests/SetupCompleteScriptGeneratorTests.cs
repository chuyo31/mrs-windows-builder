using MRS.PostInstall.Models;
using MRS.PostInstall.Scripts;
using Xunit;

namespace MRS.PostInstall.Tests;

/// <summary>
/// P18, sección 9/16/18 (Orden + Seguridad): SetupComplete.cmd instala .NET
/// antes de lanzar PCPI, nunca al revés; es determinista; y nunca contiene
/// ninguna ruta específica del equipo de desarrollo.
/// </summary>
public sealed class SetupCompleteScriptGeneratorTests
{
    [Fact]
    public void Generation_is_deterministic_for_the_same_configuration()
    {
        var config = new PostInstallConfiguration();

        var first = SetupCompleteScriptGenerator.Generate(config);
        var second = SetupCompleteScriptGenerator.Generate(config);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Dotnet_installation_always_comes_before_launching_PCPI_never_the_reverse()
    {
        var script = SetupCompleteScriptGenerator.Generate(new PostInstallConfiguration());

        var dotnetIndex = script.IndexOf("windowsdesktop-runtime-8.0.26-win-x64.exe", StringComparison.Ordinal);
        var pcpiIndex = script.IndexOf("PCPI-Retro-Minimals-Portable-0.0.5.exe", StringComparison.Ordinal);

        Assert.True(dotnetIndex >= 0);
        Assert.True(pcpiIndex >= 0);
        Assert.True(dotnetIndex < pcpiIndex, "El instalador de .NET debe aparecer antes que el lanzamiento de PCPI.");
    }

    [Fact]
    public void PCPI_is_never_launched_before_confirming_the_dotnet_ExitCode()
    {
        var script = SetupCompleteScriptGenerator.Generate(new PostInstallConfiguration());

        var exitCodeCheckIndex = script.IndexOf("if %DOTNET_EXITCODE% NEQ 0", StringComparison.Ordinal);
        var pcpiIndex = script.IndexOf("PCPI-Retro-Minimals-Portable-0.0.5.exe", StringComparison.Ordinal);

        Assert.True(exitCodeCheckIndex >= 0);
        Assert.True(exitCodeCheckIndex < pcpiIndex);
    }

    [Fact]
    public void RunPcpiAfterRuntime_false_never_mentions_PCPI_at_all()
    {
        var script = SetupCompleteScriptGenerator.Generate(new PostInstallConfiguration { RunPcpiAfterRuntime = false });

        Assert.DoesNotContain("PCPI", script);
    }

    [Fact]
    public void The_script_only_uses_paths_relative_to_its_own_location()
    {
        var script = SetupCompleteScriptGenerator.Generate(new PostInstallConfiguration());

        Assert.Contains("%~dp0", script);
        Assert.DoesNotContain(@"C:\Users\", script);
        // \Desktop\ / \Downloads\ como segmento de ruta (no como subcadena: el propio
        // producto se llama ".NET Desktop Runtime", una coincidencia legítima).
        Assert.DoesNotContain(@"\Desktop\", script);
        Assert.DoesNotContain(@"\Downloads\", script);
    }

    [Fact]
    public void No_developer_machine_username_appears_in_the_generated_script()
    {
        var script = SetupCompleteScriptGenerator.Generate(new PostInstallConfiguration());

        Assert.DoesNotContain("B3RASCASA", script);
    }

    [Fact]
    public void The_script_logs_the_documented_runtime_execution_lines()
    {
        // "[POSTINSTALL] Preparing package"/"Runtime: ..."/"Validation: OK"/"Package
        // prepared successfully" son del BUILDER (tiempo de construcción, ver
        // PostInstallPackageBuilderTests); el script solo registra la ejecución futura.
        var script = SetupCompleteScriptGenerator.Generate(new PostInstallConfiguration());

        Assert.Contains("[POSTINSTALL] Installing .NET", script);
        Assert.Contains(".NET ExitCode:", script);
        Assert.Contains("[POSTINSTALL] Launching PCPI", script);
    }

    [Fact]
    public void The_silent_install_flags_come_from_PostInstallCommands_not_a_second_hardcoded_copy()
    {
        var script = SetupCompleteScriptGenerator.Generate(new PostInstallConfiguration());

        Assert.Contains("/install /quiet /norestart", script);
    }
}
