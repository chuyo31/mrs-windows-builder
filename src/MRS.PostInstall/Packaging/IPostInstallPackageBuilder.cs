using MRS.PostInstall.Models;

namespace MRS.PostInstall.Packaging;

/// <summary>
/// Extraída en P19 para que <c>MRS.ISOEngine</c> pueda integrar el paquete
/// PostInstall en su pipeline sin depender de la clase concreta (mismo patrón
/// que el resto de motores del proyecto: <c>IRemovalEngine</c>,
/// <c>IBootWimModifier</c>, <c>IInstallationImageService</c>...). No cambia
/// ningún comportamiento de P18.
/// </summary>
public interface IPostInstallPackageBuilder
{
    PostInstallPackageResult Build(
        PostInstallConfiguration config, PostInstallSourceFiles sourceFiles, string outputDirectory,
        IProgress<PostInstallProgressInfo>? progress = null);
}
