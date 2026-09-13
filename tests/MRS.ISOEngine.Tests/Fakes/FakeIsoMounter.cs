using MRS.ImageEngine.Iso;

namespace MRS.ISOEngine.Tests.Fakes;

/// <summary>
/// <see cref="IIsoMounter"/> controlado: "monta" apuntando a un directorio local ya
/// preparado por el test (con su propio <c>sources\boot.wim</c>), en vez de montar
/// una ISO real. Cuenta los montajes/desmontajes para comprobar que siempre se
/// liberan.
/// </summary>
internal sealed class FakeIsoMounter : IIsoMounter
{
    private readonly string _rootPath;

    public int MountCount { get; private set; }
    public int DisposeCount { get; private set; }

    public FakeIsoMounter(string rootPath) => _rootPath = rootPath;

    public Task<IIsoMount> MountAsync(string isoPath, CancellationToken cancellationToken = default)
    {
        MountCount++;
        return Task.FromResult<IIsoMount>(new FakeIsoMount(_rootPath, () => DisposeCount++));
    }

    private sealed class FakeIsoMount : IIsoMount
    {
        private readonly Action _onDispose;

        public FakeIsoMount(string rootPath, Action onDispose)
        {
            RootPath = rootPath;
            _onDispose = onDispose;
        }

        public string RootPath { get; }

        public ValueTask DisposeAsync()
        {
            _onDispose();
            return ValueTask.CompletedTask;
        }
    }
}
