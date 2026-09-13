namespace MRS.ProfileEngine.Models;

/// <summary>
/// Resultado de cargar un directorio de perfiles: los perfiles válidos que se
/// pudieron leer y los errores de validación encontrados (un archivo inválido
/// no impide cargar el resto).
/// </summary>
public sealed record ProfileLoadResult(
    IReadOnlyList<ProfileDefinition> Profiles,
    IReadOnlyList<ProfileValidationError> Errors)
{
    public bool HasErrors => Errors.Count > 0;
}
