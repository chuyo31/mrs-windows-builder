namespace MRS.ComponentCatalog.Models;

/// <summary>Origen del inventario del que procede el componente.</summary>
public enum ComponentSourceType
{
    Unknown,
    Package,
    Appx,
    Feature,
    Capability,
    Driver,
}
