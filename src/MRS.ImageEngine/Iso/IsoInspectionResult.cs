using MRS.ImageEngine.Models;

namespace MRS.ImageEngine.Iso;

/// <summary>
/// Resultado de comprobar qué imagen de instalación contiene una ISO, sin
/// analizarla todavía.
/// </summary>
public sealed record IsoInspectionResult(
    string IsoPath,
    bool ImageFound,
    string? ImagePath,
    ImageFormat Format)
{
    public static IsoInspectionResult NotFound(string isoPath)
        => new(isoPath, false, null, ImageFormat.Unknown);

    public static IsoInspectionResult Found(string isoPath, string imagePath, ImageFormat format)
        => new(isoPath, true, imagePath, format);
}
