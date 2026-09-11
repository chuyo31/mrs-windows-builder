using MRS.DismEngine.Dism;
using MRS.DismEngine.Logging;
using MRS.ImageEngine.Inventory;
using MRS.ImageEngine.Parsing;
using MRS.RemovalEngine.Models;

namespace MRS.RemovalEngine;

/// <summary>
/// Crea el WIM de trabajo con <c>DISM /Export-Image</c>: exporta ÚNICAMENTE la
/// edición elegida a un archivo nuevo e independiente. El origen se abre solo
/// en lectura; nunca se modifica. El índice exportado pasa a ser el 1 dentro
/// del WIM de trabajo (es el único que contiene).
/// </summary>
public sealed class WorkingImageFactory : IWorkingImageFactory
{
    private const long MinimumFreeSpaceMarginBytes = 200L * 1024 * 1024; // 200 MB de margen además del tamaño de origen.

    private readonly IDismRunner _dism;
    private readonly IAppLogger _logger;

    public WorkingImageFactory(IDismRunner dismRunner, IAppLogger? logger = null)
    {
        _dism = dismRunner ?? throw new ArgumentNullException(nameof(dismRunner));
        _logger = logger ?? NullAppLogger.Instance;
    }

    public async Task<WorkingImage> CreateAsync(
        string sourceWimPath, int sourceIndex, string? workspaceRoot = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceWimPath) || !File.Exists(sourceWimPath))
            throw new FileNotFoundException("La imagen origen no existe.", sourceWimPath);

        if (sourceIndex < 1)
            throw new RemovalEngineException(-1, "Índice de edición inválido.");

        _logger.Info("Creando workspace...");
        var workspace = InventoryWorkspace.CreateNew(workspaceRoot);

        EnsureEnoughFreeSpace(sourceWimPath, workspace.RootPath);

        var destination = Path.Combine(workspace.SourcePath, "install.wim");

        _logger.Info($"Creando imagen de trabajo (copia independiente del índice {sourceIndex})...");
        var export = await _dism
            .ExportImageAsync(sourceWimPath, sourceIndex, destination, destinationName: null, cancellationToken)
            .ConfigureAwait(false);

        if (!export.Succeeded)
        {
            _logger.Error($"No se pudo crear la imagen de trabajo. ExitCode: {export.ExitCode}");
            throw new RemovalEngineException(export.ExitCode, "No se pudo crear la imagen de trabajo (Export-Image).");
        }

        _logger.Info("Imagen de trabajo creada. La imagen original permanece intacta.");

        return new WorkingImage
        {
            SourcePath = sourceWimPath,
            WorkingWimPath = destination,
            Index = 1,
            MountPath = workspace.MountPath,
            WorkspacePath = workspace.RootPath,
            Format = ImageFormatDetector.FromPath(destination),
        };
    }

    private static void EnsureEnoughFreeSpace(string sourceWimPath, string workspaceRoot)
    {
        try
        {
            var sourceSize = new FileInfo(sourceWimPath).Length;
            var root = Path.GetPathRoot(Path.GetFullPath(workspaceRoot));
            if (string.IsNullOrEmpty(root))
                return;

            var drive = new DriveInfo(root);
            if (drive.IsReady && drive.AvailableFreeSpace < sourceSize + MinimumFreeSpaceMarginBytes)
                throw new RemovalEngineException(-1, "Espacio en disco insuficiente para crear la imagen de trabajo.");
        }
        catch (RemovalEngineException)
        {
            throw;
        }
        catch
        {
            // Comprobación best-effort: si no se puede determinar, se continúa.
        }
    }
}
