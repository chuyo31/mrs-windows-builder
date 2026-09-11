using MRS.ComponentCatalog.Models;
using MRS.ImageEngine.Models;
using MRS.RemovalPlanning.Models;
using Xunit;

namespace MRS.RemovalPlanning.Tests;

public class RemovalPlanBuilderTests
{
    private readonly RemovalPlanBuilder _builder = new();
    private static readonly ImageInventory EmptyInventory = new();

    private RemovalPlan Build(ComponentCatalogResult catalog, params ComponentSelection[] selections)
        => _builder.Build(EmptyInventory, catalog, selections);

    // ================= SELECCIÓN =================

    [Fact]
    public void Selecting_a_valid_component_produces_one_item()
    {
        var clipchamp = TestComponents.Appx("clipchamp", "Clipchamp");
        var catalog = TestComponents.Catalog(new[] { clipchamp });

        var plan = Build(catalog, new ComponentSelection(clipchamp.Id, true));

        var item = Assert.Single(plan.Components);
        Assert.Equal(clipchamp.Id, item.ComponentId);
        Assert.True(item.Requested);
    }

    [Fact]
    public void Selecting_a_nonexistent_component_reports_an_error()
    {
        var catalog = TestComponents.Catalog(Array.Empty<ComponentDefinition>());

        var plan = Build(catalog, new ComponentSelection("appx:does-not-exist", true));

        Assert.NotEmpty(plan.Errors);
        Assert.False(plan.IsValid);
        var item = Assert.Single(plan.Components);
        Assert.False(item.Allowed);
    }

    [Fact]
    public void Selecting_multiple_components_produces_multiple_items()
    {
        var xbox = TestComponents.Appx("xbox", "Xbox", category: ComponentCategory.Gaming);
        var clipchamp = TestComponents.Appx("clipchamp", "Clipchamp");
        var catalog = TestComponents.Catalog(new[] { xbox, clipchamp });

        var plan = Build(catalog,
            new ComponentSelection(xbox.Id, true),
            new ComponentSelection(clipchamp.Id, true));

        Assert.Equal(2, plan.Components.Count);
        Assert.Equal(2, plan.TotalSelected);
    }

    [Fact]
    public void Empty_selection_produces_an_empty_but_valid_plan()
    {
        var catalog = TestComponents.Catalog(Array.Empty<ComponentDefinition>());

        var plan = Build(catalog);

        Assert.Empty(plan.Components);
        Assert.Equal(0, plan.TotalSelected);
        Assert.True(plan.IsValid);
    }

    [Fact]
    public void Unselected_entries_in_the_selection_list_are_ignored()
    {
        var clipchamp = TestComponents.Appx("clipchamp", "Clipchamp");
        var catalog = TestComponents.Catalog(new[] { clipchamp });

        var plan = Build(catalog, new ComponentSelection(clipchamp.Id, false));

        Assert.Empty(plan.Components);
    }

    // ================= PROTECCIÓN =================

    [Fact]
    public void Protected_component_is_blocked()
    {
        var vclibs = TestComponents.Appx("vclibs", "Microsoft.VCLibs.140.00",
            protection: ComponentProtection.Protected, category: ComponentCategory.Framework,
            protectionReason: "Framework protegido porque puede ser utilizado por otras aplicaciones.");
        var catalog = TestComponents.Catalog(new[] { vclibs });

        var plan = Build(catalog, new ComponentSelection(vclibs.Id, true));

        var item = Assert.Single(plan.Components);
        Assert.False(item.Allowed);
        Assert.Equal(RemovalActionType.None, item.Action);
    }

    [Fact]
    public void Protected_component_has_a_block_reason()
    {
        const string reason = "Framework protegido porque puede ser utilizado por otras aplicaciones.";
        var vclibs = TestComponents.Appx("vclibs", "Microsoft.VCLibs.140.00",
            protection: ComponentProtection.Protected, protectionReason: reason);
        var catalog = TestComponents.Catalog(new[] { vclibs });

        var plan = Build(catalog, new ComponentSelection(vclibs.Id, true));

        Assert.Equal(reason, plan.Components[0].BlockReason);
    }

    [Fact]
    public void Protected_component_generates_a_critical_warning()
    {
        var defender = TestComponents.Package("defender", "Microsoft-Windows-Defender-Package",
            protection: ComponentProtection.Protected, protectionReason: "Motor de Defender.");
        var catalog = TestComponents.Catalog(new[] { defender });

        var plan = Build(catalog, new ComponentSelection(defender.Id, true));

        Assert.Contains(plan.Warnings, w => w.Severity == RemovalWarningSeverity.Critical && w.ComponentId == defender.Id);
    }

    // ================= DEPENDENCIAS =================

    [Fact]
    public void Selecting_a_component_never_removes_its_unselected_dependency()
    {
        var paint = TestComponents.Appx("paint", "Microsoft.Paint");
        var vclibs = TestComponents.Appx("vclibs", "Microsoft.VCLibs.140.00", category: ComponentCategory.Framework);
        var catalog = TestComponents.Catalog(
            new[] { paint, vclibs },
            new[] { new ComponentDependency(paint.Id, vclibs.Id, DependencyType.Framework) });

        // Solo se selecciona Paint; VCLibs no se pide.
        var plan = Build(catalog, new ComponentSelection(paint.Id, true));

        Assert.DoesNotContain(plan.Components, c => c.ComponentId == vclibs.Id);
        Assert.DoesNotContain(plan.Actions, a => a.ComponentId == vclibs.Id);
    }

    [Fact]
    public void Warns_when_a_selected_component_uses_a_protected_dependency()
    {
        var paint = TestComponents.Appx("paint", "Microsoft.Paint");
        var vclibs = TestComponents.Appx("vclibs", "Microsoft.VCLibs.140.00",
            protection: ComponentProtection.Protected, category: ComponentCategory.Framework);
        var catalog = TestComponents.Catalog(
            new[] { paint, vclibs },
            new[] { new ComponentDependency(paint.Id, vclibs.Id, DependencyType.Framework) });

        var plan = Build(catalog, new ComponentSelection(paint.Id, true));

        var paintItem = plan.Components.Single(c => c.ComponentId == paint.Id);
        Assert.Contains(paintItem.Warnings, w => w.Code == "uses-protected-dependency");
    }

    [Fact]
    public void No_dependency_warning_when_the_dependency_is_not_protected()
    {
        var paint = TestComponents.Appx("paint", "Microsoft.Paint");
        var vclibs = TestComponents.Appx("vclibs", "Microsoft.VCLibs.140.00",
            protection: ComponentProtection.Recommended, category: ComponentCategory.Framework);
        var catalog = TestComponents.Catalog(
            new[] { paint, vclibs },
            new[] { new ComponentDependency(paint.Id, vclibs.Id, DependencyType.Framework) });

        var plan = Build(catalog, new ComponentSelection(paint.Id, true));

        var paintItem = plan.Components.Single(c => c.ComponentId == paint.Id);
        Assert.DoesNotContain(paintItem.Warnings, w => w.Code == "uses-protected-dependency");
    }

    [Fact]
    public void Removing_a_dependency_is_blocked_when_a_remaining_component_needs_it()
    {
        var paint = TestComponents.Appx("paint", "Microsoft.Paint");
        var vclibs = TestComponents.Appx("vclibs", "Microsoft.VCLibs.140.00", category: ComponentCategory.Framework);
        var catalog = TestComponents.Catalog(
            new[] { paint, vclibs },
            new[] { new ComponentDependency(paint.Id, vclibs.Id, DependencyType.Framework) });

        // Paint permanece (no se selecciona); se intenta eliminar VCLibs.
        var plan = Build(catalog, new ComponentSelection(vclibs.Id, true));

        var vclibsItem = plan.Components.Single(c => c.ComponentId == vclibs.Id);
        Assert.False(vclibsItem.Allowed);
        Assert.Contains("permanecerán", vclibsItem.BlockReason ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Removing_a_dependency_is_allowed_when_all_dependents_are_removed_too()
    {
        var paint = TestComponents.Appx("paint", "Microsoft.Paint");
        var vclibs = TestComponents.Appx("vclibs", "Microsoft.VCLibs.140.00", category: ComponentCategory.Framework);
        var catalog = TestComponents.Catalog(
            new[] { paint, vclibs },
            new[] { new ComponentDependency(paint.Id, vclibs.Id, DependencyType.Framework) });

        // Ambos se seleccionan: nada se queda dependiendo de VCLibs.
        var plan = Build(catalog,
            new ComponentSelection(paint.Id, true),
            new ComponentSelection(vclibs.Id, true));

        var vclibsItem = plan.Components.Single(c => c.ComponentId == vclibs.Id);
        Assert.True(vclibsItem.Allowed);
    }

    [Fact]
    public void Cascade_removal_never_happens_automatically()
    {
        var paint = TestComponents.Appx("paint", "Microsoft.Paint");
        var vclibs = TestComponents.Appx("vclibs", "Microsoft.VCLibs.140.00", category: ComponentCategory.Framework);
        var catalog = TestComponents.Catalog(
            new[] { paint, vclibs },
            new[] { new ComponentDependency(paint.Id, vclibs.Id, DependencyType.Framework) });

        var plan = Build(catalog, new ComponentSelection(paint.Id, true));

        // Solo la acción de Paint existe; nunca se genera una acción para VCLibs
        // porque el usuario no lo pidió explícitamente.
        var action = Assert.Single(plan.Actions);
        Assert.Equal(paint.Id, action.ComponentId);
    }

    // ================= PAQUETES =================

    [Fact]
    public void Installed_package_is_allowed_to_be_removed()
    {
        var pkg = TestComponents.Package("foo", "Contoso.Foo", installed: true);
        var catalog = TestComponents.Catalog(new[] { pkg });

        var plan = Build(catalog, new ComponentSelection(pkg.Id, true));

        var item = plan.Components.Single();
        Assert.Equal(ComponentCurrentState.Installed, item.CurrentState);
        Assert.True(item.Allowed);
        Assert.Equal(RemovalActionType.RemovePackage, item.Action);
    }

    [Fact]
    public void Superseded_package_is_never_removed_in_this_phase()
    {
        var pkg = TestComponents.Package("lcu", "Package_for_KB123456~LCU", installed: false, superseded: true);
        var catalog = TestComponents.Catalog(new[] { pkg });

        var plan = Build(catalog, new ComponentSelection(pkg.Id, true));

        var item = plan.Components.Single();
        Assert.Equal(ComponentCurrentState.Superseded, item.CurrentState);
        Assert.False(item.Allowed);
        Assert.Equal(RemovalActionType.None, item.Action);
    }

    [Fact]
    public void Package_with_unknown_state_is_blocked_with_a_warning()
    {
        var pkg = TestComponents.Package("weird", "Contoso.Weird", installed: false, superseded: false);
        var catalog = TestComponents.Catalog(new[] { pkg });

        var plan = Build(catalog, new ComponentSelection(pkg.Id, true));

        var item = plan.Components.Single();
        Assert.Equal(ComponentCurrentState.Unknown, item.CurrentState);
        Assert.False(item.Allowed);
        Assert.Contains(item.Warnings, w => w.Code == "unknown-state");
    }

    // ================= APPX =================

    [Fact]
    public void Removable_appx_is_allowed()
    {
        var clipchamp = TestComponents.Appx("clipchamp", "Clipchamp", protection: ComponentProtection.Removable);
        var catalog = TestComponents.Catalog(new[] { clipchamp });

        var plan = Build(catalog, new ComponentSelection(clipchamp.Id, true));

        var item = plan.Components.Single();
        Assert.True(item.Allowed);
        Assert.Equal(RemovalActionType.RemoveAppx, item.Action);
    }

    [Fact]
    public void Protected_appx_is_blocked()
    {
        var storeCore = TestComponents.Appx("store", "Microsoft.WindowsStore", protection: ComponentProtection.Protected,
            category: ComponentCategory.Store, protectionReason: "Aplicación base de Microsoft Store.");
        var catalog = TestComponents.Catalog(new[] { storeCore });

        var plan = Build(catalog, new ComponentSelection(storeCore.Id, true));

        Assert.False(plan.Components.Single().Allowed);
    }

    [Fact]
    public void Framework_appx_is_never_removed_automatically_even_without_an_explicit_protection_rule()
    {
        // Un framework con Protection=Recommended (no Removable) no debe poder eliminarse.
        var uiXaml = TestComponents.Appx("ui-xaml", "Microsoft.UI.Xaml.2.8",
            protection: ComponentProtection.Recommended, category: ComponentCategory.Framework);
        var catalog = TestComponents.Catalog(new[] { uiXaml });

        var plan = Build(catalog, new ComponentSelection(uiXaml.Id, true));

        var item = plan.Components.Single();
        Assert.False(item.Allowed);
        Assert.Equal(RemovalActionType.None, item.Action);
    }

    // ================= FEATURES =================

    [Fact]
    public void Removable_feature_is_allowed_to_be_disabled()
    {
        var feature = TestComponents.Feature("media", "MediaPlayback", ComponentProtection.Removable);
        var catalog = TestComponents.Catalog(new[] { feature });

        var plan = Build(catalog, new ComponentSelection(feature.Id, true));

        var item = plan.Components.Single();
        Assert.True(item.Allowed);
        Assert.Equal(RemovalActionType.DisableFeature, item.Action);
    }

    [Fact]
    public void Protected_feature_is_blocked()
    {
        var feature = TestComponents.Feature("core", "NetFx4-AdvSrvs", ComponentProtection.Protected);
        var catalog = TestComponents.Catalog(new[] { feature });

        var plan = Build(catalog, new ComponentSelection(feature.Id, true));

        Assert.False(plan.Components.Single().Allowed);
    }

    // ================= CAPABILITIES =================

    [Fact]
    public void Removable_capability_is_allowed()
    {
        var capability = TestComponents.Capability("ssh", "OpenSSH.Client~~~~0.0.1.0", ComponentProtection.Removable);
        var catalog = TestComponents.Catalog(new[] { capability });

        var plan = Build(catalog, new ComponentSelection(capability.Id, true));

        var item = plan.Components.Single();
        Assert.True(item.Allowed);
        Assert.Equal(RemovalActionType.RemoveCapability, item.Action);
    }

    [Fact]
    public void Protected_capability_is_blocked()
    {
        var capability = TestComponents.Capability("lang", "Language.Basic~~~es-ES~0.0.1.0", ComponentProtection.Protected);
        var catalog = TestComponents.Catalog(new[] { capability });

        var plan = Build(catalog, new ComponentSelection(capability.Id, true));

        Assert.False(plan.Components.Single().Allowed);
    }

    [Fact]
    public void Not_present_capability_generates_no_action()
    {
        var capability = TestComponents.Capability("ie", "Browser.InternetExplorer~~~~0.0.11.0",
            ComponentProtection.Removable, installed: false);
        var catalog = TestComponents.Catalog(new[] { capability });

        var plan = Build(catalog, new ComponentSelection(capability.Id, true));

        var item = plan.Components.Single();
        Assert.Equal(ComponentCurrentState.NotPresent, item.CurrentState);
        Assert.False(item.Allowed);
        Assert.Equal(RemovalActionType.None, item.Action);
    }

    // ================= PLAN =================

    [Fact]
    public void Plan_generates_the_correct_action_per_source_type()
    {
        var pkg = TestComponents.Package("pkg", "Contoso.Pkg");
        var appx = TestComponents.Appx("appx", "Contoso.Appx");
        var feature = TestComponents.Feature("feat", "Contoso.Feat", ComponentProtection.Removable);
        var capability = TestComponents.Capability("cap", "Contoso.Cap", ComponentProtection.Removable);
        var catalog = TestComponents.Catalog(new[] { pkg, appx, feature, capability });

        var plan = Build(catalog,
            new ComponentSelection(pkg.Id, true),
            new ComponentSelection(appx.Id, true),
            new ComponentSelection(feature.Id, true),
            new ComponentSelection(capability.Id, true));

        Assert.Equal(RemovalActionType.RemovePackage, plan.Components.Single(c => c.ComponentId == pkg.Id).Action);
        Assert.Equal(RemovalActionType.RemoveAppx, plan.Components.Single(c => c.ComponentId == appx.Id).Action);
        Assert.Equal(RemovalActionType.DisableFeature, plan.Components.Single(c => c.ComponentId == feature.Id).Action);
        Assert.Equal(RemovalActionType.RemoveCapability, plan.Components.Single(c => c.ComponentId == capability.Id).Action);
        Assert.Equal(4, plan.Actions.Count);
    }

    [Fact]
    public void Plan_counts_selected_allowed_and_blocked()
    {
        var allowed = TestComponents.Appx("clipchamp", "Clipchamp");
        var blocked = TestComponents.Appx("vclibs", "Microsoft.VCLibs.140.00", protection: ComponentProtection.Protected);
        var catalog = TestComponents.Catalog(new[] { allowed, blocked });

        var plan = Build(catalog,
            new ComponentSelection(allowed.Id, true),
            new ComponentSelection(blocked.Id, true));

        Assert.Equal(2, plan.TotalSelected);
        Assert.Equal(1, plan.TotalAllowed);
        Assert.Equal(1, plan.TotalBlocked);
    }

    [Fact]
    public void Plan_is_valid_when_every_selected_component_exists()
    {
        var clipchamp = TestComponents.Appx("clipchamp", "Clipchamp");
        var catalog = TestComponents.Catalog(new[] { clipchamp });

        var plan = Build(catalog, new ComponentSelection(clipchamp.Id, true));

        Assert.True(plan.IsValid);
        Assert.Empty(plan.Errors);
    }

    [Fact]
    public void Plan_is_invalid_when_a_selected_component_does_not_exist()
    {
        var catalog = TestComponents.Catalog(Array.Empty<ComponentDefinition>());

        var plan = Build(catalog, new ComponentSelection("appx:ghost", true));

        Assert.False(plan.IsValid);
        Assert.NotEmpty(plan.Errors);
    }

    [Fact]
    public void Plan_never_produces_an_action_for_a_blocked_component()
    {
        var blocked = TestComponents.Appx("vclibs", "Microsoft.VCLibs.140.00", protection: ComponentProtection.Protected);
        var catalog = TestComponents.Catalog(new[] { blocked });

        var plan = Build(catalog, new ComponentSelection(blocked.Id, true));

        Assert.Empty(plan.Actions);
    }

    [Fact]
    public void Estimated_size_is_always_unknown_because_dism_does_not_provide_real_sizes()
    {
        var pkg = TestComponents.Package("pkg", "Contoso.Pkg");
        var catalog = TestComponents.Catalog(new[] { pkg });

        var plan = Build(catalog, new ComponentSelection(pkg.Id, true));

        Assert.False(plan.Components.Single().EstimatedSize.IsKnown);
        Assert.Null(plan.Components.Single().EstimatedSize.Bytes);
    }

    // ================= SEGURIDAD ARQUITECTÓNICA =================

    [Fact]
    public void RemovalPlanning_assembly_never_references_DismEngine()
    {
        var referenced = typeof(RemovalPlanBuilder).Assembly.GetReferencedAssemblies()
            .Select(a => a.Name)
            .ToList();

        Assert.DoesNotContain(referenced, name => name != null && name.Contains("DismEngine", StringComparison.OrdinalIgnoreCase));
    }
}
