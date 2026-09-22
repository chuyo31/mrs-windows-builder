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

    /// <summary>
    /// P28: re-verifica, de forma independiente y DESPUÉS del commit de
    /// <see cref="ApplyAsync"/>, que boot.wim (índices 1 y 2) y autounattend.xml
    /// quedaron realmente como se pidió — vuelve a montar de solo lectura y a
    /// leer el registro/XML, en vez de confiar en el resultado ya reportado por
    /// <see cref="ApplyAsync"/>. Pensada para la fase "Validación final" del
    /// pipeline (P19), nunca se llama antes de que <see cref="ApplyAsync"/> haya
    /// terminado con éxito.
    /// </summary>
    Task<WorkspaceValidationResult> ValidateFinalAsync(
        GenerationWorkspace workspace, InstallationOptionsModel options, CancellationToken cancellationToken = default);
}
