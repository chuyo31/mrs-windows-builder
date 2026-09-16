using MRS.ISOEngine.TreeCopy;
using MRS.ISOEngine.Tests.Fakes;
using Xunit;

namespace MRS.ISOEngine.Tests;

/// <summary>
/// P19, "PREPARACIÓN": copia el árbol completo de la ISO (bootmgr, boot\, efi\,
/// sources\, setup.exe...) al workspace — el paso que faltaba en P15/P16/P18
/// para que oscdimg pueda producir una ISO arrancable.
/// </summary>
public sealed class IsoTreeCopierTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "mrs-isotreecopier-tests", Guid.NewGuid().ToString("N"));
    private readonly string _mountedIsoRoot;

    public IsoTreeCopierTests()
    {
        Directory.CreateDirectory(_dir);
        _mountedIsoRoot = Path.Combine(_dir, "mounted-iso");
        Directory.CreateDirectory(_mountedIsoRoot);

        // Árbol de ISO representativo: bootmgr en la raíz, boot\ y efi\ (cargadores
        // de arranque), sources\boot.wim/install.wim.
        File.WriteAllText(Path.Combine(_mountedIsoRoot, "bootmgr"), "x");
        Directory.CreateDirectory(Path.Combine(_mountedIsoRoot, "boot"));
        File.WriteAllText(Path.Combine(_mountedIsoRoot, "boot", "etfsboot.com"), "x");
        Directory.CreateDirectory(Path.Combine(_mountedIsoRoot, "efi", "microsoft", "boot"));
        File.WriteAllText(Path.Combine(_mountedIsoRoot, "efi", "microsoft", "boot", "efisys.bin"), "x");
        Directory.CreateDirectory(Path.Combine(_mountedIsoRoot, "sources"));
        File.WriteAllText(Path.Combine(_mountedIsoRoot, "sources", "boot.wim"), "fake boot.wim");
        File.WriteAllText(Path.Combine(_mountedIsoRoot, "sources", "install.wim"), "fake install.wim");
    }

    [Fact]
    public async Task Copies_every_file_and_subdirectory_preserving_the_tree()
    {
        var mounter = new FakeIsoMounter(_mountedIsoRoot);
        var copier = new IsoTreeCopier(mounter);
        var destination = Path.Combine(_dir, "workspace");

        await copier.CopyAsync("fake.iso", destination);

        Assert.True(File.Exists(Path.Combine(destination, "bootmgr")));
        Assert.True(File.Exists(Path.Combine(destination, "boot", "etfsboot.com")));
        Assert.True(File.Exists(Path.Combine(destination, "efi", "microsoft", "boot", "efisys.bin")));
        Assert.True(File.Exists(Path.Combine(destination, "sources", "boot.wim")));
        Assert.True(File.Exists(Path.Combine(destination, "sources", "install.wim")));
    }

    [Fact]
    public async Task The_ISO_mount_is_always_released_after_copying()
    {
        var mounter = new FakeIsoMounter(_mountedIsoRoot);
        var copier = new IsoTreeCopier(mounter);

        await copier.CopyAsync("fake.iso", Path.Combine(_dir, "workspace2"));

        Assert.Equal(1, mounter.MountCount);
        Assert.Equal(1, mounter.DisposeCount);
    }

    [Fact]
    public async Task Never_modifies_the_mounted_source_tree()
    {
        var mounter = new FakeIsoMounter(_mountedIsoRoot);
        var copier = new IsoTreeCopier(mounter);
        var originalBootMgr = File.ReadAllText(Path.Combine(_mountedIsoRoot, "bootmgr"));

        await copier.CopyAsync("fake.iso", Path.Combine(_dir, "workspace3"));

        Assert.Equal(originalBootMgr, File.ReadAllText(Path.Combine(_mountedIsoRoot, "bootmgr")));
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); }
        catch { /* best-effort */ }
    }
}
