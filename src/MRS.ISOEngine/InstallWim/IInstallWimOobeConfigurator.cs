using MRS.ISOEngine.Models;

namespace MRS.ISOEngine.InstallWim;

/// <summary>
/// Ciclo transaccional sobre la imagen de trabajo de <c>install.wim</c> (P29):
/// Mount-Wim (ReadWrite) → cargar hive SYSTEM offline → aplicar BypassNRO →
/// verificar → descargar hive → Commit si todo fue bien, Discard en cualquier
/// otro caso. Mismo patrón que <c>MRS.ISOEngine.BootWim.IBootWimModifier</c>,
/// aplicado a install.wim en vez de boot.wim: el BypassNRO offline de boot.wim
/// (P28) no es suficiente por sí solo -- el estado tiene que existir también en
/// el SYSTEM hive del Windows YA INSTALADO antes de llegar a OOBE.
/// </summary>
public interface IInstallWimOobeConfigurator
{
    Task<InstallWimOobeConfigurationResult> ApplyOfflineOobeBypassAsync(
        string installWimPath, int index, string mountDir,
        CancellationToken cancellationToken = default,
        IProgress<InstallationProgressInfo>? progress = null);
}
