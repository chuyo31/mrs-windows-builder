using MRS.DismEngine.Logging;
using MRS.ImageEngine.Iso;
using MRS.ISOEngine.Exceptions;

namespace MRS.ISOEngine.BootWim;

/// <summary>
/// Implementación de <see cref="IBootWimProvisioner"/>. Reutiliza <see cref="IIsoMounter"/>
/// (el mismo mecanismo que ya usan <c>ImageService</c>/<c>ImageInventoryService</c>
/// para leer <c>sources\install.wim</c>): la ISO se monta de solo lectura, y el
/// montaje se libera siempre — incluso si la copia falla — porque
/// <see cref="IIsoMount"/> es <see cref="IAsyncDisposable"/>.
/// </summary>
public sealed class BootWimProvisioner : IBootWimProvisioner
{
    private readonly IIsoMounter _isoMounter;
    private readonly IAppLogger _logger;

    public BootWimProvisioner(IIsoMounter isoMounter, IAppLogger? logger = null)
    {
        _isoMounter = isoMounter ?? throw new ArgumentNullException(nameof(isoMounter));
        _logger = logger ?? NullAppLogger.Instance;
    }

    public async Task<bool> EnsureBootWimCopyAsync(
        string sourceIsoPath, string destinationBootWimPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceIsoPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationBootWimPath);

        if (File.Exists(destinationBootWimPath))
        {
            _logger.Info("boot.wim ya existe en el workspace; no se vuelve a copiar (idempotente).");
            return false;
        }

        var destinationDir = Path.GetDirectoryName(destinationBootWimPath);
        if (!string.IsNullOrEmpty(destinationDir))
            Directory.CreateDirectory(destinationDir);

        _logger.Info("Montando la ISO de origen (solo lectura) para leer boot.wim...");
        var mount = await _isoMounter.MountAsync(sourceIsoPath, cancellationToken).ConfigureAwait(false);
        try
        {
            var sourceBootWim = Path.Combine(mount.RootPath, "sources", "boot.wim");
            if (!File.Exists(sourceBootWim))
                throw new IsoEngineException(@"La ISO no contiene sources\boot.wim.");

            // Copia simple: boot.wim no necesita reducirse a una sola edición (a
            // diferencia de install.wim con Export-Image) — sus índices son WinPE/
            // Setup y Recuperación, no ediciones de Windows.
            File.Copy(sourceBootWim, destinationBootWimPath, overwrite: false);
            _logger.Info($"boot.wim copiado al workspace: '{destinationBootWimPath}'. La ISO original no se ha modificado.");
            return true;
        }
        finally
        {
            await mount.DisposeAsync().ConfigureAwait(false);
        }
    }
}
