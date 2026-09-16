namespace MRS.ISOEngine.Models;

/// <summary>Resultado de <see cref="Pipeline.IIsoGenerationPipeline.GenerateAsync"/> (P19).</summary>
public sealed record IsoGenerationResult(
    bool Success,
    string? OutputIsoPath,
    GenerationWorkspace? Workspace,
    IReadOnlyList<string> AppliedLogLines,
    IReadOnlyList<string> Errors);
