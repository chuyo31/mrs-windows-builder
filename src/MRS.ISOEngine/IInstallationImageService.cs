using MRS.ISOEngine.Models;
using InstallationOptionsModel = MRS.InstallationOptions.Models.InstallationOptions;

namespace MRS.ISOEngine;

/// <summary>
/// Servicio principal de P16 (sección 10):
///
///   InstallationOptions -> InstallationConfigurationPlanner -> InstallationImageService -> BootWimModifier -> AutounattendGenerator
///
/// Recibe la ISO de origen, un <see cref="GenerationWorkspace"/> ya creado y las
/// <see cref="InstallationOptionsModel"/> elegidas, y ejecuta solo las
/// modificaciones correspondientes: nunca toca la ISO original, siempre trabaja
/// sobre la copia de <c>boot.wim</c> del workspace. Si
/// <see cref="InstallationOptionsModel.BypassStorage"/> está activado, aborta
/// antes de tocar nada (ver <c>Configuration.InstallationExecutionValidator</c>).
/// </summary>
public interface IInstallationImageService
{
    Task<InstallationImageResult> ApplyAsync(
        string sourceIsoPath, GenerationWorkspace workspace, InstallationOptionsModel options,
        AutounattendConfiguration accountConfig, CancellationToken cancellationToken = default,
        IProgress<InstallationProgressInfo>? progress = null);
}
