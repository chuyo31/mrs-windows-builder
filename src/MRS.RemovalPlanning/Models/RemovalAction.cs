using MRS.ComponentCatalog.Models;

namespace MRS.RemovalPlanning.Models;

/// <summary>
/// Qué HARÍAMOS con un componente concreto. Puramente descriptiva: el
/// <see cref="RemovalPlanBuilder"/> la construye pero nunca la ejecuta; eso
/// sería responsabilidad del futuro <c>RemovalEngine</c>.
/// </summary>
public sealed record RemovalAction
{
    public RemovalActionType ActionType { get; init; } = RemovalActionType.None;

    /// <summary>Identificador técnico sobre el que actuaría DISM (Package Identity, PackageName, etc.).</summary>
    public string Target { get; init; } = string.Empty;

    public string ComponentId { get; init; } = string.Empty;

    public IReadOnlyDictionary<string, string> Parameters { get; init; } =
        new Dictionary<string, string>();

    public EstimatedSize EstimatedSize { get; init; } = EstimatedSize.Unknown;

    public ComponentRisk Risk { get; init; } = ComponentRisk.Unknown;
}
