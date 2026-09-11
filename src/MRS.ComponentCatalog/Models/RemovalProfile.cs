namespace MRS.ComponentCatalog.Models;

/// <summary>
/// Perfiles de eliminación previstos. Se definen aquí para que el modelo del
/// catálogo esté preparado, pero los perfiles en sí se implementan en una fase
/// posterior: <see cref="ComponentDefinition.SuggestedProfiles"/> no se rellena
/// todavía.
/// </summary>
public enum RemovalProfile
{
    Normal,
    Light,
    Medium,
    Ultra,
    Custom,
}
