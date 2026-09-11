namespace MRS.ComponentCatalog.Models;

/// <summary>
/// Nivel de protección frente a eliminación. Independiente del <see cref="ComponentRisk"/>
/// y del estado DISM (Installed/Superseded): un componente puede estar
/// "Installed + Protected" o "Superseded + Removable", por ejemplo.
/// </summary>
public enum ComponentProtection
{
    Unknown,
    Removable,
    Optional,
    Recommended,
    Protected,
}
