namespace MRS.ComponentCatalog.Models;

/// <summary>
/// Mecanismo que se usaría para eliminar el componente. En esta fase es
/// puramente DESCRIPTIVO: el catálogo no ejecuta ninguna eliminación.
/// </summary>
public enum RemovalMode
{
    None,
    Package,
    Appx,
    Feature,
    Capability,
    Registry,
    FileSystem,
    Task,
    Service,
}
