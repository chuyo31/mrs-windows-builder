namespace MRS.ISOEngine.Models;

/// <summary>
/// Configuración para <c>AutounattendGenerator</c> (P16, sección 5): cuenta local
/// a crear durante OOBE. Deliberadamente separada de <c>InstallationOptions</c>
/// (que solo dice SI se quiere cuenta local, no el nombre/contraseña) y de
/// cualquier servicio de ISO/Workspace/DISM.
///
/// <see cref="AccountName"/> no tiene ningún valor por defecto (P30): el nombre
/// lo elige siempre quien use la aplicación, nunca "Usuario" ni ningún otro
/// nombre fijo -- un <see cref="AccountName"/> vacío es intencionadamente
/// inválido (ver <c>AutounattendGenerator.Validate</c>), para forzar que se
/// proporcione uno explícito en vez de generar silenciosamente una cuenta con
/// un nombre que nadie pidió.
///
/// <see cref="Password"/> es opcional y nunca tiene un valor por defecto no
/// vacío: no se codifica ninguna contraseña real en el proyecto ni se guarda en
/// el repositorio; quien construya esta configuración en tiempo de ejecución
/// decide si la rellena. <see cref="ConfirmPassword"/> (P30) existe solo para
/// que <c>Validate</c> pueda comprobar que ambas coinciden -- nunca se escribe
/// en el autounattend.xml generado, ni se persiste más allá de esta comprobación.
/// </summary>
public sealed record AutounattendConfiguration
{
    public string AccountName { get; init; } = string.Empty;

    /// <summary><c>null</c> o vacío = sin contraseña (cuenta local sin contraseña, como pide "contraseña opcional").</summary>
    public string? Password { get; init; }

    /// <summary>
    /// Solo para validación (P30): debe coincidir exactamente con
    /// <see cref="Password"/> cuando este no está vacío, y estar vacío cuando
    /// <see cref="Password"/> también lo está. Nunca se incluye en el XML
    /// generado por <c>AutounattendGenerator.Generate</c>.
    /// </summary>
    public string? ConfirmPassword { get; init; }

    public string ComputerName { get; init; } = "MRS-PC";
}
