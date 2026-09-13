using MRS.PostInstall.Configuration;
using MRS.PostInstall.Models;

namespace MRS.PostInstall.Packaging;

/// <summary>
/// Validación previa a empaquetar (P18, sección 13): configuración coherente
/// (<see cref="PostInstallConfigurationValidator"/>) + existencia real de los
/// dos instaladores. Si falta .NET o PCPI, el resultado es inválido — el
/// llamador (<c>PostInstallPackageBuilder</c>) debe abortar, nunca generar un
/// paquete incompleto.
/// </summary>
public static class PostInstallPackageValidator
{
    public static PostInstallValidationResult Validate(PostInstallConfiguration config, PostInstallSourceFiles sourceFiles)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(sourceFiles);

        var configResult = PostInstallConfigurationValidator.Validate(config);
        if (!config.Enabled)
            return configResult; // deshabilitado: nada más que comprobar (ya es Valid).

        var errors = new List<string>(configResult.Errors);

        if (string.IsNullOrWhiteSpace(sourceFiles.DotNetInstallerPath) || !File.Exists(sourceFiles.DotNetInstallerPath))
            errors.Add($"No se encuentra el instalador de .NET Desktop Runtime: '{sourceFiles.DotNetInstallerPath}'.");

        if (string.IsNullOrWhiteSpace(sourceFiles.PcpiInstallerPath) || !File.Exists(sourceFiles.PcpiInstallerPath))
            errors.Add($"No se encuentra PCPI: '{sourceFiles.PcpiInstallerPath}'.");

        return errors.Count == 0 ? PostInstallValidationResult.Valid : new PostInstallValidationResult(false, errors);
    }
}
