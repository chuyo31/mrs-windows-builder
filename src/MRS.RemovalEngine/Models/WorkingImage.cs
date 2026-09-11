using MRS.ImageEngine.Models;

namespace MRS.RemovalEngine.Models;

/// <summary>
/// Copia de trabajo independiente de la imagen original. Todas las
/// modificaciones de la fase 7 ocurren aquí; <see cref="SourcePath"/> nunca se
/// abre en modo escritura.
/// </summary>
public sealed class WorkingImage
{
    /// <summary>Ruta del WIM/ESD original (de la ISO). Nunca se modifica.</summary>
    public required string SourcePath { get; init; }

    /// <summary>Ruta del WIM de trabajo: una copia independiente creada con <c>/Export-Image</c>.</summary>
    public required string WorkingWimPath { get; init; }

    /// <summary>Índice a usar dentro de <see cref="WorkingWimPath"/> (tras exportar, normalmente 1).</summary>
    public required int Index { get; init; }

    public required string MountPath { get; init; }

    public required string WorkspacePath { get; init; }

    public ImageFormat Format { get; init; } = ImageFormat.Wim;

    public bool IsMounted { get; set; }

    public bool IsCommitted { get; set; }
}
