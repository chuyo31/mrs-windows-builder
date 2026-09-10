namespace MRS.ImageEngine.Models;

/// <summary>
/// Información agregada de una imagen de instalación de Windows, obtenida
/// exclusivamente por lectura (DISM). No representa ninguna modificación.
/// </summary>
public sealed record ImageInfo
{
    /// <summary>Ruta de la ISO o del propio WIM/ESD analizado.</summary>
    public string SourceImagePath { get; init; } = string.Empty;

    public ImageFormat Format { get; init; } = ImageFormat.Unknown;

    /// <summary>Nombre del sistema, p. ej. "Windows 11".</summary>
    public string OperatingSystem { get; init; } = string.Empty;

    /// <summary>Versión comercial derivada de la build, p. ej. "26H2".</summary>
    public string? DisplayVersion { get; init; }

    /// <summary>Versión cruda, p. ej. "10.0.26300".</summary>
    public string? Version { get; init; }

    /// <summary>Build legible, p. ej. "26300.9278".</summary>
    public string? Build { get; init; }

    public ImageArchitecture Architecture { get; init; } = ImageArchitecture.Unknown;

    /// <summary>Idioma por defecto, p. ej. "es-ES".</summary>
    public string? Language { get; init; }

    public IReadOnlyList<ImageEdition> Editions { get; init; } = Array.Empty<ImageEdition>();
}
