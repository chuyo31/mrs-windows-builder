using MRS.ISOEngine.BootWim;
using MRS.ISOEngine.Exceptions;
using MRS.ISOEngine.Tests.Fakes;
using Xunit;

namespace MRS.ISOEngine.Tests;

/// <summary>
/// P16, sección 2/17 (seguridad): copia <c>sources\boot.wim</c> desde la ISO
/// montada en solo lectura, nunca escribe en la ruta de la ISO original, y
/// siempre libera el montaje (incluso si la copia falla).
/// </summary>
public sealed class BootWimProvisionerTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "mrs-bootwim-tests", Guid.NewGuid().ToString("N"));
    private readonly string _mountedIsoRoot;
    private readonly string _sourceBootWim;

    public BootWimProvisionerTests()
    {
        Directory.CreateDirectory(_dir);
        _mountedIsoRoot = Path.Combine(_dir, "mounted-iso");
        Directory.CreateDirectory(Path.Combine(_mountedIsoRoot, "sources"));
        _sourceBootWim = Path.Combine(_mountedIsoRoot, "sources", "boot.wim");
        File.WriteAllText(_sourceBootWim, "fake boot.wim contenido original");
    }

    [Fact]
    public async Task Copies_boot_wim_from_the_mounted_ISO_into_the_workspace()
    {
        var mounter = new FakeIsoMounter(_mountedIsoRoot);
        var provisioner = new BootWimProvisioner(mounter);
        var destination = Path.Combine(_dir, "workspace", "sources", "boot.wim");

        var copied = await provisioner.EnsureBootWimCopyAsync("fake.iso", destination);

        Assert.True(copied);
        Assert.True(File.Exists(destination));
        Assert.Equal("fake boot.wim contenido original", File.ReadAllText(destination));
    }

    [Fact]
    public async Task The_ISO_mount_is_always_released_after_a_successful_copy()
    {
        var mounter = new FakeIsoMounter(_mountedIsoRoot);
        var provisioner = new BootWimProvisioner(mounter);
        var destination = Path.Combine(_dir, "workspace2", "sources", "boot.wim");

        await provisioner.EnsureBootWimCopyAsync("fake.iso", destination);

        Assert.Equal(1, mounter.MountCount);
        Assert.Equal(1, mounter.DisposeCount);
    }

    [Fact]
    public async Task The_ISO_mount_is_released_even_if_boot_wim_is_missing_inside_it()
    {
        var emptyIsoRoot = Path.Combine(_dir, "empty-iso");
        Directory.CreateDirectory(Path.Combine(emptyIsoRoot, "sources")); // sin boot.wim
        var mounter = new FakeIsoMounter(emptyIsoRoot);
        var provisioner = new BootWimProvisioner(mounter);
        var destination = Path.Combine(_dir, "workspace3", "sources", "boot.wim");

        await Assert.ThrowsAsync<IsoEngineException>(() => provisioner.EnsureBootWimCopyAsync("fake.iso", destination));

        Assert.Equal(1, mounter.DisposeCount); // liberado incluso tras el fallo
    }

    [Fact]
    public async Task Does_not_copy_again_if_the_destination_already_exists_idempotent()
    {
        var mounter = new FakeIsoMounter(_mountedIsoRoot);
        var provisioner = new BootWimProvisioner(mounter);
        var destination = Path.Combine(_dir, "workspace4", "sources", "boot.wim");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.WriteAllText(destination, "ya existía");

        var copied = await provisioner.EnsureBootWimCopyAsync("fake.iso", destination);

        Assert.False(copied);
        Assert.Equal("ya existía", File.ReadAllText(destination)); // no se sobrescribió
        Assert.Equal(0, mounter.MountCount); // ni siquiera se montó la ISO
    }

    [Fact]
    public async Task Never_writes_inside_the_mounted_source_ISO_root()
    {
        var mounter = new FakeIsoMounter(_mountedIsoRoot);
        var provisioner = new BootWimProvisioner(mounter);
        var destination = Path.Combine(_dir, "workspace5", "sources", "boot.wim");
        var originalHash = File.ReadAllText(_sourceBootWim);

        await provisioner.EnsureBootWimCopyAsync("fake.iso", destination);

        Assert.Equal(originalHash, File.ReadAllText(_sourceBootWim)); // el "original" no cambió
        Assert.Single(Directory.GetFiles(Path.Combine(_mountedIsoRoot, "sources"))); // no se creó nada nuevo ahí
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); }
        catch { /* best-effort */ }
    }
}
