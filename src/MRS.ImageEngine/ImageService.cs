using MRS.DismEngine.Dism;
using MRS.DismEngine.Logging;
using MRS.DismEngine.Processes;
using MRS.ImageEngine.Iso;
using MRS.ImageEngine.Models;
using MRS.ImageEngine.Parsing;

namespace MRS.ImageEngine;

/// <summary>
/// Orquestador de la fase de análisis (100% lectura).
///
///   MainWindow  ->  ImageService  ->  DismRunner  ->  DISM.exe
///
/// No monta el WIM, no modifica la ISO y no toca ningún archivo de la imagen.
/// </summary>
public sealed class ImageService
{
    private static readonly string[] InstallImageNames = { "install.wim", "install.esd" };

    private readonly IDismRunner _dism;
    private readonly IIsoMounter? _isoMounter;
    private readonly DismWimInfoParser _parser;
    private readonly IAppLogger _logger;

    public ImageService(
        IDismRunner dismRunner,
        IIsoMounter? isoMounter = null,
        DismWimInfoParser? parser = null,
        IAppLogger? logger = null)
    {
        _dism = dismRunner ?? throw new ArgumentNullException(nameof(dismRunner));
        _isoMounter = isoMounter;
        _parser = parser ?? new DismWimInfoParser();
        _logger = logger ?? NullAppLogger.Instance;
    }

    /// <summary>
    /// Monta la ISO, comprueba que existe y localiza <c>sources\install.wim</c> o
    /// <c>sources\install.esd</c>. Desmonta siempre antes de devolver.
    /// </summary>
    public async Task<IsoInspectionResult> InspectIsoAsync(string isoPath, CancellationToken cancellationToken = default)
    {
        EnsureIsoExists(isoPath);
        var mounter = RequireMounter();

        await using var mount = await mounter.MountAsync(isoPath, cancellationToken).ConfigureAwait(false);

        var imagePath = LocateInstallImage(mount.RootPath);
        if (imagePath is null)
        {
            _logger.Warn(@"La ISO no contiene sources\install.wim ni sources\install.esd.");
            return IsoInspectionResult.NotFound(isoPath);
        }

        var format = ImageFormatDetector.FromPath(imagePath);
        _logger.Info($"Imagen encontrada en la ISO: sources\\{Path.GetFileName(imagePath)} ({format}).");
        return IsoInspectionResult.Found(isoPath, imagePath, format);
    }

    /// <summary>
    /// Monta la ISO, analiza la imagen de instalación que contiene y desmonta.
    /// </summary>
    public async Task<ImageInfo> AnalyzeIsoAsync(string isoPath, CancellationToken cancellationToken = default)
    {
        EnsureIsoExists(isoPath);
        var mounter = RequireMounter();

        await using var mount = await mounter.MountAsync(isoPath, cancellationToken).ConfigureAwait(false);

        var imagePath = LocateInstallImage(mount.RootPath)
            ?? throw new ImageAnalysisException(-1,
                @"La ISO no contiene sources\install.wim ni sources\install.esd.");

        var info = await AnalyzeImageFileAsync(imagePath, cancellationToken).ConfigureAwait(false);
        return info with { SourceImagePath = isoPath };
    }

    /// <summary>
    /// Analiza directamente un WIM/ESD accesible en disco. No requiere montaje de
    /// ISO (usado también por los tests).
    /// </summary>
    public async Task<ImageInfo> AnalyzeImageFileAsync(string imageFilePath, CancellationToken cancellationToken = default)
    {
        var format = ImageFormatDetector.FromPath(imageFilePath);

        var listResult = await _dism.GetWimInfoAsync(imageFilePath, cancellationToken).ConfigureAwait(false);
        EnsureDismSucceeded(listResult);

        var listEntries = _parser.ParseEditionList(listResult.StandardOutput);
        if (listEntries.Count == 0)
            throw new ImageAnalysisException(listResult.ExitCode, "DISM no devolvió ningún índice de imagen.");

        var editions = new List<ImageEdition>(listEntries.Count);
        foreach (var entry in listEntries)
        {
            var detailResult = await _dism.GetWimInfoAsync(imageFilePath, entry.Index, cancellationToken).ConfigureAwait(false);
            EnsureDismSucceeded(detailResult);

            editions.Add(_parser.ParseEditionDetail(
                detailResult.StandardOutput, entry.Index, entry.Name, entry.Description));
        }

        var reference = editions[0];
        var buildNumber = WindowsVersionResolver.ExtractBuildNumber(reference.Version, reference.ServicePackBuild);

        return new ImageInfo
        {
            SourceImagePath = imageFilePath,
            Format = format,
            OperatingSystem = WindowsVersionResolver.ResolveProductName(buildNumber, reference.Name),
            DisplayVersion = WindowsVersionResolver.ResolveDisplayVersion(buildNumber),
            Version = reference.Version,
            Build = reference.Build,
            Architecture = reference.Architecture,
            Language = reference.Language,
            Editions = editions,
        };
    }

    private IIsoMounter RequireMounter()
        => _isoMounter ?? throw new InvalidOperationException(
            "ImageService se creó sin IIsoMounter; solo AnalyzeImageFileAsync está disponible.");

    private static void EnsureIsoExists(string isoPath)
    {
        if (string.IsNullOrWhiteSpace(isoPath) || !File.Exists(isoPath))
            throw new FileNotFoundException("La ISO indicada no existe.", isoPath);

        if (!isoPath.EndsWith(".iso", StringComparison.OrdinalIgnoreCase))
            throw new ImageAnalysisException(-1, "El archivo seleccionado no es una ISO.");
    }

    private void EnsureDismSucceeded(ProcessRunResult result)
    {
        if (result.TimedOut)
            throw new ImageAnalysisException(-1, "DISM no respondió dentro del tiempo máximo.");

        if (result.ExitCode != 0)
            throw new ImageAnalysisException(result.ExitCode, "DISM no pudo analizar la imagen.");
    }

    private static string? LocateInstallImage(string mountRoot)
    {
        foreach (var name in InstallImageNames)
        {
            var candidate = Path.Combine(mountRoot, "sources", name);
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }
}
