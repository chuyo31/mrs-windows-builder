using MRS.ComponentCatalog.Classification;
using MRS.ComponentCatalog.Models;
using MRS.ImageEngine.Models;
using Xunit;

namespace MRS.ComponentCatalog.Tests;

public class CatalogClassifierTests
{
    private readonly CatalogClassifier _classifier = new();

    private static ComponentDefinition Find(IReadOnlyList<ComponentDefinition> components, string nameContains)
        => components.Single(c => c.Name.Contains(nameContains, StringComparison.OrdinalIgnoreCase)
                                   || c.DisplayName.Contains(nameContains, StringComparison.OrdinalIgnoreCase));

    [Fact]
    public void Classifies_xbox_as_gaming()
    {
        var components = _classifier.Classify(FakeInventory.Sample());

        Assert.Equal(ComponentCategory.Gaming, Find(components, "Xbox").Category);
    }

    [Fact]
    public void Classifies_clipchamp_as_application()
    {
        var components = _classifier.Classify(FakeInventory.Sample());

        Assert.Equal(ComponentCategory.Application, Find(components, "Clipchamp").Category);
    }

    [Fact]
    public void Classifies_copilot_as_ai()
    {
        var components = _classifier.Classify(FakeInventory.Sample());

        Assert.Equal(ComponentCategory.AI, Find(components, "Copilot").Category);
    }

    [Fact]
    public void Classifies_store_apps_as_store_category()
    {
        var components = _classifier.Classify(FakeInventory.Sample());

        Assert.Equal(ComponentCategory.Store, Find(components, "WindowsStore").Category);
        Assert.Equal(ComponentCategory.Store, Find(components, "StorePurchaseApp").Category);
    }

    [Fact]
    public void Classifies_frameworks()
    {
        var components = _classifier.Classify(FakeInventory.Sample());

        Assert.Equal(ComponentCategory.Framework, Find(components, "VCLibs").Category);
        Assert.Equal(ComponentCategory.Framework, Find(components, "UI.Xaml").Category);
    }

    [Fact]
    public void Classifies_defender_as_security()
    {
        var components = _classifier.Classify(FakeInventory.Sample());

        Assert.Equal(ComponentCategory.Security, Find(components, "Defender").Category);
    }

    [Fact]
    public void Unmatched_component_stays_unknown_with_no_protection()
    {
        var components = _classifier.Classify(FakeInventory.Sample());

        var unknown = Find(components, "Contoso");

        Assert.Equal(ComponentCategory.Unknown, unknown.Category);
        Assert.Equal(ComponentProtection.Unknown, unknown.Protection);
        Assert.Null(unknown.ProtectionReason);
    }

    [Fact]
    public void Does_not_mark_every_match_as_removable_automatically()
    {
        // Clasificación != protección: un componente clasificado (p. ej. Framework)
        // no queda "Removable" solo por tener categoría; ProtectionEngine decide eso.
        var components = _classifier.Classify(FakeInventory.Sample());

        var vclibs = Find(components, "VCLibs");
        Assert.NotEqual(ComponentProtection.Removable, vclibs.Protection);
    }

    [Fact]
    public void Installed_state_is_read_from_dism_state()
    {
        var components = _classifier.Classify(FakeInventory.Sample());

        var servicingStack = Find(components, "ServicingStack");
        Assert.True(servicingStack.Installed);
        Assert.False(servicingStack.Superseded);
    }

    [Fact]
    public void Superseded_state_is_detected()
    {
        var components = _classifier.Classify(FakeInventory.Sample());

        var lcu = Find(components, "KB5044384");
        Assert.True(lcu.Superseded);
        Assert.False(lcu.Installed);
    }

    [Fact]
    public void Empty_inventory_produces_empty_catalog()
    {
        var components = _classifier.Classify(new ImageInventory());

        Assert.Empty(components);
    }
}
