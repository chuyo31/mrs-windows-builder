namespace MRS.ImageEngine.Inventory;

/// <summary>
/// Telemetría de progreso reportada por <see cref="ImageInventoryService"/> a
/// través de <see cref="IProgress{T}"/> (P22). Representa únicamente las
/// FASES reales del inventariado (creación de workspace, montaje, cada
/// categoría DISM, etc.) — nunca el porcentaje interno de una operación DISM
/// individual, que no es fiable. Puramente informativa: no acopla
/// <c>MRS.ImageEngine</c> a WPF ni a ninguna UI concreta.
/// </summary>
public sealed record InventoryProgressInfo(
    string Stage,
    int Percent,
    string Message,
    InventoryProgressLevel Level,
    DateTimeOffset Timestamp)
{
    public static InventoryProgressInfo Create(string stage, int percent, string message, InventoryProgressLevel level = InventoryProgressLevel.Info)
        => new(stage, Math.Clamp(percent, 0, 100), message, level, DateTimeOffset.Now);
}
