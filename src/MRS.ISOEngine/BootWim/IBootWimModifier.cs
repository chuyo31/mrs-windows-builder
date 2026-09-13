using MRS.ISOEngine.Models;
using InstallationOptionsModel = MRS.InstallationOptions.Models.InstallationOptions;

namespace MRS.ISOEngine.BootWim;

/// <summary>
/// Ciclo transaccional sobre una copia de <c>boot.wim</c> (P16, sección 2):
/// Mount-Wim (ReadWrite) → cargar hive SYSTEM offline → aplicar/retirar LabConfig
/// → verificar → descargar hive → Commit si todo fue bien, Discard en cualquier
/// otro caso. Nunca deja un hive cargado ni un mount abierto al finalizar, incluso
/// si una operación intermedia lanza una excepción.
/// </summary>
public interface IBootWimModifier
{
    Task<BootWimModificationResult> ApplyLabConfigAsync(
        string bootWimPath, int index, InstallationOptionsModel options, string mountDir,
        CancellationToken cancellationToken = default,
        IProgress<InstallationProgressInfo>? progress = null);
}
