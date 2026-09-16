namespace MRS.ISOEngine.Pipeline;

/// <summary>
/// Resultado de la comprobación de entorno de P20, sección 1: privilegios de
/// administrador (necesarios para DISM) y disponibilidad de oscdimg (Windows
/// ADK). Si falla cualquiera de las dos, el pipeline debe abortar antes de
/// crear un workspace o copiar el árbol de la ISO.
/// </summary>
public sealed record EnvironmentPreflightResult(bool IsReady, IReadOnlyList<string> Errors)
{
    public static readonly EnvironmentPreflightResult Ready = new(true, Array.Empty<string>());
}
