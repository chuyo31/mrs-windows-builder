namespace MRS.ProfileEngine.Models;

/// <summary>Error de validación detectado al cargar un archivo de perfil.</summary>
public sealed record ProfileValidationError(
    string FileName,
    string? ProfileId,
    ProfileValidationErrorCode Code,
    string Message);
