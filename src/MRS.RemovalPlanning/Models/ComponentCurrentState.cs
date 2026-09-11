namespace MRS.RemovalPlanning.Models;

/// <summary>
/// Estado DISM del componente, visto desde el motor de selección. No debe
/// confundirse con <see cref="MRS.ComponentCatalog.Models.ComponentProtection"/>:
/// esto es "qué hay instalado", la protección es "qué se puede tocar".
/// </summary>
public enum ComponentCurrentState
{
    Installed,
    Superseded,
    NotPresent,
    Unknown,
}
