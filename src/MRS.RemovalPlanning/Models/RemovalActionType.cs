namespace MRS.RemovalPlanning.Models;

/// <summary>
/// Qué HARÍAMOS con un componente (no confundir con qué ES el componente,
/// eso es <see cref="MRS.ComponentCatalog.Models.ComponentDefinition"/>).
/// Puramente descriptivo en esta fase: ninguna acción se ejecuta.
/// </summary>
public enum RemovalActionType
{
    None,
    RemovePackage,
    RemoveAppx,
    DisableFeature,
    RemoveCapability,
    RemoveService,
    RemoveTask,
    RemoveRegistry,
    RemoveFile,
    Unknown,
}
