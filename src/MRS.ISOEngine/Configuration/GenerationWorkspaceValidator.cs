using MRS.ISOEngine.Models;

namespace MRS.ISOEngine.Configuration;

/// <summary>
/// Fase de validación previa a generar la ISO final (P15, sección 10): comprueba
/// que el workspace tiene lo que se necesita (boot.wim, install.wim, índice,
/// arquitectura) y que nunca coincide con la ISO original. Si falta algo, el
/// llamador debe ABORTAR — nunca generar una ISO aparentemente válida pero
/// incompleta. No ejecuta DISM ni construye nada: solo comprueba rutas.
/// </summary>
public static class GenerationWorkspaceValidator
{
    private static readonly string[] SupportedArchitectures = { "amd64" };

    public static WorkspaceValidationResult Validate(GenerationWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(workspace.SourceIsoPath) || !File.Exists(workspace.SourceIsoPath))
            errors.Add($"No se encuentra la ISO de origen: '{workspace.SourceIsoPath}'.");

        if (string.IsNullOrWhiteSpace(workspace.WorkspacePath) || !Directory.Exists(workspace.WorkspacePath))
            errors.Add($"No existe el workspace de generación: '{workspace.WorkspacePath}'.");

        if (string.IsNullOrWhiteSpace(workspace.BootWimPath) || !File.Exists(workspace.BootWimPath))
            errors.Add("No se encuentra boot.wim en el workspace.");

        if (string.IsNullOrWhiteSpace(workspace.InstallWimPath) || !File.Exists(workspace.InstallWimPath))
            errors.Add("No se encuentra install.wim en el workspace.");

        if (workspace.Index <= 0)
            errors.Add($"Índice de edición inválido: {workspace.Index}.");

        if (!SupportedArchitectures.Contains(workspace.Architecture, StringComparer.OrdinalIgnoreCase))
            errors.Add($"Arquitectura no soportada: '{workspace.Architecture}' (se esperaba amd64).");

        // La ISO original nunca se modifica: el workspace tiene que ser una copia,
        // nunca la misma ruta que la fuente.
        if (!string.IsNullOrWhiteSpace(workspace.SourceIsoPath)
            && !string.IsNullOrWhiteSpace(workspace.WorkspacePath)
            && PathsPointToSameLocation(workspace.SourceIsoPath, workspace.WorkspacePath))
        {
            errors.Add("El workspace de generación no puede ser la misma ruta que la ISO original.");
        }

        return errors.Count == 0 ? WorkspaceValidationResult.Valid : new WorkspaceValidationResult(false, errors);
    }

    private static bool PathsPointToSameLocation(string a, string b)
        => string.Equals(
            Path.GetFullPath(a).TrimEnd('\\', '/'),
            Path.GetFullPath(b).TrimEnd('\\', '/'),
            StringComparison.OrdinalIgnoreCase);
}
