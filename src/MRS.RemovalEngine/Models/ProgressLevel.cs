namespace MRS.RemovalEngine.Models;

/// <summary>Nivel visual de un <see cref="ProgressInfo"/>, para que la UI pueda distinguirlos sin acoplarse a la lógica de negocio.</summary>
public enum ProgressLevel
{
    Info,
    Success,
    Warning,
    Error,
}
