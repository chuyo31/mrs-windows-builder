using MRS.RemovalPlanning.Models;

namespace MRS.RemovalEngine.Models;

/// <summary>Resultado real de ejecutar UNA <see cref="RemovalAction"/> contra DISM.</summary>
public sealed record RemovalExecutionItem
{
    public string ComponentId { get; init; } = string.Empty;
    public RemovalActionType ActionType { get; init; } = RemovalActionType.None;
    public string Target { get; init; } = string.Empty;

    public DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset? FinishedAt { get; init; }

    public bool Success { get; init; }
    public int? ExitCode { get; init; }
    public string? Output { get; init; }
    public string? Error { get; init; }
}
