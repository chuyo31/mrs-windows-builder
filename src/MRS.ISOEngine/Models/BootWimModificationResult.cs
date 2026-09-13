namespace MRS.ISOEngine.Models;

/// <summary>Resultado de aplicar LabConfig sobre boot.wim (P16): qué se aplicó realmente y si terminó consistente.</summary>
public sealed record BootWimModificationResult(
    bool Success,
    IReadOnlyList<string> AppliedLogLines,
    IReadOnlyList<string> Errors);
