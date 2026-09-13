namespace MRS.ComponentCatalog.Models;

/// <summary>
/// Decisión explícita (P13) sobre si Microsoft Defender y Windows Update deben
/// seguir protegidos al construir el catálogo. Por defecto ambos se mantienen
/// (<see cref="Safe"/>): un JSON de perfil antiguo, o cualquier llamada que no
/// pase esta opción, se comporta exactamente igual que antes de P13.
/// </summary>
public sealed record SecurityOptions
{
    public bool KeepDefender { get; init; } = true;

    public bool KeepWindowsUpdate { get; init; } = true;

    public static SecurityOptions Safe { get; } = new();
}
