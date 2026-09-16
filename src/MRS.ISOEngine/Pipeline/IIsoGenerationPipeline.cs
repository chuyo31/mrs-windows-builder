using MRS.ISOEngine.Models;

namespace MRS.ISOEngine.Pipeline;

/// <summary>
/// Pipeline completo de generación de una ISO (P19):
///
///   ISO ORIGINAL -&gt; VALIDACIÓN -&gt; GENERATION WORKSPACE -&gt; PREPARACIÓN
///   (árbol completo, boot.wim, install.wim, autounattend.xml, PostInstall)
///   -&gt; MODIFICACIÓN BOOT.WIM (LabConfig + Setup/OOBE) -&gt; MODIFICACIÓN
///   INSTALL.WIM (selección Pro + RemovalPlan) -&gt; INTEGRACIÓN POSTINSTALL
///   (.NET + PCPI) -&gt; VALIDACIÓN -&gt; OSCDIMG -&gt; ISO FINAL
///
/// Une las piezas ya implementadas en fases anteriores sin modificar su
/// comportamiento: RemovalEngine, InstallationImageService (P16) y
/// PostInstallPackageBuilder (P18) se invocan tal cual, sin cambios en su
/// lógica interna. No decide qué eliminar (eso es el <c>RemovalPlan</c> que
/// recibe ya construido) ni implementa ningún bypass nuevo.
/// </summary>
public interface IIsoGenerationPipeline
{
    Task<IsoGenerationResult> GenerateAsync(
        IsoGenerationRequest request, CancellationToken cancellationToken = default,
        IProgress<InstallationProgressInfo>? progress = null);
}
