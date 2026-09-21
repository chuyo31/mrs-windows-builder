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
            // P26: este archivo puede llegar aquí ya copiado por IsoTreeCopier, que
            // copia el árbol COMPLETO de la ISO (incluido sources\boot.wim) antes de
            // que InstallationImageService invoque este método. P25 solo preparaba
            // (quitaba ReadOnly de) la copia justo después de copiarla aquí mismo;
            // como en el flujo real casi siempre el archivo YA EXISTE por ese motivo,
            // ese camino idempotente se saltaba la preparación por completo y el
            // archivo llegaba a Mount-Wim todavía ReadOnly. Ahora se prepara en
            // AMBOS caminos -- este método nunca devuelve sin haber dejado el
            // archivo listo para montar, se haya copiado él mismo o no.
            _logger.Info("boot.wim ya existe en el workspace; no se vuelve a copiar (idempotente).");
            PrepareWorkingCopyForMount(destinationBootWimPath);
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

            PrepareWorkingCopyForMount(destinationBootWimPath);
            return true;
        }
        finally
        {
            await mount.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Deja la COPIA DE TRABAJO lista para Mount-Wim: quita el atributo ReadOnly
    /// si lo tiene (idempotente -- si ya es escribible, no hace nada ni falla).
    /// <c>File.Copy</c> conserva los atributos del archivo origen, y como
    /// <c>sources\boot.wim</c> siempre se lee de una ISO montada en SOLO LECTURA
    /// (ya sea por este mismo método o por <c>IsoTreeCopier</c>, que copia el
    /// árbol completo antes), cualquier copia nueva hereda ReadOnly -- DISM no
    /// puede montarla en escritura hasta quitárselo ("Fail to flush file
    /// buffers", HRESULT=0x80070006, "WIM open failed with access denied.").
    /// Nunca se llama sobre nada dentro de una ISO montada, solo sobre
    /// <paramref name="path"/>, que ya es una copia independiente en el workspace.
    /// </summary>
    private void PrepareWorkingCopyForMount(string path)
    {
        _logger.Info("Preparando boot.wim de trabajo...");

        var attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.ReadOnly) != 0)
        {
            _logger.Info("Eliminando atributo ReadOnly de boot.wim...");
            File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
            _logger.Info("OK boot.wim preparado para escritura.");
        }
        else
        {
            _logger.Info("OK boot.wim ya era writable.");
        }
    }
}
