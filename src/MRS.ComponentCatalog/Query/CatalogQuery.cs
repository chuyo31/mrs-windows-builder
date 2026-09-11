using MRS.ComponentCatalog.Models;

namespace MRS.ComponentCatalog.Query;

/// <summary>
/// Búsqueda y filtros sobre el catálogo (Partes 13/14). Sin dependencias de UI,
/// para poder usarse igual desde la app o desde los tests.
/// </summary>
public static class CatalogQuery
{
    /// <summary>Busca por Id, Name, DisplayName, categoría o tags (sin distinguir mayúsculas/minúsculas).</summary>
    public static IEnumerable<ComponentDefinition> Search(
        IEnumerable<ComponentDefinition> components, string? term)
    {
        if (string.IsNullOrWhiteSpace(term))
            return components;

        var trimmed = term.Trim();
        return components.Where(c => Matches(c, trimmed));
    }

    public static IEnumerable<ComponentDefinition> Filter(
        IEnumerable<ComponentDefinition> components, CatalogFilterKind filter) => filter switch
    {
        CatalogFilterKind.All => components,
        CatalogFilterKind.Protected => components.Where(c => c.Protection == ComponentProtection.Protected),
        CatalogFilterKind.Removable => components.Where(c => c.Protection == ComponentProtection.Removable),
        CatalogFilterKind.Optional => components.Where(c => c.Protection == ComponentProtection.Optional),
        CatalogFilterKind.Critical => components.Where(c => c.Risk == ComponentRisk.Critical),
        CatalogFilterKind.Applications => components.Where(c => c.Category == ComponentCategory.Application),
        CatalogFilterKind.Features => components.Where(c => c.SourceType == ComponentSourceType.Feature),
        CatalogFilterKind.Capabilities => components.Where(c => c.SourceType == ComponentSourceType.Capability),
        CatalogFilterKind.Frameworks => components.Where(c => c.Category == ComponentCategory.Framework),
        CatalogFilterKind.Gaming => components.Where(c => c.Category == ComponentCategory.Gaming),
        CatalogFilterKind.Communication => components.Where(c => c.Category == ComponentCategory.Communication),
        CatalogFilterKind.AI => components.Where(c => c.Category == ComponentCategory.AI),
        CatalogFilterKind.Telemetry => components.Where(c => c.Category == ComponentCategory.Telemetry),
        _ => components,
    };

    private static bool Matches(ComponentDefinition c, string term)
        => Contains(c.Id, term)
           || Contains(c.Name, term)
           || Contains(c.DisplayName, term)
           || Contains(c.Category.ToString(), term)
           || c.Tags.Any(tag => Contains(tag, term));

    private static bool Contains(string? value, string term)
        => !string.IsNullOrEmpty(value) && value.Contains(term, StringComparison.OrdinalIgnoreCase);
}
