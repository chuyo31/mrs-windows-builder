namespace MRS.ComponentCatalog.Models;

/// <summary>Naturaleza de una dependencia entre dos componentes.</summary>
public enum DependencyType
{
    Optional,
    Runtime,
    Framework,
    Required,
    System,
}
