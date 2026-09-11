using MRS.RemovalEngine.Tests.Fakes;
using Xunit;

namespace MRS.RemovalEngine.Tests;

public sealed class WorkingImageFactoryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "mrs-workingimage-tests", Guid.NewGuid().ToString("N"));
    private readonly string _sourceWim;

    public WorkingImageFactoryTests()
    {
        Directory.CreateDirectory(_root);
        _sourceWim = Path.Combine(_root, "install.wim");
        File.WriteAllText(_sourceWim, "fake wim contents");
    }

    [Fact]
    public async Task CreateAsync_throws_when_source_does_not_exist()
    {
        var factory = new WorkingImageFactory(new FakeDismRunner());

        await Assert.ThrowsAsync<FileNotFoundException>(
            () => factory.CreateAsync(Path.Combine(_root, "missing.wim"), 2, _root));
    }

    [Fact]
    public async Task CreateAsync_creates_workspace_with_subdirectories()
    {
        var factory = new WorkingImageFactory(new FakeDismRunner());

        var image = await factory.CreateAsync(_sourceWim, 2, _root);

        Assert.True(Directory.Exists(Path.Combine(image.WorkspacePath, "source")));
        Assert.True(Directory.Exists(Path.Combine(image.WorkspacePath, "mount")));
        Assert.True(Directory.Exists(Path.Combine(image.WorkspacePath, "logs")));
        Assert.True(Directory.Exists(Path.Combine(image.WorkspacePath, "output")));
    }

    [Fact]
    public async Task CreateAsync_exports_the_selected_index()
    {
        var dism = new FakeDismRunner();
        var factory = new WorkingImageFactory(dism);

        await factory.CreateAsync(_sourceWim, 2, _root);

        Assert.Contains("export:2", dism.Calls);
    }

    [Fact]
    public async Task CreateAsync_sets_working_index_to_one_after_export()
    {
        var factory = new WorkingImageFactory(new FakeDismRunner());

        var image = await factory.CreateAsync(_sourceWim, 2, _root);

        Assert.Equal(1, image.Index);
        Assert.Equal(_sourceWim, image.SourcePath);
        Assert.NotEqual(_sourceWim, image.WorkingWimPath);
    }

    [Fact]
    public async Task CreateAsync_throws_with_exit_code_when_export_fails()
    {
        var dism = new FakeDismRunner { ExportExitCode = 87 };
        var factory = new WorkingImageFactory(dism);

        var ex = await Assert.ThrowsAsync<RemovalEngineException>(() => factory.CreateAsync(_sourceWim, 2, _root));

        Assert.Equal(87, ex.ExitCode);
    }

    [Fact]
    public async Task Original_source_file_is_never_touched()
    {
        var before = File.ReadAllText(_sourceWim);
        var factory = new WorkingImageFactory(new FakeDismRunner());

        await factory.CreateAsync(_sourceWim, 2, _root);

        Assert.Equal(before, File.ReadAllText(_sourceWim));
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
        catch { /* best-effort */ }
    }
}
