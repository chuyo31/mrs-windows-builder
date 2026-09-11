using MRS.DismEngine.Processes;

namespace MRS.DismEngine.Dism;

/// <summary>
/// Invoca DISM.exe de forma controlada. Devuelve siempre el
/// <see cref="ProcessRunResult"/> crudo para que la capa superior decida en
/// función del código de salida. La mayoría de operaciones son de solo lectura
/// o de montaje temporal; las de la sección "Modificación" (fase 7) sí pueden
/// cambiar una imagen de trabajo (nunca la ISO original).
/// </summary>
public interface IDismRunner
{
    // --- Información de la imagen (sin montar) -------------------------------

    Task<ProcessRunResult> GetWimInfoAsync(string imagePath, CancellationToken cancellationToken = default);
    Task<ProcessRunResult> GetWimInfoAsync(string imagePath, int index, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>DISM /English /Export-Image /SourceImageFile:.. /SourceIndex:N /DestinationImageFile:..</c>.
    /// Crea una imagen de trabajo independiente a partir de una edición concreta
    /// del origen; el origen nunca se abre en modo escritura.
    /// </summary>
    Task<ProcessRunResult> ExportImageAsync(
        string sourceImageFile, int sourceIndex, string destinationImageFile, string? destinationName = null,
        CancellationToken cancellationToken = default);

    // --- Montaje temporal del WIM ----------------------------------------

    /// <summary><c>DISM /English /Get-MountedWimInfo</c>.</summary>
    Task<ProcessRunResult> GetMountedWimInfoAsync(CancellationToken cancellationToken = default);

    /// <summary><c>DISM /English /Mount-Wim /WimFile:.. /Index:N /MountDir:.. [/ReadOnly]</c>.</summary>
    Task<ProcessRunResult> MountWimAsync(
        string wimFile, int index, string mountDir, bool readOnly = true,
        CancellationToken cancellationToken = default);

    /// <summary>Desmonta descartando cambios (<c>/Unmount-Wim /Discard</c>).</summary>
    Task<ProcessRunResult> UnmountWimDiscardAsync(string mountDir, CancellationToken cancellationToken = default);

    /// <summary>Desmonta confirmando cambios (<c>/Unmount-Wim /Commit</c>). Solo para imágenes de trabajo montadas en escritura.</summary>
    Task<ProcessRunResult> UnmountWimCommitAsync(string mountDir, CancellationToken cancellationToken = default);

    // --- Inventario de una imagen montada (solo lectura) ------------------

    Task<ProcessRunResult> GetPackagesAsync(string mountDir, CancellationToken cancellationToken = default);
    Task<ProcessRunResult> GetFeaturesAsync(string mountDir, CancellationToken cancellationToken = default);
    Task<ProcessRunResult> GetCapabilitiesAsync(string mountDir, CancellationToken cancellationToken = default);
    Task<ProcessRunResult> GetProvisionedAppxPackagesAsync(string mountDir, CancellationToken cancellationToken = default);
    Task<ProcessRunResult> GetDriversAsync(string mountDir, CancellationToken cancellationToken = default);

    // --- Modificación de una imagen de trabajo montada en escritura (fase 7) ---

    /// <summary><c>DISM /English /Image:.. /Remove-ProvisionedAppxPackage /PackageName:..</c>.</summary>
    Task<ProcessRunResult> RemoveProvisionedAppxPackageAsync(string mountDir, string packageName, CancellationToken cancellationToken = default);

    /// <summary><c>DISM /English /Image:.. /Disable-Feature /FeatureName:..</c> (nunca <c>/Remove</c> en esta fase).</summary>
    Task<ProcessRunResult> DisableFeatureAsync(string mountDir, string featureName, CancellationToken cancellationToken = default);

    /// <summary><c>DISM /English /Image:.. /Remove-Capability /CapabilityName:..</c>.</summary>
    Task<ProcessRunResult> RemoveCapabilityAsync(string mountDir, string capabilityName, CancellationToken cancellationToken = default);

    /// <summary><c>DISM /English /Image:.. /Remove-Package /PackageName:..</c>. Nunca /ResetBase ni /Cleanup-Image.</summary>
    Task<ProcessRunResult> RemovePackageAsync(string mountDir, string packageIdentity, CancellationToken cancellationToken = default);
}
