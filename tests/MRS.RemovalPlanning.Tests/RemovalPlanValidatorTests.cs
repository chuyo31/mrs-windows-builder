using MRS.ComponentCatalog.Models;
using MRS.ImageEngine.Models;
using MRS.RemovalPlanning.Models;
using Xunit;

namespace MRS.RemovalPlanning.Tests;

public class RemovalPlanValidatorTests
{
    private readonly RemovalPlanValidator _validator = new();
    private readonly RemovalPlanBuilder _builder = new();
    private static readonly ImageInventory EmptyInventory = new();

    [Fact]
    public void A_plan_built_from_a_valid_selection_validates_as_valid()
    {
        var clipchamp = TestComponents.Appx("clipchamp", "Clipchamp");
        var catalog = TestComponents.Catalog(new[] { clipchamp });
        var plan = _builder.Build(EmptyInventory, catalog, new[] { new ComponentSelection(clipchamp.Id, true) });

        var result = _validator.Validate(plan, catalog);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Rejects_an_action_incompatible_with_the_source_type()
    {
        var pkg = TestComponents.Package("pkg", "Contoso.Pkg");
        var catalog = TestComponents.Catalog(new[] { pkg });

        var plan = new RemovalPlan
        {
            Components = new[]
            {
                new RemovalPlanItem
                {
                    ComponentId = pkg.Id,
                    Requested = true,
                    Allowed = true,
                    Action = RemovalActionType.RemoveAppx, // incompatible: pkg es Package, no Appx
                    CurrentState = ComponentCurrentState.Installed,
                },
            },
        };

        var result = _validator.Validate(plan, catalog);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("no es compatible"));
    }

    [Fact]
    public void Rejects_a_component_not_present_in_the_catalog()
    {
        var catalog = TestComponents.Catalog(Array.Empty<ComponentDefinition>());
        var plan = new RemovalPlan
        {
            Components = new[]
            {
                new RemovalPlanItem { ComponentId = "appx:ghost", Requested = true, Allowed = false },
            },
        };

        var result = _validator.Validate(plan, catalog);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("no existe en el catálogo"));
    }

    [Fact]
    public void Rejects_an_undetected_component()
    {
        var ghost = TestComponents.Appx("ghost", "Ghost") with { Detected = false };
        var catalog = TestComponents.Catalog(new[] { ghost });
        var plan = new RemovalPlan
        {
            Components = new[] { new RemovalPlanItem { ComponentId = ghost.Id, Requested = true, Allowed = false } },
        };

        var result = _validator.Validate(plan, catalog);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("no está detectado"));
    }

    [Fact]
    public void Rejects_an_allowed_action_on_a_protected_component()
    {
        var vclibs = TestComponents.Appx("vclibs", "Microsoft.VCLibs.140.00", protection: ComponentProtection.Protected);
        var catalog = TestComponents.Catalog(new[] { vclibs });
        var plan = new RemovalPlan
        {
            Components = new[]
            {
                new RemovalPlanItem
                {
                    ComponentId = vclibs.Id, Requested = true, Allowed = true,
                    Action = RemovalActionType.RemoveAppx, CurrentState = ComponentCurrentState.Installed,
                },
            },
        };

        var result = _validator.Validate(plan, catalog);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("protegido"));
    }

    [Fact]
    public void Rejects_a_conflicting_pair_both_allowed()
    {
        var a = TestComponents.Appx("a", "A") with { Conflicts = new[] { "appx:b" } };
        var b = TestComponents.Appx("b", "B");
        var catalog = TestComponents.Catalog(new[] { a, b });

        var plan = new RemovalPlan
        {
            Components = new[]
            {
                new RemovalPlanItem { ComponentId = a.Id, Requested = true, Allowed = true, Action = RemovalActionType.RemoveAppx, CurrentState = ComponentCurrentState.Installed },
                new RemovalPlanItem { ComponentId = b.Id, Requested = true, Allowed = true, Action = RemovalActionType.RemoveAppx, CurrentState = ComponentCurrentState.Installed },
            },
        };

        var result = _validator.Validate(plan, catalog);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("conflicto"));
    }

    [Fact]
    public void Rejects_removal_of_a_dependency_still_needed_by_a_remaining_component()
    {
        var paint = TestComponents.Appx("paint", "Microsoft.Paint");
        var vclibs = TestComponents.Appx("vclibs", "Microsoft.VCLibs.140.00", category: ComponentCategory.Framework);
        var catalog = TestComponents.Catalog(
            new[] { paint, vclibs },
            new[] { new ComponentDependency(paint.Id, vclibs.Id, DependencyType.Framework) });

        // Se fuerza (a mano) un plan inválido: intenta eliminar VCLibs sin eliminar Paint.
        var plan = new RemovalPlan
        {
            Components = new[]
            {
                new RemovalPlanItem { ComponentId = vclibs.Id, Requested = true, Allowed = true, Action = RemovalActionType.RemoveAppx, CurrentState = ComponentCurrentState.Installed },
            },
        };

        var result = _validator.Validate(plan, catalog);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("es requerido por"));
    }

    [Fact]
    public void Rejects_an_item_with_an_empty_component_id()
    {
        var catalog = TestComponents.Catalog(Array.Empty<ComponentDefinition>());
        var plan = new RemovalPlan
        {
            Components = new[] { new RemovalPlanItem { ComponentId = "", Requested = true } },
        };

        var result = _validator.Validate(plan, catalog);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Rejects_an_action_present_on_a_blocked_item()
    {
        var pkg = TestComponents.Package("pkg", "Contoso.Pkg");
        var catalog = TestComponents.Catalog(new[] { pkg });
        var plan = new RemovalPlan
        {
            Components = new[]
            {
                new RemovalPlanItem { ComponentId = pkg.Id, Requested = true, Allowed = false, Action = RemovalActionType.RemovePackage },
            },
        };

        var result = _validator.Validate(plan, catalog);

        Assert.False(result.IsValid);
    }
}
