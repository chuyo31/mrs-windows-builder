using MRS.RemovalEngine.Models;

namespace MRS.RemovalEngine.Tests;

/// <summary>
/// <see cref="IProgress{T}"/> de prueba que registra cada reporte de forma
/// síncrona e inmediata (a diferencia de <see cref="Progress{T}"/>, que
/// reenvía a través de un <c>SynchronizationContext</c> y podría no haber
/// entregado el último valor todavía cuando el test hace sus asserts).
/// Demuestra además que la telemetría no necesita WPF para poder probarse.
/// </summary>
internal sealed class RecordingProgress : IProgress<ProgressInfo>
{
    public List<ProgressInfo> Reports { get; } = new();

    public void Report(ProgressInfo value) => Reports.Add(value);
}
