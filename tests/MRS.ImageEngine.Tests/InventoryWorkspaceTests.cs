using MRS.ImageEngine.Inventory;
using MRS.ImageEngine.Models;
using Xunit;

namespace MRS.ImageEngine.Tests;

public sealed class InventoryWorkspaceTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "mrs-workspace-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void CreateNew_creates_all_subdirectories_under_a_unique_guid_folder()
    {
        var workspace = InventoryWorkspace.CreateNew(_root);

        Assert.StartsWith(_root, workspace.RootPath);
        Assert.NotEqual(_root, workspace.RootPath); // subcarpeta con GUID
        Assert.True(Directory.Exists(workspace.SourcePath));
        Assert.True(Directory.Exists(workspace.MountPath));
        Assert.True(Directory.Exists(workspace.LogsPath));
        Assert.True(Directory.Exists(workspace.OutputPath));
    }

    [Fact]
    public void CreateNew_produces_a_different_workspace_each_time()
    {
        var a = InventoryWorkspace.CreateNew(_root);
        var b = InventoryWorkspace.CreateNew(_root);

        Assert.NotEqual(a.RootPath, b.RootPath);
    }

    [Fact]
    public void Delete_removes_the_workspace_tree()
    {
        var workspace = InventoryWorkspace.CreateNew(_root);
        File.WriteAllText(Path.Combine(workspace.LogsPath, "dism.log"), "x");

        workspace.Delete();

        Assert.False(Directory.Exists(workspace.RootPath));
    }

    [Fact]
    public void ImageInventory_counts_reflect_the_collections()
    {
        var inventory = new ImageInventory
        {
            Packages = new[] { new ImagePackage { PackageIdentity = "p1" }, new ImagePackage { PackageIdentity = "p2" } },
            Features = new[] { new ImageFeature { Name = "f1" } },
            Capabilities = Array.Empty<ImageCapability>(),
            ProvisionedApps = new[] { new ProvisionedApp { DisplayName = "a1" } },
            Drivers = new[] { new ImageDriver { PublishedName = "oem0.inf" }, new ImageDriver { PublishedName = "oem1.inf" } },
        };

        Assert.Equal(2, inventory.PackageCount);
        Assert.Equal(1, inventory.FeatureCount);
        Assert.Equal(0, inventory.CapabilityCount);
        Assert.Equal(1, inventory.ProvisionedAppCount);
        Assert.Equal(2, inventory.DriverCount);
        Assert.Empty(ImageInventory.Empty.Packages);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // best-effort
        }
    }
}
