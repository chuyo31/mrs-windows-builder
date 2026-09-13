using MRS.ComponentCatalog.Classification;
using MRS.ComponentCatalog.Models;
using Xunit;

namespace MRS.ComponentCatalog.Tests;

public class ProtectionEngineTests
{
    private readonly ProtectionEngine _engine = new();

    private static ComponentDefinition Component(string name) => new()
    {
        Id = $"package:{name}",
        Name = name,
        DisplayName = name,
        Category = ComponentCategory.Unknown,
        Protection = ComponentProtection.Unknown,
        Risk = ComponentRisk.Low,
    };

    [Theory]
    [InlineData("Microsoft-Windows-WindowsUpdate-Package")]
    [InlineData("Microsoft-Windows-ServicingStack-Package")]
    [InlineData("Microsoft-Windows-VCLibs-Package")] // caso hipotético de un paquete, no solo AppX
    [InlineData("Microsoft.UI.Xaml.2.8")]
    public void Protects_critical_components_with_a_reason(string name)
    {
        var protectedComponent = _engine.Apply(Component(name));

        Assert.Equal(ComponentProtection.Protected, protectedComponent.Protection);
        Assert.False(string.IsNullOrWhiteSpace(protectedComponent.ProtectionReason));
    }

    [Fact]
    public void Protects_windows_update_with_critical_risk()
    {
        var result = _engine.Apply(Component("Package_for_KB123456~amd64~LCU~1.0.0.0"));

        Assert.Equal(ComponentProtection.Protected, result.Protection);
        Assert.Equal(ComponentRisk.Critical, result.Risk);
        Assert.Contains("Windows Update", result.ProtectionReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Protects_servicing_stack_with_critical_risk_and_reason()
    {
        var result = _engine.Apply(Component("Microsoft-Windows-ServicingStack-OnDemand-Package"));

        Assert.Equal(ComponentProtection.Protected, result.Protection);
        Assert.Equal(ComponentRisk.Critical, result.Risk);
        Assert.Contains("Servicing Stack", result.ProtectionReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Protects_vclibs_explaining_it_is_a_shared_framework()
    {
        var result = _engine.Apply(Component("Microsoft.VCLibs.140.00"));

        Assert.Equal(ComponentProtection.Protected, result.Protection);
        Assert.Contains("framework", result.ProtectionReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Protects_ui_xaml_explaining_it_is_used_by_modern_apps()
    {
        var result = _engine.Apply(Component("Microsoft.UI.Xaml.2.8"));

        Assert.Equal(ComponentProtection.Protected, result.Protection);
        Assert.Contains("UI.Xaml", result.ProtectionReason);
    }

    [Theory]
    [InlineData("Microsoft-Windows-WiFiDirect-Package")]
    [InlineData("Microsoft-Windows-Bluetooth-Package")]
    [InlineData("Microsoft-Windows-Ethernet-Package")]
    public void Protects_basic_networking(string name)
    {
        var result = _engine.Apply(Component(name));

        Assert.Equal(ComponentProtection.Protected, result.Protection);
        Assert.Equal(ComponentRisk.High, result.Risk);
    }

    [Fact]
    public void Protects_winre()
    {
        var result = _engine.Apply(Component("Microsoft-Windows-WinRE-Package"));

        Assert.Equal(ComponentProtection.Protected, result.Protection);
        Assert.Contains("recuperación", result.ProtectionReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Never_downgrades_an_already_higher_risk()
    {
        var alreadyCritical = Component("Microsoft-Windows-Print-Package") with { Risk = ComponentRisk.Critical };

        var result = _engine.Apply(alreadyCritical);

        Assert.Equal(ComponentRisk.Critical, result.Risk); // la regla de Print es Medium; no debe bajarlo
    }

    [Fact]
    public void Components_with_no_matching_rule_are_left_untouched()
    {
        var component = Component("Contoso.SampleThirdPartyWidget");

        var result = _engine.Apply(component);

        Assert.Equal(component, result);
    }

    [Fact]
    public void ApplyAll_processes_a_full_collection()
    {
        var components = new[] { Component("Contoso.Foo"), Component("Microsoft-Windows-Defender-Package") };

        var result = _engine.ApplyAll(components);

        Assert.Equal(ComponentProtection.Unknown, result[0].Protection);
        Assert.Equal(ComponentProtection.Protected, result[1].Protection);
    }

    // ---- P13: SecurityOptions (Defender / Windows Update) -------------------

    [Fact]
    public void Defender_is_protected_by_default_with_no_security_options()
    {
        var result = _engine.Apply(Component("Microsoft-Windows-Defender-Package"));

        Assert.Equal(ComponentProtection.Protected, result.Protection);
    }

    [Fact]
    public void Defender_is_protected_when_KeepDefender_is_true()
    {
        var options = new SecurityOptions { KeepDefender = true, KeepWindowsUpdate = true };

        var result = _engine.Apply(Component("Microsoft-Windows-Defender-Package"), options);

        Assert.Equal(ComponentProtection.Protected, result.Protection);
    }

    [Fact]
    public void Defender_is_no_longer_blocked_by_this_rule_when_KeepDefender_is_false()
    {
        var options = new SecurityOptions { KeepDefender = false, KeepWindowsUpdate = true };

        var result = _engine.Apply(Component("Microsoft-Windows-Defender-Package"), options);

        // "Permitido" no significa "eliminado": solo deja de estar protegido por
        // esta regla específica. No se ejecuta ninguna desactivación real (P13).
        Assert.NotEqual(ComponentProtection.Protected, result.Protection);
    }

    [Fact]
    public void WindowsUpdate_is_protected_when_KeepWindowsUpdate_is_true()
    {
        var options = new SecurityOptions { KeepDefender = true, KeepWindowsUpdate = true };

        var result = _engine.Apply(Component("Microsoft-Windows-WindowsUpdate-Package"), options);

        Assert.Equal(ComponentProtection.Protected, result.Protection);
    }

    [Fact]
    public void WindowsUpdate_is_no_longer_blocked_by_this_rule_when_KeepWindowsUpdate_is_false()
    {
        var options = new SecurityOptions { KeepDefender = true, KeepWindowsUpdate = false };

        var result = _engine.Apply(Component("Microsoft-Windows-WindowsUpdate-Package"), options);

        Assert.NotEqual(ComponentProtection.Protected, result.Protection);
    }

    [Theory]
    [InlineData("Microsoft-Windows-ServicingStack-OnDemand-Package")] // Servicing Stack
    [InlineData("Microsoft-Windows-WinRE-Package")] // WinRE
    [InlineData("Microsoft-Windows-OOBE-Package")] // OOBE
    [InlineData("Microsoft-Windows-Ethernet-Package")] // red
    [InlineData("Microsoft-Windows-Audio-Package")] // audio
    [InlineData("Microsoft-Windows-USB-Package")] // USB
    [InlineData("Microsoft-Windows-Print-Package")] // impresión
    [InlineData("Microsoft.UI.Xaml.2.8")] // framework necesario
    public void Other_critical_protections_are_unaffected_by_KeepDefender_false(string name)
    {
        var options = new SecurityOptions { KeepDefender = false, KeepWindowsUpdate = false };

        var result = _engine.Apply(Component(name), options);

        Assert.Equal(ComponentProtection.Protected, result.Protection);
    }

    [Fact]
    public void Other_windows_update_related_protections_are_unaffected_by_KeepWindowsUpdate_false()
    {
        // CBS/SSU/LCU/ServicingStack son parte del servicing stack, no de "Windows
        // Update" en sí: KeepWindowsUpdate=false nunca debe debilitarlas.
        var options = new SecurityOptions { KeepDefender = true, KeepWindowsUpdate = false };

        Assert.Equal(ComponentProtection.Protected, _engine.Apply(Component("Microsoft-Windows-ServicingStack-OnDemand-Package"), options).Protection);
        Assert.Equal(ComponentProtection.Protected, _engine.Apply(Component("Package_for_KB123456~amd64~CBS~1.0.0.0"), options).Protection);
        Assert.Equal(ComponentProtection.Protected, _engine.Apply(Component("Package_for_KB123456~amd64~SSU~1.0.0.0"), options).Protection);
        Assert.Equal(ComponentProtection.Protected, _engine.Apply(Component("Package_for_KB123456~amd64~LCU~1.0.0.0"), options).Protection);
    }

    [Fact]
    public void SecurityHealth_is_unaffected_by_KeepDefender_false()
    {
        // SecurityHealth es un componente de "Seguridad de Windows" distinto del
        // motor de Defender propiamente dicho; KeepDefender solo afecta a la regla
        // "windows-defender".
        var options = new SecurityOptions { KeepDefender = false, KeepWindowsUpdate = true };

        var result = _engine.Apply(Component("Microsoft-Windows-SecurityHealth-Package"), options);

        Assert.Equal(ComponentProtection.Protected, result.Protection);
    }
}
