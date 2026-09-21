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

            _logger.Info("Preparando boot.wim de trabajo...");

            // Copia simple: boot.wim no necesita reducirse a una sola edición (a
            // diferencia de install.wim con Export-Image) — sus índices son WinPE/
            // Setup y Recuperación, no ediciones de Windows.
            File.Copy(sourceBootWim, destinationBootWimPath, overwrite: false);

            // P25: File.Copy conserva los atributos del archivo origen. Como
            // sources\boot.wim se lee desde una ISO montada en SOLO LECTURA, la
            // copia hereda el atributo ReadOnly, y DISM no puede montarla en
            // escritura hasta quitárselo ("Fail to flush file buffers",
            // HRESULT=0x80070006, "WIM open failed with access denied."). Solo se
            // toca la COPIA de trabajo: el archivo original (dentro de la ISO
            // montada) nunca se modifica.
            EnsureWritable(destinationBootWimPath);

            _logger.Info($"boot.wim copiado al workspace: '{destinationBootWimPath}'. La ISO original no se ha modificado.");
            _logger.Info("boot.wim preparado para montaje.");
            return true;
        }
        finally
        {
            await mount.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Quita el atributo ReadOnly de la copia de trabajo si lo tiene. Idempotente:
    /// si ya es escribible, no hace nada (ni falla). Nunca se llama sobre nada
    /// dentro de una ISO montada -- solo sobre <paramref name="path"/>, que ya es
    /// una copia independiente en el workspace.
    /// </summary>
    private void EnsureWritable(string path)
    {
        var attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.ReadOnly) == 0)
            return;

        _logger.Info("Eliminando atributo ReadOnly de boot.wim...");
        File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
    }
}
