using MRS.RemovalEngine;
using MRS.RemovalEngine.Models;

namespace MRS.ISOEngine.Tests.Fakes;

internal sealed class FakeWorkingImageFactory : IWorkingImageFactory
{
    public int CallCount { get; private set; }
    public string? LastSourceWimPath { get; private set; }
    public int LastSourceIndex { get; private set; }

    /// <summary>Ruta que se le "exportaría" a la imagen de trabajo (un archivo de marcador de posición ya existente en el test).</summary>
    public string WorkingWimPathToReturn { get; set; } = string.Empty;

    public Task<WorkingImage> CreateAsync(
        string sourceWimPath, int sourceIndex, string? workspaceRoot = null,
        CancellationToken cancellationToken = default, IProgress<ProgressInfo>? progress = null)
    {
        CallCount++;
        LastSourceWimPath = sourceWimPath;
        LastSourceIndex = sourceIndex;

        var mountPath = Path.Combine(Path.GetDirectoryName(WorkingWimPathToReturn) ?? Path.GetTempPath(), "mount");
        Directory.CreateDirectory(mountPath);

        return Task.FromResult(new WorkingImage
        {
            SourcePath = sourceWimPath,
            WorkingWimPath = WorkingWimPathToReturn,
            Index = 1,
            MountPath = mountPath,
            WorkspacePath = Path.GetDirectoryName(WorkingWimPathToReturn) ?? Path.GetTempPath(),
        });
    }
}
