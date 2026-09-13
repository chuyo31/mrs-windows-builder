using System.Text.RegularExpressions;
using MRS.PostInstall.Models;

namespace MRS.PostInstall.Configuration;

/// <summary>
/// Valida la coherencia de una <see cref="PostInstallConfiguration"/> por sí
/// misma (P18, sección 13/18): no comprueba archivos en disco (eso es
/// <c>PostInstallPackageValidator</c>, que combina esto con
/// <see cref="PostInstallSourceFiles"/>).
/// </summary>
public static class PostInstallConfigurationValidator
{
    private static readonly Regex VersionPattern = new(@"^\d+\.\d+\.\d+$", RegexOptions.Compiled);

    public static PostInstallValidationResult Validate(PostInstallConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config);

        // Deshabilitado: no hay nada más que comprobar, no se va a generar ningún paquete.
        if (!config.Enabled)
            return PostInstallValidationResult.Valid;

        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(config.DotNetRuntimeVersion) || !VersionPattern.IsMatch(config.DotNetRuntimeVersion))
            errors.Add($"Versión de runtime inválida: '{config.DotNetRuntimeVersion}' (se esperaba algo como '8.0.26').");

        if (!string.Equals(config.Architecture, "x64", StringComparison.OrdinalIgnoreCase))
            errors.Add($"Arquitectura no soportada: '{config.Architecture}' (solo x64 en esta fase).");

        if (string.IsNullOrWhiteSpace(config.DotNetInstallerFileName) || LooksLikeAPath(config.DotNetInstallerFileName))
            errors.Add("El nombre del instalador de .NET debe ser un nombre de archivo simple, no una ruta.");

        if (string.IsNullOrWhiteSpace(config.PcpiFileName) || LooksLikeAPath(config.PcpiFileName))
            errors.Add("El nombre de PCPI debe ser un nombre de archivo simple, no una ruta.");

        if (!string.IsNullOrWhiteSpace(config.DotNetInstallerFileName) && !string.IsNullOrWhiteSpace(config.PcpiFileName)
            && string.Equals(config.DotNetInstallerFileName, config.PcpiFileName, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("El nombre de archivo de .NET y el de PCPI no pueden coincidir (se sobrescribirían entre sí).");
        }

        return errors.Count == 0 ? PostInstallValidationResult.Valid : new PostInstallValidationResult(false, errors);
    }

    private static bool LooksLikeAPath(string fileName)
        => Path.IsPathRooted(fileName) || fileName.Contains('\\') || fileName.Contains('/');
}
