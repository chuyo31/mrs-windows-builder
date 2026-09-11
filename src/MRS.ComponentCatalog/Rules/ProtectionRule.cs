using MRS.ComponentCatalog.Models;

namespace MRS.ComponentCatalog.Rules;

/// <summary>
/// Regla de protección específica y revisable (nunca algo tan general como
/// "todo Microsoft-Windows = protegido"). Si <see cref="Pattern"/> coincide con
/// el texto de un componente, se marca como <c>Protected</c> con
/// <see cref="Reason"/> explicando el motivo, y su riesgo sube al menos a
/// <see cref="Risk"/>.
/// </summary>
public sealed record ProtectionRule
{
    public string Id { get; init; } = string.Empty;
    public string Pattern { get; init; } = string.Empty;
    public ComponentCategory? Category { get; init; }
    public ComponentRisk Risk { get; init; } = ComponentRisk.High;
    public string Reason { get; init; } = string.Empty;
    public bool Enabled { get; init; } = true;
}
