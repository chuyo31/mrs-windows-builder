namespace MRS.ISOEngine.Models;

/// <summary>Nivel de un <see cref="InstallationProgressInfo"/> (P16). Mismo shape que MRS.RemovalEngine.Models.ProgressLevel (P10), deliberadamente sin referenciarlo (ver prompts/16-resultado.md).</summary>
public enum InstallationProgressLevel
{
    Info,
    Success,
    Warning,
    Error,
}
