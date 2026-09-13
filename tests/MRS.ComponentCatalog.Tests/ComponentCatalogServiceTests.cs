using MRS.ComponentCatalog.Models;
using MRS.ComponentCatalog.Query;
using Xunit;

namespace MRS.ComponentCatalog.Tests;

public class ComponentCatalogServiceTests
{
    private readonly ComponentCatalogService _service = new();

    private static ComponentDefinition Find(IEnumerable<ComponentDefinition> components, string nameContains)
        => components.Single(c => c.Name.Contains(nameContains, StringComparison.OrdinalIgnoreCase)
                                   || c.DisplayName.Contains(nameContains, StringComparison.OrdinalIgnoreCase));

    [Fact]
    public void BuildCatalog_produces_one_component_per_inventory_item()
    {
        var inventory = FakeInventory.Sample();
        var catalog = _service.BuildCatalog(inventory);

        Assert.Equal(
            inventory.PackageCount + inventory.ProvisionedAppCount + inventory.FeatureCount +
            inventory.CapabilityCount + inventory.DriverCount,
            catalog.TotalCount);
    }

    [Fact]
    public void A_different_inventory_produces_a_different_catalog()
    {
        var full = _service.BuildCatalog(FakeInventory.Sample());
        var empty = _service.BuildCatalog(new MRS.ImageEngine.Models.ImageInventory());

        Assert.NotEqual(full.TotalCount, empty.TotalCount);
        Assert.Equal(0, empty.TotalCount);
    }

    [Fact]
    public void Paint_depends_on_the_shared_frameworks_present_in_the_inventory()
    {
        var catalog = _service.BuildCatalog(FakeInventory.Sample());
        var paint = Find(catalog.Components, "Paint");
        var vclibs = Find(catalog.Components, "VCLibs");
        var uiXaml = Find(catalog.Components, "UI.Xaml");

        var paintDependencies = catalog.DependenciesOf(paint.Id).Select(c => c.Id).ToList();

        Assert.Contains(vclibs.Id, paintDependencies);
        Assert.Contains(uiXaml.Id, paintDependencies);
    }

    [Fact]
    public void Vclibs_reports_dependents_using_it()
    {
        var catalog = _service.BuildCatalog(FakeInventory.Sample());
        var vclibs = Find(catalog.Components, "VCLibs");

        var dependents = catalog.DependentsOf(vclibs.Id).Select(c => c.Id).ToList();

        Assert.NotEmpty(dependents);
        Assert.DoesNotContain(vclibs.Id, dependents); // un framework no depende de sí mismo
    }

    [Fact]
    public void Protected_components_are_never_counted_as_removable()
    {
        var catalog = _service.BuildCatalog(FakeInventory.Sample());

        Assert.True(catalog.ProtectedCount > 0);
        var protectedIds = catalog.Components.Where(c => c.Protection == ComponentProtection.Protected).Select(c => c.Id).ToHashSet();
        var removableIds = catalog.Components.Where(c => c.Protection == ComponentProtection.Removable).Select(c => c.Id).ToHashSet();

        Assert.Empty(protectedIds.Intersect(removableIds));
    }

    [Fact]
    public void Search_finds_components_by_partial_case_insensitive_text()
    {
        var catalog = _service.BuildCatalog(FakeInventory.Sample());

        var results = CatalogQuery.Search(catalog.Components, "xbox").ToList();

        Assert.Single(results);
        Assert.Contains("Xbox", results[0].DisplayName);
    }

    [Fact]
    public void Search_finds_components_by_category_name()
    {
        var catalog = _service.BuildCatalog(FakeInventory.Sample());

        var results = CatalogQuery.Search(catalog.Components, "gaming").ToList();

        Assert.Contains(results, c => c.Category == ComponentCategory.Gaming);
    }

    [Fact]
    public void Search_with_empty_term_returns_everything()
    {
        var catalog = _service.BuildCatalog(FakeInventory.Sample());

        var results = CatalogQuery.Search(catalog.Components, "").ToList();

        Assert.Equal(catalog.TotalCount, results.Count);
    }

    [Fact]
    public void Filter_protected_returns_only_protected_components()
    {
        var catalog = _service.BuildCatalog(FakeInventory.Sample());

        var results = CatalogQuery.Filter(catalog.Components, CatalogFilterKind.Protected).ToList();

        Assert.NotEmpty(results);
        Assert.All(results, c => Assert.Equal(ComponentProtection.Protected, c.Protection));
    }

    [Fact]
    public void Filter_frameworks_returns_only_framework_category()
    {
        var catalog = _service.BuildCatalog(FakeInventory.Sample());

        var results = CatalogQuery.Filter(catalog.Components, CatalogFilterKind.Frameworks).ToList();

        Assert.NotEmpty(results);
        Assert.All(results, c => Assert.Equal(ComponentCategory.Framework, c.Category));
    }

    [Fact]
    public void Filter_all_returns_everything_unchanged()
    {
        var catalog = _service.BuildCatalog(FakeInventory.Sample());

        var results = CatalogQuery.Filter(catalog.Components, CatalogFilterKind.All).ToList();

        Assert.Equal(catalog.TotalCount, results.Count);
    }

    // ---- P13: SecurityOptions de extremo a extremo (Inventory -> Catalog) --

    [Fact]
    public void BuildCatalog_protects_Defender_by_default_with_no_security_options()
    {
        var catalog = _service.BuildCatalog(FakeInventory.Sample());

        var defender = Find(catalog.Components, "Defender");

        Assert.Equal(ComponentProtection.Protected, defender.Protection);
    }

    [Fact]
    public void BuildCatalog_no_longer_protects_Defender_specifically_when_KeepDefender_is_false()
    {
        var catalog = _service.BuildCatalog(FakeInventory.Sample(), new SecurityOptions { KeepDefender = false, KeepWindowsUpdate = true });

        var defender = Find(catalog.Components, "Defender");

        Assert.NotEqual(ComponentProtection.Protected, defender.Protection);
    }

    [Fact]
    public void BuildCatalog_keeps_ServicingStack_and_WinRE_protected_even_when_both_security_options_are_false()
    {
        var catalog = _service.BuildCatalog(FakeInventory.Sample(), new SecurityOptions { KeepDefender = false, KeepWindowsUpdate = false });

        var servicingStack = Find(catalog.Components, "ServicingStack");
        var winre = Find(catalog.Components, "WinRE");

        Assert.Equal(ComponentProtection.Protected, servicingStack.Protection);
        Assert.Equal(ComponentProtection.Protected, winre.Protection);
    }
}
