namespace MRS.ComponentCatalog.Models;

/// <summary>
/// Arista del grafo de dependencias entre dos <see cref="ComponentDefinition"/>.
/// Ejemplo: Microsoft.Paint --Framework--&gt; VCLibs --Framework--&gt; UI.Xaml.
/// El catálogo no resuelve todavía el grafo completo, pero la estructura ya
/// existe para que una fase posterior lo use al decidir qué se puede eliminar.
/// </summary>
public sealed record ComponentDependency(
    string SourceComponentId,
    string TargetComponentId,
    DependencyType DependencyType);
