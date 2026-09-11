namespace MRS.RemovalEngine.Models;

/// <summary>
/// Resultado de <see cref="MRS.RemovalEngine.IRemovalEngine.ExecuteAsync"/>.
/// Comportamiento transaccional: <see cref="Committed"/> solo es <c>true</c> si
/// TODAS las acciones tuvieron éxito; cualquier fallo implica
/// <see cref="Discarded"/> = <c>true</c> y <see cref="Committed"/> = <c>false</c>.
/// </summary>
public sealed record RemovalExecutionResult
{
    public bool Success { get; init; }
    public RemovalExecutionPhase Phase { get; init; } = RemovalExecutionPhase.NotStarted;

    public IReadOnlyList<RemovalExecutionItem> ActionsExecuted { get; init; } = Array.Empty<RemovalExecutionItem>();
    public IReadOnlyList<RemovalExecutionItem> ActionsFailed { get; init; } = Array.Empty<RemovalExecutionItem>();

    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

    public int? ExitCode { get; init; }

    public string Workspace { get; init; } = string.Empty;
    public WorkingImage? WorkingImage { get; init; }

    public bool Committed { get; init; }
    public bool Discarded { get; init; }
}
