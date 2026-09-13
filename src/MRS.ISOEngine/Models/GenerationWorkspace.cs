namespace MRS.ISOEngine.Models;

/// <summary>
/// Describe dónde viven los artefactos de una ISO en proceso de generación
/// (P15): una copia de trabajo de la ISO original, nunca la ISO original en
/// sí. La extracción/copiado real de la ISO a este layout (oscdimg, montaje,
/// etc.) es una fase posterior (ver prompts/15-resultado.md); este tipo solo
/// modela las rutas para poder validarlas y planificar sobre ellas.
/// </summary>
public sealed record GenerationWorkspace
{
    /// <summary>Ruta de la ISO original. Solo se lee; nunca se escribe en ella.</summary>
    public string SourceIsoPath { get; init; } = string.Empty;

    /// <summary>Directorio de trabajo (copia) donde vive el árbol de la ISO en generación.</summary>
    public string WorkspacePath { get; init; } = string.Empty;

    /// <summary>Ruta de <c>sources\boot.wim</c> (WinPE/Setup) dentro del workspace.</summary>
    public string BootWimPath { get; init; } = string.Empty;

    /// <summary>Ruta de <c>sources\install.wim</c> dentro del workspace.</summary>
    public string InstallWimPath { get; init; } = string.Empty;

    /// <summary>Índice de la edición objetivo dentro de <see cref="InstallWimPath"/>.</summary>
    public int Index { get; init; }

    /// <summary>Arquitectura esperada (p. ej. <c>"amd64"</c>).</summary>
    public string Architecture { get; init; } = "amd64";

    /// <summary>
    /// Directorio donde se monta temporalmente un WIM del workspace (P16) — nunca
    /// una unidad fija, igual que <c>InventoryWorkspace.MountPath</c>.
    /// </summary>
    public string MountPath { get; init; } = string.Empty;

    /// <summary>Identificador del workspace para logging estructurado (P09/P16), derivado de <see cref="WorkspacePath"/>.</summary>
    public string WorkspaceId => WorkspacePath.Length == 0
        ? string.Empty
        : Path.GetFileName(Path.TrimEndingDirectorySeparator(WorkspacePath));
}
