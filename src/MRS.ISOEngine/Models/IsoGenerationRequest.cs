using MRS.PostInstall.Models;
using MRS.RemovalPlanning.Models;
using InstallationOptionsModel = MRS.InstallationOptions.Models.InstallationOptions;

namespace MRS.ISOEngine.Models;

/// <summary>
/// Todo lo que <see cref="Pipeline.IIsoGenerationPipeline"/> necesita para
/// generar una ISO (P19): la ISO original, la edición Pro a exportar, las
/// opciones de instalación/OOBE (P15/P16), un <see cref="RemovalPlan"/> ya
/// construido y validado (P06/P07 — este pipeline nunca decide qué eliminar,
/// solo aplica el plan que ya se decidió antes), y la configuración de
/// PostInstall (P18). Cada pieza sigue siendo responsabilidad de su propio
/// motor; este tipo solo las junta para un único punto de entrada.
/// </summary>
public sealed record IsoGenerationRequest
{
    /// <summary>Ruta de la ISO original. Solo se lee; nunca se escribe en ella.</summary>
    public string SourceIsoPath { get; init; } = string.Empty;

    /// <summary>Índice de la edición Pro dentro de <c>sources\install.wim</c>.</summary>
    public int EditionIndex { get; init; }

    public string Architecture { get; init; } = "amd64";

    public InstallationOptionsModel InstallationOptions { get; init; } = InstallationOptionsModel.Default;

    public AutounattendConfiguration AccountConfiguration { get; init; } = new();

    /// <summary>
    /// Plan ya construido (Catálogo -&gt; Protección -&gt; Dependencias -&gt; RemovalPlan,
    /// P05/P06). <c>null</c> es válido: significa "seleccionar la edición Pro sin
    /// eliminar nada" — este pipeline nunca añade eliminaciones por sí mismo.
    /// </summary>
    public RemovalPlan? RemovalPlan { get; init; }

    public PostInstallConfiguration PostInstallConfiguration { get; init; } = new();

    public PostInstallSourceFiles PostInstallSourceFiles { get; init; } = new();

    /// <summary>Ruta del archivo .iso final a producir.</summary>
    public string OutputIsoPath { get; init; } = string.Empty;
}
