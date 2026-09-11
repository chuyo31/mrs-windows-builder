using MRS.ComponentCatalog.Models;
using MRS.RemovalPlanning.Models;

namespace MRS.RemovalEngine.Tests;

/// <summary>Construye <see cref="RemovalPlan"/>/<see cref="RemovalPlanItem"/> de prueba sin pasar por <c>RemovalPlanBuilder</c>.</summary>
internal static class PlanFactory
{
    public static RemovalPlanItem Item(
        string componentId, string displayName, ComponentSourceType sourceType, RemovalActionType action,
        bool allowed = true, ComponentProtection protection = ComponentProtection.Removable) => new()
    {
        ComponentId = componentId,
        DisplayName = displayName,
        SourceType = sourceType,
        Action = allowed ? action : RemovalActionType.None,
        Allowed = allowed,
        Protection = protection,
        Requested = true,
        CurrentState = ComponentCurrentState.Installed,
    };

    public static RemovalAction Action(RemovalPlanItem item, string? target = null) => new()
    {
        ActionType = item.Action,
        Target = target ?? ExtractTarget(item.ComponentId),
        ComponentId = item.ComponentId,
    };

    public static RemovalPlan Plan(params RemovalPlanItem[] items) => new()
    {
        Components = items,
        Actions = items.Where(i => i.Allowed && i.Action != RemovalActionType.None).Select(i => Action(i)).ToList(),
        Errors = Array.Empty<string>(),
    };

    public static RemovalPlan InvalidPlan() => new() { Errors = new[] { "forzado para tests" } };

    private static string ExtractTarget(string componentId)
    {
        var idx = componentId.IndexOf(':');
        return idx >= 0 ? componentId[(idx + 1)..] : componentId;
    }
}
