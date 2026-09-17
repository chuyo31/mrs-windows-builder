using MRS.ImageEngine.Inventory;

namespace MRS.ImageEngine.Tests.Fakes;

/// <summary>
/// <see cref="IProgress{T}"/> de prueba que registra cada reporte de forma
/// síncrona e inmediata (a diferencia de <see cref="Progress{T}"/>, que
/// reenvía a través de un <c>SynchronizationContext</c> y podría no haber
/// entregado el último valor todavía cuando el test hace sus asserts). Mismo
/// patrón que <c>MRS.RemovalEngine.Tests.RecordingProgress</c> (P10).
/// </summary>
internal sealed class RecordingProgress : IProgress<InventoryProgressInfo>
{
    public List<InventoryProgressInfo> Reports { get; } = new();

    public void Report(InventoryProgressInfo value) => Reports.Add(value);
}
