namespace MRS.ComponentCatalog.Models;

/// <summary>
/// Catálogo construido a partir de UN inventario concreto. No es una lista fija:
/// una ISO distinta produce un catálogo distinto, porque solo contiene lo que
/// realmente se detectó.
/// </summary>
public sealed record ComponentCatalogResult
{
    public IReadOnlyList<ComponentDefinition> Components { get; init; } = Array.Empty<ComponentDefinition>();

    public IReadOnlyList<ComponentDependency> Dependencies { get; init; } = Array.Empty<ComponentDependency>();

    public int TotalCount => Components.Count;
    public int ProtectedCount => Components.Count(c => c.Protection == ComponentProtection.Protected);
    public int RemovableCount => Components.Count(c => c.Protection == ComponentProtection.Removable);
    public int OptionalCount => Components.Count(c => c.Protection == ComponentProtection.Optional);
    public int CriticalCount => Components.Count(c => c.Risk == ComponentRisk.Critical);

    /// <summary>Componentes de los que <paramref name="componentId"/> depende.</summary>
    public IEnumerable<ComponentDefinition> DependenciesOf(string componentId)
        => Dependencies
            .Where(d => d.SourceComponentId == componentId)
            .Join(Components, d => d.TargetComponentId, c => c.Id, (_, c) => c);

    /// <summary>Componentes que dependen de <paramref name="componentId"/> ("utilizado por").</summary>
    public IEnumerable<ComponentDefinition> DependentsOf(string componentId)
        => Dependencies
            .Where(d => d.TargetComponentId == componentId)
            .Join(Components, d => d.SourceComponentId, c => c.Id, (_, c) => c);
}
