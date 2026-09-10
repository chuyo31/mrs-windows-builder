namespace MRS.ImageEngine.Models;

/// <summary>
/// Una edición (índice) dentro de un WIM/ESD. Diseñada para soportar varias
/// ediciones por imagen, distintas arquitecturas y distintas versiones.
/// </summary>
public sealed record ImageEdition
{
    /// <summary>Índice DISM (1..N).</summary>
    public int Index { get; init; }

    /// <summary>Nombre, p. ej. "Windows 11 Pro".</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Descripción declarada en la imagen.</summary>
    public string Description { get; init; } = string.Empty;

    public ImageArchitecture Architecture { get; init; } = ImageArchitecture.Unknown;

    /// <summary>Versión cruda de DISM, p. ej. "10.0.26300".</summary>
    public string? Version { get; init; }

    /// <summary>"ServicePack Build" de DISM, p. ej. "9278".</summary>
    public string? ServicePackBuild { get; init; }

    /// <summary>Build compuesta legible, p. ej. "26300.9278".</summary>
    public string? Build { get; init; }

    /// <summary>Idioma por defecto, p. ej. "es-ES".</summary>
    public string? Language { get; init; }

    /// <summary>Campo "Edition" de DISM, p. ej. "Professional".</summary>
    public string? Edition { get; init; }

    public override string ToString() => Name;
}
