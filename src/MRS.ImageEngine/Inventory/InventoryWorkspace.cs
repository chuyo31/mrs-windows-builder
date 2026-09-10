namespace MRS.ImageEngine.Inventory;

/// <summary>
/// Workspace temporal único por operación de inventario:
///
///   %LOCALAPPDATA%\MRS-Windows-Builder\workspaces\&lt;GUID&gt;\
///       source\  mount\  logs\  output\
///
/// No usa letras de unidad fijas ni rutas asumidas. Puede eliminarse por
/// completo con <see cref="Delete"/>.
/// </summary>
public sealed class InventoryWorkspace
{
    private InventoryWorkspace(string rootPath)
    {
        RootPath = rootPath;
        SourcePath = Path.Combine(rootPath, "source");
        MountPath = Path.Combine(rootPath, "mount");
        LogsPath = Path.Combine(rootPath, "logs");
        OutputPath = Path.Combine(rootPath, "output");
    }

    public string RootPath { get; }
    public string SourcePath { get; }
    public string MountPath { get; }
    public string LogsPath { get; }
    public string OutputPath { get; }

    /// <summary>Carpeta raíz que contiene todos los workspaces.</summary>
    public static string WorkspacesRoot(string? baseDirectory = null)
        => baseDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MRS-Windows-Builder", "workspaces");

    /// <summary>
    /// Crea un workspace nuevo con un GUID propio y todos sus subdirectorios.
    /// Comprueba antes que la carpeta está libre.
    /// </summary>
    public static InventoryWorkspace CreateNew(string? baseDirectory = null)
    {
        var root = Path.Combine(WorkspacesRoot(baseDirectory), Guid.NewGuid().ToString("N"));

        if (Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any())
            throw new InvalidOperationException($"El workspace '{root}' ya existe y no está vacío.");

        var workspace = new InventoryWorkspace(root);
        Directory.CreateDirectory(workspace.SourcePath);
        Directory.CreateDirectory(workspace.MountPath);
        Directory.CreateDirectory(workspace.LogsPath);
        Directory.CreateDirectory(workspace.OutputPath);
        return workspace;
    }

    public void Delete()
    {
        if (Directory.Exists(RootPath))
            Directory.Delete(RootPath, recursive: true);
    }
}
