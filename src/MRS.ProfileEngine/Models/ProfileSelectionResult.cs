namespace MRS.ProfileEngine.Models;

/// <summary>
/// Selección de ComponentId que produce un perfil al aplicarlo sobre un
/// catálogo real. Es puramente informativa: quien la use sigue teniendo que
/// pasar <see cref="SelectedComponentIds"/> por Catalog + ProtectionEngine +
/// RemovalPlanning antes de que se traduzca en ninguna eliminación real.
/// </summary>
public sealed record ProfileSelectionResult(
    string ProfileId,
    IReadOnlyList<string> SelectedComponentIds,
    /// <summary>ComponentId del perfil que no existen en el catálogo actual (nunca se seleccionan).</summary>
    IReadOnlyList<string> UnknownComponentIds,
    /// <summary>
    /// ComponentId del perfil que coinciden con un componente marcado como protegido por el
    /// caller. Es solo informativo: ProfileEngine nunca decide protección, y la decisión final
    /// siempre corresponde a ProtectionEngine/RemovalPlanning cuando se construya el RemovalPlan.
    /// </summary>
    IReadOnlyList<string> BlockedComponentIds);
