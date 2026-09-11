namespace MRS.RemovalPlanning.Models;

/// <summary>Resultado de <see cref="MRS.RemovalPlanning.RemovalPlanValidator"/>.</summary>
public sealed record PlanValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors,
    IReadOnlyList<RemovalWarning> Warnings)
{
    public static PlanValidationResult Valid { get; } =
        new(true, Array.Empty<string>(), Array.Empty<RemovalWarning>());
}
