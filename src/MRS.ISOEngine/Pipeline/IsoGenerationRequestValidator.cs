using MRS.ISOEngine.Configuration;
using MRS.ISOEngine.Models;
using MRS.PostInstall.Packaging;

namespace MRS.ISOEngine.Pipeline;

/// <summary>
/// Validación previa del pipeline completo (P19, "VALIDACIÓN" inicial):
/// combina las validaciones que ya existían por separado (P15/P16 para
/// InstallationOptions, P18 para PostInstall) con las comprobaciones propias
/// de esta fase (ISO origen, ruta de salida, índice). Si algo falla, el
/// pipeline no debe llegar a tocar ningún archivo.
/// </summary>
public static class IsoGenerationRequestValidator
{
    public static WorkspaceValidationResult Validate(IsoGenerationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.SourceIsoPath) || !File.Exists(request.SourceIsoPath))
            errors.Add($"No se encuentra la ISO de origen: '{request.SourceIsoPath}'.");

        if (request.EditionIndex <= 0)
            errors.Add($"Índice de edición inválido: {request.EditionIndex}.");

        if (string.IsNullOrWhiteSpace(request.OutputIsoPath))
            errors.Add("No se especificó una ruta de salida para la ISO final.");
        else if (!string.IsNullOrWhiteSpace(request.SourceIsoPath)
                 && string.Equals(Path.GetFullPath(request.OutputIsoPath), Path.GetFullPath(request.SourceIsoPath), StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("La ISO de salida no puede ser la misma ruta que la ISO original.");
        }

        // Reutiliza la validación ya existente de P15/P16 (bypass de
        // almacenamiento sin mecanismo implementado): no se duplica la lógica.
        var installationOptionsResult = InstallationExecutionValidator.Validate(request.InstallationOptions);
        errors.AddRange(installationOptionsResult.Errors);

        // Reutiliza la validación de P18: coherencia + existencia real de .NET/PCPI.
        var postInstallResult = PostInstallPackageValidator.Validate(request.PostInstallConfiguration, request.PostInstallSourceFiles);
        errors.AddRange(postInstallResult.Errors);

        return errors.Count == 0 ? WorkspaceValidationResult.Valid : new WorkspaceValidationResult(false, errors);
    }
}
