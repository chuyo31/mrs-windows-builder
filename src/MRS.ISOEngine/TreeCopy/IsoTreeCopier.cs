using MRS.DismEngine.Logging;
using MRS.ImageEngine.Iso;

namespace MRS.ISOEngine.TreeCopy;

/// <summary>Implementación de <see cref="IIsoTreeCopier"/>. Reutiliza <see cref="IIsoMounter"/> (igual que <c>BootWimProvisioner</c>, P16): monta de solo lectura, copia, libera el montaje siempre.</summary>
public sealed class IsoTreeCopier : IIsoTreeCopier
{
    private readonly IIsoMounter _isoMounter;
    private readonly IAppLogger _logger;

    public IsoTreeCopier(IIsoMounter isoMounter, IAppLogger? logger = null)
    {
        _isoMounter = isoMounter ?? throw new ArgumentNullException(nameof(isoMounter));
        _logger = logger ?? NullAppLogger.Instance;
    }

    public async Task CopyAsync(string sourceIsoPath, string destinationRoot, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceIsoPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationRoot);

        Directory.CreateDirectory(destinationRoot);

        _logger.Info("Montando la ISO de origen (solo lectura) para copiar su árbol completo...");
        var mount = await _isoMounter.MountAsync(sourceIsoPath, cancellationToken).ConfigureAwait(false);
        try
        {
            var fileCount = DirectoryCopyHelper.CopyAll(mount.RootPath, destinationRoot, cancellationToken);
            _logger.Info($"Árbol de la ISO copiado al workspace ({fileCount} archivos). La ISO original no se ha modificado.");
        }
        finally
        {
            await mount.DisposeAsync().ConfigureAwait(false);
        }
    }
}
