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
    public void Generate_writes_an_empty_Password_Value_when_none_is_provided()
    {
        // P30: se descubrió (prueba real de P29) que OMITIR <Password> por
        // completo hace que Windows exija cambiar la contraseña en el primer
        // inicio de sesión pese a haberse pedido una cuenta sin contraseña.
        // El elemento debe estar SIEMPRE presente, con <Value></Value> vacío
        // cuando no se proporcionó ninguna -- nunca un valor por defecto no vacío.
        var xml = AutounattendGenerator.Generate(DefaultConfig() with { Password = null, ConfirmPassword = null }, allowOfflineOobe: true);

        Assert.Contains("<Password>", xml);
        Assert.Contains("<Value></Value>", xml);
        Assert.Contains("<PlainText>true</PlainText>", xml);
    }

    [Fact]
    public void Generate_includes_the_password_only_when_one_is_explicitly_provided_at_runtime()
    {
        // El valor viene del llamador en tiempo de ejecución, nunca de una constante del código.
        var runtimePassword = "P@ss-" + Guid.NewGuid().ToString("N")[..8];

        var xml = AutounattendGenerator.Generate(DefaultConfig() with { Password = runtimePassword, ConfirmPassword = runtimePassword }, allowOfflineOobe: true);

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
    public void AllowOfflineOobe_true_adds_a_specialize_pass_with_the_BypassNRO_command()
    {
        // P28: HideOnlineAccountScreens/HideWirelessSetupInOOBE (oobeSystem) no
        // evitan la pantalla "Vamos a conectarte a una red" -- esa depende de
        // HKLM\SYSTEM\Setup\OOBE\BypassNRO en el sistema YA INSTALADO, que solo
        // existe a partir del paso specialize (después de aplicar install.wim).
        var xml = AutounattendGenerator.Generate(DefaultConfig(), allowOfflineOobe: true);
        var document = XDocument.Parse(xml);
        XNamespace ns = "urn:schemas-microsoft-com:unattend";

        var specializeSettings = document.Root!.Elements(ns + "settings")
            .SingleOrDefault(e => (string?)e.Attribute("pass") == "specialize");

        Assert.NotNull(specializeSettings);
        var path = specializeSettings!.Descendants(ns + "Path").Select(e => e.Value).SingleOrDefault();
        Assert.NotNull(path);
        Assert.Contains("BypassNRO", path);
        Assert.Contains(@"HKLM\SYSTEM\Setup\OOBE", path);
        Assert.Contains("Microsoft-Windows-Deployment", xml);
    }

    [Fact]
    public void AllowOfflineOobe_false_never_adds_a_specialize_pass()
    {
        var xml = AutounattendGenerator.Generate(DefaultConfig(), allowOfflineOobe: false);
        var document = XDocument.Parse(xml);
        XNamespace ns = "urn:schemas-microsoft-com:unattend";

        var specializeSettings = document.Root!.Elements(ns + "settings")
            .Any(e => (string?)e.Attribute("pass") == "specialize");

        Assert.False(specializeSettings);
        Assert.DoesNotContain("BypassNRO", xml);
    }

    [Fact]
    public void Generate_always_adds_a_valid_windowsPE_pass_with_AcceptEula()
    {
        // P29, sección 2: reemplaza la decisión de P28 (P28 determinó que no
        // hacía falta windowsPE; la prueba real demostró que sí hacía falta
        // para que Setup tratase el resto del archivo como autoritativo desde
        // el arranque). Presente siempre, no solo cuando AllowOfflineOobe.
        foreach (var allowOfflineOobe in new[] { true, false })
        {
            var xml = AutounattendGenerator.Generate(DefaultConfig(), allowOfflineOobe);
            var document = XDocument.Parse(xml);
            XNamespace ns = "urn:schemas-microsoft-com:unattend";

            var windowsPeSettings = document.Root!.Elements(ns + "settings")
                .SingleOrDefault(e => (string?)e.Attribute("pass") == "windowsPE");

            Assert.NotNull(windowsPeSettings);
            var acceptEula = windowsPeSettings!.Descendants(ns + "AcceptEula").SingleOrDefault()?.Value;
            Assert.Equal("true", acceptEula);
            Assert.Contains("Microsoft-Windows-Setup", xml);
        }
    }

    [Fact]
    public void The_windowsPE_pass_appears_before_specialize_and_oobeSystem()
    {
        var xml = AutounattendGenerator.Generate(DefaultConfig(), allowOfflineOobe: true);

        var windowsPeIndex = xml.IndexOf("pass=\"windowsPE\"", StringComparison.Ordinal);
        var specializeIndex = xml.IndexOf("pass=\"specialize\"", StringComparison.Ordinal);
        var oobeSystemIndex = xml.IndexOf("pass=\"oobeSystem\"", StringComparison.Ordinal);

        Assert.True(windowsPeIndex >= 0 && windowsPeIndex < specializeIndex && specializeIndex < oobeSystemIndex);
    }

    [Fact]
    public void The_specialize_pass_appears_before_the_oobeSystem_pass()
    {
        // Orden documentado del propio esquema de unattend: specialize se
        // ejecuta antes que oobeSystem: si el XML se generara en otro orden,
        // seguiría siendo válido para Setup (que no depende del orden de
        // <settings>), pero mantenerlo en orden lógico facilita auditarlo.
        var xml = AutounattendGenerator.Generate(DefaultConfig(), allowOfflineOobe: true);

        var specializeIndex = xml.IndexOf("pass=\"specialize\"", StringComparison.Ordinal);
        var oobeSystemIndex = xml.IndexOf("pass=\"oobeSystem\"", StringComparison.Ordinal);

        Assert.True(specializeIndex >= 0 && oobeSystemIndex >= 0 && specializeIndex < oobeSystemIndex);
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

    // ----- P30: cuenta local configurable + contraseña opcional -----

    [Fact]
    public void AccountName_has_no_default_value()
    {
        // El propio modelo (no este generador) es responsable de no tener un
        // valor por defecto: aquí se comprueba que un record recién creado sin
        // inicializar AccountName queda vacío, nunca "Usuario" ni ningún otro nombre fijo.
        var config = new AutounattendConfiguration();

        Assert.Equal(string.Empty, config.AccountName);
    }

    [Fact]
    public void Validate_accepts_an_empty_password_with_an_empty_confirmation()
    {
        var result = AutounattendGenerator.Validate(DefaultConfig() with { Password = null, ConfirmPassword = null });

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_accepts_a_password_that_matches_its_confirmation()
    {
        var result = AutounattendGenerator.Validate(DefaultConfig() with { Password = "abc123", ConfirmPassword = "abc123" });

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_rejects_a_password_that_does_not_match_its_confirmation()
    {
        var result = AutounattendGenerator.Validate(DefaultConfig() with { Password = "abc123", ConfirmPassword = "xyz789" });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("no coinciden"));
    }

    [Fact]
    public void Validate_rejects_a_password_without_its_confirmation()
    {
        var result = AutounattendGenerator.Validate(DefaultConfig() with { Password = "abc123", ConfirmPassword = null });

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_rejects_a_confirmation_when_no_password_was_provided()
    {
        var result = AutounattendGenerator.Validate(DefaultConfig() with { Password = null, ConfirmPassword = "abc123" });

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_rejects_an_account_name_with_leading_or_trailing_whitespace()
    {
        var result = AutounattendGenerator.Validate(DefaultConfig() with { AccountName = " Carlos" });

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_rejects_an_account_name_that_is_only_whitespace()
    {
        var result = AutounattendGenerator.Validate(DefaultConfig() with { AccountName = "   " });

        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData("Carlos")]
    [InlineData("Tecnico")]
    [InlineData("Juan")]
    public void Generate_uses_whatever_custom_name_the_caller_provides_never_a_fixed_default(string customName)
    {
        var xml = AutounattendGenerator.Generate(DefaultConfig() with { AccountName = customName }, allowOfflineOobe: true);

        Assert.Contains($"<Name>{customName}</Name>", xml);
    }

    [Fact]
    public void No_source_file_in_this_project_hardcodes_the_username_Usuario_as_a_default()
    {
        // P30: el modelo ya no debe tener "Usuario" como valor por defecto en
        // ningún sitio del código fuente (solo en tests, donde es un valor de
        // prueba explícito, no un default silencioso).
        var repoRoot = FindRepoRoot();
        var modelFile = Path.Combine(repoRoot, "src", "MRS.ISOEngine", "Models", "AutounattendConfiguration.cs");

        // Se busca la asignación real (fuera de comentarios doc, donde "Usuario"
        // aparece deliberadamente para EXPLICAR que ya no es el valor por defecto).
        var codeLines = File.ReadAllLines(modelFile).Where(l => !l.TrimStart().StartsWith("///"));
        Assert.DoesNotContain(codeLines, l => l.Contains("= \"Usuario\""));
    }

    [Fact]
    public void P29_hardware_bypass_and_OOBE_mechanisms_remain_intact()
    {
        // Regresión P30: la cuenta local configurable no debe tocar nada de lo
        // que P28/P29 ya dejó funcionando de verdad en la prueba real.
        var xml = AutounattendGenerator.Generate(DefaultConfig(), allowOfflineOobe: true);
        var document = XDocument.Parse(xml);
        XNamespace ns = "urn:schemas-microsoft-com:unattend";

        var passes = document.Root!.Elements(ns + "settings").Select(e => (string?)e.Attribute("pass")).ToList();
        Assert.Contains("windowsPE", passes);
        Assert.Contains("specialize", passes);
        Assert.Contains("oobeSystem", passes);
        Assert.Contains("BypassNRO", xml);
        Assert.Contains(@"HKLM\SYSTEM\Setup\OOBE", xml);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "MRS-Windows-Builder.sln")))
            dir = dir.Parent;

        return dir?.FullName ?? throw new InvalidOperationException("No se encontró la raíz del repositorio.");
    }
}
