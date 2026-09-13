namespace MRS.ProfileEngine.Models;

/// <summary>
/// Configuración de seguridad de un perfil (P13): si debe mantenerse Microsoft
/// Defender y Windows Update. Por defecto ambos se mantienen (<see cref="Safe"/>);
/// un JSON de perfil que no declare <c>securityOptions</c> usa estos mismos
/// valores por defecto, nunca valores inseguros por omisión.
///
/// Deliberadamente es un tipo propio de <c>MRS.ProfileEngine</c> (no una
/// referencia a <c>MRS.ComponentCatalog.Models.SecurityOptions</c>): ProfileEngine
/// no referencia ningún otro proyecto (ver P11), así que este pequeño valor de
/// datos (dos booleanos) se define aquí también. Quien integra ambos mundos
/// (la UI) traduce uno al otro explícitamente.
/// </summary>
public sealed record SecurityOptions
{
    public bool KeepDefender { get; init; } = true;

    public bool KeepWindowsUpdate { get; init; } = true;

    public static SecurityOptions Safe { get; } = new();
}
