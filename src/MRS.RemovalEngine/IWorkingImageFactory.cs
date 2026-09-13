using MRS.RemovalEngine.Models;

namespace MRS.RemovalEngine;

/// <summary>
/// Crea la copia de trabajo sobre la que operará el <see cref="IRemovalEngine"/>.
/// La imagen original (<paramref name="sourceWimPath"/> en <see cref="CreateAsync"/>)
/// nunca se abre en modo escritura.
/// </summary>
public interface IWorkingImageFactory
{
    /// <summary>
    /// <paramref name="progress"/> es puramente informativo (etapa/porcentaje/
    /// mensaje/nivel); es opcional y nunca condiciona el resultado de la
    /// operación. No acopla esta capa a ninguna UI concreta.
    /// </summary>
    Task<WorkingImage> CreateAsync(
        string sourceWimPath, int sourceIndex, string? workspaceRoot = null,
        CancellationToken cancellationToken = default, IProgress<ProgressInfo>? progress = null);
}
