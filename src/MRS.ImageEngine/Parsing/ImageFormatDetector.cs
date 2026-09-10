using MRS.ImageEngine.Models;

namespace MRS.ImageEngine.Parsing;

/// <summary>Deduce el formato de imagen a partir de la extensión del archivo.</summary>
public static class ImageFormatDetector
{
    public static ImageFormat FromPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return ImageFormat.Unknown;

        var extension = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
        return extension switch
        {
            "wim" => ImageFormat.Wim,
            "esd" => ImageFormat.Esd,
            _ => ImageFormat.Unknown
        };
    }
}
