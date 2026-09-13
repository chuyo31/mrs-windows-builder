using System.Xml.Linq;
using MRS.ISOEngine.Autounattend;
using MRS.ISOEngine.Exceptions;
using MRS.ISOEngine.Models;
using Xunit;

namespace MRS.ISOEngine.Tests;

/// <summary>P16, sección 5/16 (Autounattend): XML válido, cuenta local, sin credenciales hardcoded, generación determinista.</summary>
public sealed class AutounattendGeneratorTests
{
    private static AutounattendConfiguration DefaultConfig() => new() { AccountName = "Usuario", ComputerName = "MRS-PC" };

    [Fact]
    public void Generate_produces_well_formed_XML()
    {
        var xml = AutounattendGenerator.Generate(DefaultConfig(), allowOfflineOobe: true);

        // Si el XML no fuese válido, XDocument.Parse lanzaría.
        var document = XDocument.Parse(xml);
        Assert.Equal("unattend", document.Root!.Name.LocalName);
    }

    [Fact]
    public void Generate_includes_a_local_account_with_the_configured_name()
    {
        var xml = AutounattendGenerator.Generate(DefaultConfig(), allowOfflineOobe: true);

        Assert.Contains("<Name>Usuario</Name>", xml);
        Assert.Contains("Administrators", xml);
    }

    [Fact]
    public void Generate_never_contains_a_hardcoded_password_when_none_is_provided()
    {
        var xml = AutounattendGenerator.Generate(DefaultConfig() with { Password = null }, allowOfflineOobe: true);

        Assert.DoesNotContain("<Password>", xml);
        Assert.DoesNotContain("PlainText", xml);
    }

    [Fact]
    public void Generate_includes_the_password_only_when_one_is_explicitly_provided_at_runtime()
    {
        // El valor viene del llamador en tiempo de ejecución, nunca de una constante del código.
        var runtimePassword = "P@ss-" + Guid.NewGuid().ToString("N")[..8];

        var xml = AutounattendGenerator.Generate(DefaultConfig() with { Password = runtimePassword }, allowOfflineOobe: true);

        Assert.Contains(runtimePassword, xml);
    }

    [Fact]
    public void No_source_file_in_this_project_contains_a_literal_password_constant()
    {
        // Comprueba, sobre el propio código fuente, que no hay ninguna contraseña
        // hardcodeada en el repositorio (sección 5: "no incluir credenciales del
        // usuario en el repositorio").
        var repoRoot = FindRepoRoot();
        var sourceFiles = Directory.EnumerateFiles(Path.Combine(repoRoot, "src", "MRS.ISOEngine"), "*.cs", SearchOption.AllDirectories);

        foreach (var file in sourceFiles)
        {
            var content = File.ReadAllText(file);
            Assert.DoesNotContain("Password = \"", content); // ninguna asignación literal de contraseña
        }
    }

    [Fact]
    public void Generation_is_deterministic_for_the_same_configuration()
    {
        var config = DefaultConfig();

        var first = AutounattendGenerator.Generate(config, allowOfflineOobe: true);
        var second = AutounattendGenerator.Generate(config, allowOfflineOobe: true);

        Assert.Equal(first, second);
    }

    [Fact]
    public void AllowOfflineOobe_true_hides_wireless_setup_and_sets_network_location_home()
    {
        var xml = AutounattendGenerator.Generate(DefaultConfig(), allowOfflineOobe: true);

        Assert.Contains("<HideWirelessSetupInOOBE>true</HideWirelessSetupInOOBE>", xml);
        Assert.Contains("<NetworkLocation>Home</NetworkLocation>", xml);
    }

    [Fact]
    public void AllowOfflineOobe_false_does_not_force_hiding_wireless_setup()
    {
        var xml = AutounattendGenerator.Generate(DefaultConfig(), allowOfflineOobe: false);

        Assert.Contains("<HideWirelessSetupInOOBE>false</HideWirelessSetupInOOBE>", xml);
        Assert.DoesNotContain("<NetworkLocation>", xml);
    }

    [Fact]
    public void Validate_rejects_an_empty_account_name()
    {
        var result = AutounattendGenerator.Validate(DefaultConfig() with { AccountName = "" });

        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void Validate_rejects_account_names_with_invalid_characters()
    {
        var result = AutounattendGenerator.Validate(DefaultConfig() with { AccountName = "Usuario:1" });

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_accepts_the_default_configuration()
    {
        var result = AutounattendGenerator.Validate(DefaultConfig());

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Generate_throws_a_controlled_exception_for_an_invalid_configuration()
    {
        Assert.Throws<IsoEngineException>(() => AutounattendGenerator.Generate(DefaultConfig() with { AccountName = "" }, allowOfflineOobe: true));
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "MRS-Windows-Builder.sln")))
            dir = dir.Parent;

        return dir?.FullName ?? throw new InvalidOperationException("No se encontró la raíz del repositorio.");
    }
}
