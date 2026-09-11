using MRS.ComponentCatalog.Models;

namespace MRS.RemovalPlanning.Tests;

/// <summary>Construye <see cref="ComponentDefinition"/> y catálogos de prueba sin pasar por DISM ni por una ISO real.</summary>
internal static class TestComponents
{
    public static ComponentDefinition Package(
        string id, string name,
        ComponentProtection protection = ComponentProtection.Removable,
        bool installed = true, bool superseded = false,
        ComponentRisk risk = ComponentRisk.Low,
        string? protectionReason = null,
        ComponentCategory category = ComponentCategory.Application) => new()
    {
        Id = $"package:{id}",
        Name = name,
        DisplayName = name,
        Category = category,
        SourceType = ComponentSourceType.Package,
        Protection = protection,
        Risk = risk,
        Installed = installed,
        Superseded = superseded,
        ProtectionReason = protectionReason,
    };

    public static ComponentDefinition Appx(
        string id, string name,
        ComponentProtection protection = ComponentProtection.Removable,
        ComponentCategory category = ComponentCategory.Application,
        string? protectionReason = null) => new()
    {
        Id = $"appx:{id}",
        Name = name,
        DisplayName = name,
        Category = category,
        SourceType = ComponentSourceType.Appx,
        Protection = protection,
        Installed = true,
        ProtectionReason = protectionReason,
    };

    public static ComponentDefinition Feature(
        string id, string name,
        ComponentProtection protection,
        bool installed = true) => new()
    {
        Id = $"feature:{id}",
        Name = name,
        DisplayName = name,
        Category = ComponentCategory.Feature,
        SourceType = ComponentSourceType.Feature,
        Protection = protection,
        Installed = installed,
    };

    public static ComponentDefinition Capability(
        string id, string name,
        ComponentProtection protection,
        bool installed = true) => new()
    {
        Id = $"capability:{id}",
        Name = name,
        DisplayName = name,
        Category = ComponentCategory.Capability,
        SourceType = ComponentSourceType.Capability,
        Protection = protection,
        Installed = installed,
    };

    public static ComponentDefinition Driver(string id, string name) => new()
    {
        Id = $"driver:{id}",
        Name = name,
        DisplayName = name,
        Category = ComponentCategory.Driver,
        SourceType = ComponentSourceType.Driver,
        Protection = ComponentProtection.Recommended,
        Installed = true,
    };

    public static ComponentCatalogResult Catalog(
        IReadOnlyList<ComponentDefinition> components,
        IReadOnlyList<ComponentDependency>? dependencies = null)
        => new() { Components = components, Dependencies = dependencies ?? Array.Empty<ComponentDependency>() };
}
