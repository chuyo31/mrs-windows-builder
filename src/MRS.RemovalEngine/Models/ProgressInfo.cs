namespace MRS.RemovalEngine.Models;

/// <summary>
/// Telemetría de progreso reportada por <see cref="MRS.RemovalEngine.IWorkingImageFactory"/>
/// y <see cref="MRS.RemovalEngine.IRemovalEngine"/> a través de <see cref="IProgress{T}"/>.
/// Puramente informativa: no acopla la capa de negocio a WPF ni a ninguna UI.
/// </summary>
public sealed record ProgressInfo(
    string Stage,
    int Percent,
    string Message,
    ProgressLevel Level,
    DateTimeOffset Timestamp)
{
    public static ProgressInfo Create(string stage, int percent, string message, ProgressLevel level = ProgressLevel.Info)
        => new(stage, Math.Clamp(percent, 0, 100), message, level, DateTimeOffset.Now);
}
