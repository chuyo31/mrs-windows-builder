using MRS.ISOEngine.Models;

namespace MRS.ISOEngine;

/// <summary>
/// Crea un <see cref="GenerationWorkspace"/> real en disco (P16), con el mismo
/// patrón que <c>InventoryWorkspace</c> (P04/P09) y <c>WorkingImageFactory</c> (P07):
/// un GUID propio, nunca una letra de unidad fija, bajo
/// <c>%LOCALAPPDATA%\MRS-Windows-Builder\iso-workspaces\&lt;GUID&gt;\</c>.
/// </summary>
public static class GenerationWorkspaceFactory
{
    public static string WorkspacesRoot(string? baseDirectory = null)
        => baseDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MRS-Windows-Builder", "iso-workspaces");

    /// <summary>
    /// Crea un workspace nuevo con sus subdirectorios (<c>sources/</c>, <c>mount/</c>).
    /// No copia todavía ningún archivo — eso lo hace <c>IBootWimProvisioner</c>.
    /// </summary>
    public static GenerationWorkspace CreateNew(
        string sourceIsoPath, int index, string architecture = "amd64", string? baseDirectory = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceIsoPath);

        var root = Path.Combine(WorkspacesRoot(baseDirectory), Guid.NewGuid().ToString("N"));

        if (Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any())
            throw new InvalidOperationException($"El workspace '{root}' ya existe y no está vacío.");

        var sourcesDir = Path.Combine(root, "sources");
        var mountDir = Path.Combine(root, "mount");
        Directory.CreateDirectory(sourcesDir);
        Directory.CreateDirectory(mountDir);

        return new GenerationWorkspace
        {
            SourceIsoPath = sourceIsoPath,
            WorkspacePath = root,
            BootWimPath = Path.Combine(sourcesDir, "boot.wim"),
            InstallWimPath = Path.Combine(sourcesDir, "install.wim"),
            MountPath = mountDir,
            Index = index,
            Architecture = architecture,
        };
    }
}
