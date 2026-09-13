namespace MRS.ISOEngine.Models;

/// <summary>
/// Configuración para <c>AutounattendGenerator</c> (P16, sección 5): cuenta local
/// a crear durante OOBE. Deliberadamente separada de <c>InstallationOptions</c>
/// (que solo dice SI se quiere cuenta local, no el nombre/contraseña) y de
/// cualquier servicio de ISO/Workspace/DISM.
///
/// <see cref="Password"/> es opcional y nunca tiene un valor por defecto no
/// vacío: no se codifica ninguna contraseña real en el proyecto ni se guarda en
/// el repositorio; quien construya esta configuración en tiempo de ejecución
/// decide si la rellena.
/// </summary>
public sealed record AutounattendConfiguration
{
    public string AccountName { get; init; } = "Usuario";

    /// <summary><c>null</c> o vacío = sin contraseña (cuenta local sin contraseña, como pide "contraseña opcional").</summary>
    public string? Password { get; init; }

    public string ComputerName { get; init; } = "MRS-PC";
}
