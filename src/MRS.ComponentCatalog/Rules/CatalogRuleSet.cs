namespace MRS.ComponentCatalog.Rules;

/// <summary>Conjunto de reglas de clasificación y protección usado por el catálogo.</summary>
public sealed record CatalogRuleSet(
    IReadOnlyList<ClassificationRule> ClassificationRules,
    IReadOnlyList<ProtectionRule> ProtectionRules)
{
    public static CatalogRuleSet Empty { get; } =
        new(Array.Empty<ClassificationRule>(), Array.Empty<ProtectionRule>());
}
