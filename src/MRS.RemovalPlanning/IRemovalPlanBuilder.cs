using MRS.ComponentCatalog.Models;
using MRS.ImageEngine.Models;
using MRS.RemovalPlanning.Models;

namespace MRS.RemovalPlanning;

/// <summary>
///   ComponentCatalog -&gt; RemovalPlanBuilder -&gt; RemovalPlan -&gt; (futuro) RemovalEngine -&gt; DismRunner
///
/// El builder no referencia <c>MRS.DismEngine</c> ni ejecuta nada: solo
/// transforma una selección en un plan verificable.
/// </summary>
public interface IRemovalPlanBuilder
{
    RemovalPlan Build(
        ImageInventory inventory,
        ComponentCatalogResult catalog,
        IReadOnlyCollection<ComponentSelection> selections,
        string? imageId = null);
}
