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

    /// <summary>
    /// Identificador del workspace (el GUID de <see cref="WorkspacePath"/>), para
    /// logging estructurado. Toda esta operación debe usar un único
    /// WorkspaceId: si <see cref="MountPath"/> o <see cref="WorkingWimPath"/>
    /// pertenecieran a un workspace distinto, sería exactamente el bug de P09
    /// (mezclar el MountDir de un workspace con el WIM de otro).
    /// </summary>
    public string WorkspaceId => Path.GetFileName(Path.TrimEndingDirectorySeparator(WorkspacePath));

    /// <summary>
    /// Comprueba que <see cref="MountPath"/> y <see cref="WorkingWimPath"/>
    /// cuelgan realmente de <see cref="WorkspacePath"/>. Si alguno perteneciera a
    /// otro workspace, esta operación estaría mezclando dos contextos distintos.
    /// </summary>
    public bool HasConsistentWorkspace()
    {
        try
        {
            var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(WorkspacePath)) + Path.DirectorySeparatorChar;
            return IsUnderRoot(MountPath, root) && IsUnderRoot(WorkingWimPath, root);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsUnderRoot(string path, string root)
        => Path.GetFullPath(path).StartsWith(root, StringComparison.OrdinalIgnoreCase);
}
