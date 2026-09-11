namespace MRS.RemovalEngine.Models;

/// <summary>
/// Compara el inventario ANTES/DESPUÉS de ejecutar un <see cref="RemovalExecutionResult"/>
/// para confirmar (o no) que lo que se dijo eliminado realmente lo está.
/// </summary>
public sealed record RemovalVerificationResult
{
    public IReadOnlyList<string> Removed { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> StillPresent { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> UnexpectedChanges { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Failed { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}
