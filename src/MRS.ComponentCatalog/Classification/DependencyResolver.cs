using MRS.ComponentCatalog.Models;

namespace MRS.ComponentCatalog.Classification;

/// <summary>
/// Primera estructura de dependencias (Parte 7). No resuelve el grafo completo
/// de manifiestos AppX; genera de forma conservadora las aristas hacia los
/// frameworks compartidos realmente presentes en el inventario (VCLibs,
/// UI.Xaml, .NET Native, Windows App Runtime...), que es el caso citado como
/// ejemplo: Microsoft.Paint -&gt; VCLibs -&gt; UI.Xaml.
///
/// La arquitectura queda preparada para dependencias más finas en una fase
/// posterior; el objetivo aquí es que el catálogo NUNCA pueda concluir que un
/// framework compartido "no se usa" solo porque una app concreta sea removible.
/// </summary>
public sealed class DependencyResolver
{
    public IReadOnlyList<ComponentDependency> Resolve(IReadOnlyList<ComponentDefinition> components)
    {
        var frameworks = components
            .Where(c => c.Category == ComponentCategory.Framework)
            .ToList();

        if (frameworks.Count == 0)
            return Array.Empty<ComponentDependency>();

        var dependencies = new List<ComponentDependency>();

        foreach (var component in components)
        {
            if (component.Category == ComponentCategory.Framework)
                continue;

            if (component.SourceType != ComponentSourceType.Appx)
                continue;

            foreach (var framework in frameworks)
            {
                dependencies.Add(new ComponentDependency(component.Id, framework.Id, DependencyType.Framework));
            }
        }

        return dependencies;
    }
}
