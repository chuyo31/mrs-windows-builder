namespace MRS.ProfileEngine.Models;

/// <summary>Motivo por el que un perfil (o el directorio de perfiles) no se pudo cargar.</summary>
public enum ProfileValidationErrorCode
{
    DirectoryNotFound,
    InvalidJson,
    MissingId,
    DuplicateId,
    InvalidVersion,
}
