namespace MRS.PostInstall.Models;

/// <summary>Nivel de un <see cref="PostInstallProgressInfo"/> (P18). Mismo shape que los otros motores (P10/P16), deliberadamente sin referenciarlos.</summary>
public enum PostInstallProgressLevel
{
    Info,
    Success,
    Warning,
    Error,
}
