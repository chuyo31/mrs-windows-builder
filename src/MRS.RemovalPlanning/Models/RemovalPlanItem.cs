using MRS.ComponentCatalog.Models;

namespace MRS.RemovalPlanning.Models;

/// <summary>
/// Resultado de evaluar UN componente seleccionado. <see cref="Allowed"/> +
/// <see cref="Action"/> son el veredicto final tras comprobar protección,
/// estado y dependencias; <see cref="BlockReason"/> explica por qué no si
/// <see cref="Allowed"/> es <c>false</c>.
/// </summary>
public sealed record RemovalPlanItem
{
    public string ComponentId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public ComponentCategory Category { get; init; } = ComponentCategory.Unknown;
    public ComponentSourceType SourceType { get; init; } = ComponentSourceType.Unknown;

    public ComponentCurrentState CurrentState { get; init; } = ComponentCurrentState.Unknown;

    /// <summary>El usuario lo marcó para eliminar.</summary>
    public bool Requested { get; init; }

    /// <summary>Veredicto final: solo <c>true</c> si superó protección, estado y dependencias.</summary>
    public bool Allowed { get; init; }

    /// <summary>Qué haríamos si el plan se ejecutara. <c>None</c> cuando <see cref="Allowed"/> es <c>false</c>.</summary>
    public RemovalActionType Action { get; init; } = RemovalActionType.None;

    public ComponentRisk Risk { get; init; } = ComponentRisk.Unknown;
    public ComponentProtection Protection { get; init; } = ComponentProtection.Unknown;

    /// <summary>Motivo del bloqueo; <c>null</c> si <see cref="Allowed"/> es <c>true</c>.</summary>
    public string? BlockReason { get; init; }

    public IReadOnlyList<string> Dependencies { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Dependents { get; init; } = Array.Empty<string>();

    public EstimatedSize EstimatedSize { get; init; } = EstimatedSize.Unknown;

    public IReadOnlyList<RemovalWarning> Warnings { get; init; } = Array.Empty<RemovalWarning>();
}
