using System.Security.Principal;

namespace MRS.ISOEngine.Pipeline;

/// <summary>
/// Implementación real de <see cref="IElevationChecker"/> basada en
/// <see cref="WindowsPrincipal"/>. DISM exige privilegios de administrador
/// incluso para operaciones de solo lectura (confirmado empíricamente en P16,
/// P17, P19 y P20: /Get-WimInfo y /Get-MountedImageInfo devuelven
/// "Error: 740" sin elevación), por lo que comprobarlo aquí, antes de tocar
/// cualquier WIM, evita que el pipeline falle de forma confusa a mitad de
/// PrepareBootWim o PrepareInstallWim.
/// </summary>
public sealed class ElevationChecker : IElevationChecker
{
    public bool IsElevated()
    {
        if (!OperatingSystem.IsWindows())
            return false;

        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
}
