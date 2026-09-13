using MRS.ProfileEngine.Models;

namespace MRS.ProfileEngine;

/// <summary>
/// Carga y consulta perfiles de eliminación definidos en JSON. No conoce
/// DISM, WPF ni el catálogo real: trabaja únicamente con
/// <see cref="ProfileDefinition"/> y listas de ComponentId como texto.
/// </summary>
public interface IProfileService
{
    /// <summary>
    /// Carga todos los <c>*.json</c> de <paramref name="directoryPath"/>. Un archivo
    /// inválido (JSON malformado, sin <c>id</c>, id duplicado o versión inválida) se
    /// reporta como error y no impide cargar el resto.
    /// </summary>
    ProfileLoadResult LoadFromDirectory(string directoryPath);

    /// <summary>Busca un perfil por Id (sin distinguir mayúsculas/minúsculas) dentro de un resultado ya cargado.</summary>
    ProfileDefinition? GetProfile(ProfileLoadResult loadResult, string profileId);

    /// <summary>
    /// Calcula la selección de ComponentId que produciría <paramref name="profile"/>.
    /// Si se pasa <paramref name="knownComponentIds"/>, cualquier ComponentId del
    /// perfil que no exista en esa colección se reporta como desconocido y NUNCA se
    /// selecciona. Si se pasa <paramref name="protectedComponentIds"/>, los
    /// ComponentId seleccionados que coincidan se reportan como bloqueados
    /// (a título informativo: la protección real la decide ProtectionEngine).
    /// </summary>
    ProfileSelectionResult GetSelection(
        ProfileDefinition profile,
        IReadOnlyCollection<string>? knownComponentIds = null,
        IReadOnlyCollection<string>? protectedComponentIds = null);
}
