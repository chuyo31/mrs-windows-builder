using MRS.ComponentCatalog.Models;
using MRS.ComponentCatalog.Rules;
using MRS.ImageEngine.Models;

namespace MRS.ComponentCatalog.Classification;

/// <summary>
/// Convierte un <see cref="ImageInventory"/> real en componentes clasificados.
///
///   ImageInventory  ->  CatalogClassifier  ->  ComponentDefinition[]
///
/// El catálogo resultante depende ÚNICAMENTE de lo que exista en el inventario:
/// una ISO distinta produce componentes distintos. Las reglas solo clasifican
/// lo detectado, nunca añaden nada que no esté en el inventario.
/// </summary>
public sealed class CatalogClassifier
{
    private readonly IReadOnlyList<ClassificationRule> _rules;

    public CatalogClassifier(CatalogRuleSet? ruleSet = null)
        => _rules = (ruleSet ?? DefaultCatalogRules.Windows11).ClassificationRules
            .Where(r => r.Enabled)
            .ToList();

    public IReadOnlyList<ComponentDefinition> Classify(ImageInventory inventory)
    {
        var result = new List<ComponentDefinition>(
            inventory.PackageCount + inventory.ProvisionedAppCount + inventory.FeatureCount +
            inventory.CapabilityCount + inventory.DriverCount);

        foreach (var package in inventory.Packages)
            result.Add(ClassifyPackage(package));

        foreach (var app in inventory.ProvisionedApps)
            result.Add(ClassifyApp(app));

        foreach (var feature in inventory.Features)
            result.Add(ClassifyFeature(feature));

        foreach (var capability in inventory.Capabilities)
            result.Add(ClassifyCapability(capability));

        foreach (var driver in inventory.Drivers)
            result.Add(ClassifyDriver(driver));

        return result;
    }

    private ComponentDefinition ClassifyPackage(ImagePackage package)
    {
        var category = Match(package.PackageIdentity, package.Description);
        return Build(
            id: $"package:{package.PackageIdentity}",
            name: package.PackageIdentity,
            displayName: package.PackageIdentity,
            description: package.Description,
            category: category,
            sourceType: ComponentSourceType.Package,
            removalMode: RemovalMode.Package,
            installed: string.Equals(package.State, "Installed", StringComparison.OrdinalIgnoreCase),
            superseded: package.State?.Contains("Superseded", StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private ComponentDefinition ClassifyApp(ProvisionedApp app)
    {
        var id = app.PackageName.Length > 0 ? app.PackageName : app.DisplayName;
        var category = Match(app.DisplayName, app.PackageName);
        return Build(
            id: $"appx:{id}",
            name: app.PackageName,
            displayName: app.DisplayName.Length > 0 ? app.DisplayName : app.PackageName,
            description: null,
            category: category,
            sourceType: ComponentSourceType.Appx,
            removalMode: RemovalMode.Appx,
            installed: true,
            superseded: false);
    }

    private ComponentDefinition ClassifyFeature(ImageFeature feature)
    {
        var category = Match(feature.Name, null);
        if (category == ComponentCategory.Unknown)
            category = ComponentCategory.Feature;

        return Build(
            id: $"feature:{feature.Name}",
            name: feature.Name,
            displayName: feature.Name,
            description: null,
            category: category,
            sourceType: ComponentSourceType.Feature,
            removalMode: RemovalMode.Feature,
            installed: string.Equals(feature.State, "Enabled", StringComparison.OrdinalIgnoreCase),
            superseded: false);
    }

    private ComponentDefinition ClassifyCapability(ImageCapability capability)
    {
        var category = Match(capability.Identity, null);
        if (category == ComponentCategory.Unknown)
            category = ComponentCategory.Capability;

        return Build(
            id: $"capability:{capability.Identity}",
            name: capability.Identity,
            displayName: capability.Identity,
            description: null,
            category: category,
            sourceType: ComponentSourceType.Capability,
            removalMode: RemovalMode.Capability,
            installed: string.Equals(capability.State, "Installed", StringComparison.OrdinalIgnoreCase),
            superseded: false);
    }

    private ComponentDefinition ClassifyDriver(ImageDriver driver)
    {
        return Build(
            id: $"driver:{driver.PublishedName}",
            name: driver.PublishedName,
            displayName: driver.OriginalFileName ?? driver.PublishedName,
            description: driver.Provider is null ? null : $"{driver.Provider} · {driver.Class}",
            category: ComponentCategory.Driver,
            sourceType: ComponentSourceType.Driver,
            removalMode: RemovalMode.None,
            installed: true,
            superseded: false);
    }

    private ComponentDefinition Build(
        string id, string name, string displayName, string? description,
        ComponentCategory category, ComponentSourceType sourceType, RemovalMode removalMode,
        bool installed, bool superseded)
    {
        var (risk, protection) = CategoryDefaults.For(category);

        return new ComponentDefinition
        {
            Id = id,
            Name = name,
            DisplayName = displayName,
            Description = description,
            Category = category,
            SourceType = sourceType,
            RemovalMode = removalMode,
            Risk = risk,
            Protection = protection,
            Detected = true,
            Installed = installed,
            Superseded = superseded,
        };
    }

    /// <summary>Primera regla cuyo patrón aparece en el texto (sin distinguir mayúsculas/minúsculas).</summary>
    private ComponentCategory Match(string primaryText, string? secondaryText)
    {
        foreach (var rule in _rules)
        {
            if (Contains(primaryText, rule.Pattern) || Contains(secondaryText, rule.Pattern))
                return rule.Category;
        }

        return ComponentCategory.Unknown;
    }

    private static bool Contains(string? haystack, string needle)
        => !string.IsNullOrEmpty(haystack) &&
           haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);
}
