using MRS.DismEngine.Processes;

namespace MRS.DismEngine.Dism;

/// <summary>
/// Invoca DISM.exe de forma controlada. Todas las operaciones son de solo
/// lectura o de montaje temporal; ninguna modifica la imagen de forma
/// permanente. Devuelve siempre el <see cref="ProcessRunResult"/> crudo para que
/// la capa superior decida en función del código de salida.
/// </summary>
public interface IDismRunner
{
    // --- Información de la imagen (sin montar) -------------------------------

    Task<ProcessRunResult> GetWimInfoAsync(string imagePath, CancellationToken cancellationToken = default);
    Task<ProcessRunResult> GetWimInfoAsync(string imagePath, int index, CancellationToken cancellationToken = default);

    // --- Montaje temporal del WIM ----------------------------------------

    /// <summary><c>DISM /English /Get-MountedWimInfo</c>.</summary>
    Task<ProcessRunResult> GetMountedWimInfoAsync(CancellationToken cancellationToken = default);

    /// <summary><c>DISM /English /Mount-Wim /WimFile:.. /Index:N /MountDir:.. /ReadOnly</c>.</summary>
    Task<ProcessRunResult> MountWimAsync(
        string wimFile, int index, string mountDir, bool readOnly = true,
        CancellationToken cancellationToken = default);

    /// <summary>Desmonta descartando cambios (<c>/Unmount-Wim /Discard</c>).</summary>
    Task<ProcessRunResult> UnmountWimDiscardAsync(string mountDir, CancellationToken cancellationToken = default);

    // --- Inventario de una imagen montada (solo lectura) ------------------

    Task<ProcessRunResult> GetPackagesAsync(string mountDir, CancellationToken cancellationToken = default);
    Task<ProcessRunResult> GetFeaturesAsync(string mountDir, CancellationToken cancellationToken = default);
    Task<ProcessRunResult> GetCapabilitiesAsync(string mountDir, CancellationToken cancellationToken = default);
    Task<ProcessRunResult> GetProvisionedAppxPackagesAsync(string mountDir, CancellationToken cancellationToken = default);
    Task<ProcessRunResult> GetDriversAsync(string mountDir, CancellationToken cancellationToken = default);
}
