using MRS.ComponentCatalog.Classification;
using MRS.ComponentCatalog.Models;
using MRS.ComponentCatalog.Rules;
using MRS.ImageEngine.Models;

namespace MRS.ComponentCatalog;

/// <summary>
/// Punto de entrada del catálogo. NO ejecuta DISM ni toca la imagen: solo
/// transforma un <see cref="ImageInventory"/> ya obtenido en un catálogo
/// clasificado y protegido.
///
///   Inventory -> Catalog -> Protection -> (selección de usuario, fase futura)
/// </summary>
public sealed class ComponentCatalogService
{
    private readonly CatalogClassifier _classifier;
    private readonly ProtectionEngine _protection;
    private readonly DependencyResolver _dependencies;

    public ComponentCatalogService(CatalogRuleSet? ruleSet = null)
    {
        _classifier = new CatalogClassifier(ruleSet);
        _protection = new ProtectionEngine(ruleSet);
        _dependencies = new DependencyResolver();
    }

    /// <summary>
    /// <paramref name="securityOptions"/> (P13) decide si Defender/Windows Update
    /// se mantienen protegidos; <c>null</c> equivale a <see cref="SecurityOptions.Safe"/>
    /// (comportamiento idéntico al anterior a P13). No ejecuta DISM ni toca la imagen:
    /// es una recomputación en memoria, segura de repetir cada vez que cambie la
    /// configuración de seguridad del perfil activo.
    /// </summary>
    public ComponentCatalogResult BuildCatalog(ImageInventory inventory, SecurityOptions? securityOptions = null)
    {
        ArgumentNullException.ThrowIfNull(inventory);

        var classified = _classifier.Classify(inventory);
        var protectedComponents = _protection.ApplyAll(classified, securityOptions);
        var dependencies = _dependencies.Resolve(protectedComponents);

        var withDependencies = protectedComponents
            .Select(c => c with
            {
                Dependencies = dependencies.Where(d => d.SourceComponentId == c.Id).ToList(),
            })
            .ToList();

        return new ComponentCatalogResult
        {
            Components = withDependencies,
            Dependencies = dependencies,
        };
    }
}
