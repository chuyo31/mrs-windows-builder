using MRS.RemovalEngine.Models;

namespace MRS.RemovalEngine;

/// <summary>
/// Crea la copia de trabajo sobre la que operará el <see cref="IRemovalEngine"/>.
/// La imagen original (<paramref name="sourceWimPath"/> en <see cref="CreateAsync"/>)
/// nunca se abre en modo escritura.
/// </summary>
public interface IWorkingImageFactory
{
    Task<WorkingImage> CreateAsync(
        string sourceWimPath, int sourceIndex, string? workspaceRoot = null,
        CancellationToken cancellationToken = default);
}
