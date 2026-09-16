using MRS.PostInstall.Models;
using MRS.PostInstall.Packaging;

namespace MRS.ISOEngine.Tests.Fakes;

internal sealed class FakePostInstallPackageBuilder : IPostInstallPackageBuilder
{
    public bool Success { get; set; } = true;
    public IReadOnlyList<string> Errors { get; set; } = Array.Empty<string>();
    public int CallCount { get; private set; }
    public string? LastOutputDirectory { get; private set; }

    /// <summary>Si se define, crea este contenido dentro del OemRootPath devuelto (simula un $OEM$ real generado).</summary>
    public Action<string>? OnBuild { get; set; }

    public PostInstallPackageResult Build(
        PostInstallConfiguration config, PostInstallSourceFiles sourceFiles, string outputDirectory,
        IProgress<PostInstallProgressInfo>? progress = null)
    {
        CallCount++;
        LastOutputDirectory = outputDirectory;

        if (!Success)
            return new PostInstallPackageResult(false, null, Errors);

        if (!config.Enabled)
            return new PostInstallPackageResult(true, null, Array.Empty<string>());

        var oemRoot = Path.Combine(outputDirectory, "$OEM$");
        OnBuild?.Invoke(oemRoot);

        return new PostInstallPackageResult(true, oemRoot, Array.Empty<string>());
    }
}
