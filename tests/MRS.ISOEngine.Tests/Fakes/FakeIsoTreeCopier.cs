using MRS.ISOEngine.TreeCopy;

namespace MRS.ISOEngine.Tests.Fakes;

internal sealed class FakeIsoTreeCopier : IIsoTreeCopier
{
    public int CallCount { get; private set; }
    public List<(string SourceIsoPath, string DestinationRoot)> Calls { get; } = new();

    /// <summary>Si se define, se ejecuta en vez del comportamiento por defecto (permite simular la copia real de un árbol de prueba).</summary>
    public Action<string, string>? OnCopy { get; set; }

    public Task CopyAsync(string sourceIsoPath, string destinationRoot, CancellationToken cancellationToken = default)
    {
        CallCount++;
        Calls.Add((sourceIsoPath, destinationRoot));
        OnCopy?.Invoke(sourceIsoPath, destinationRoot);
        return Task.CompletedTask;
    }
}
