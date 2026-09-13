namespace MRS.ISOEngine.Models;

/// <summary>Resultado de validar un <see cref="GenerationWorkspace"/> antes de generar la ISO final (P15).</summary>
public sealed record WorkspaceValidationResult(bool IsValid, IReadOnlyList<string> Errors)
{
    public static WorkspaceValidationResult Valid { get; } = new(true, Array.Empty<string>());
}
