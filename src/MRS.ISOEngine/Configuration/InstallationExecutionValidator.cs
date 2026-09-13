using MRS.ISOEngine.Models;
// Ver InstallationConfigurationPlanner.cs: mismo caso de colisión namespace/tipo.
using InstallationOptionsModel = MRS.InstallationOptions.Models.InstallationOptions;

namespace MRS.ISOEngine.Configuration;

/// <summary>
/// Guarda previa a ejecutar <c>InstallationImageService</c> (P16, sección 9): si
/// <see cref="InstallationOptionsModel.BypassStorage"/> está activado, la
/// ejecución debe abortar en vez de generar algo aparentemente completo pero
/// engañoso (el bypass de almacenamiento no tiene un mecanismo implementado —
/// ver <see cref="InstallationActionStatus.NotImplemented"/> en el planificador).
/// </summary>
public static class InstallationExecutionValidator
{
    public static WorkspaceValidationResult Validate(InstallationOptionsModel options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var plannedActions = InstallationConfigurationPlanner.Plan(options);
        var notImplemented = plannedActions.Where(a => a.Status == InstallationActionStatus.NotImplemented).ToList();

        if (notImplemented.Count == 0)
            return WorkspaceValidationResult.Valid;

        var errors = notImplemented
            .Select(a => a.Description switch
            {
                "Storage bypass enabled" => "El bypass de almacenamiento no está implementado/validado para esta build.",
                _ => $"'{a.Description}' no está implementado/validado para esta build.",
            })
            .ToList();

        return new WorkspaceValidationResult(false, errors);
    }
}
