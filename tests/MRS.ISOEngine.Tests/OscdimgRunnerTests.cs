using MRS.ISOEngine.Exceptions;
using MRS.ISOEngine.Oscdimg;
using MRS.ISOEngine.Tests.Fakes;
using Xunit;

namespace MRS.ISOEngine.Tests;

/// <summary>
/// P19, "OSCDIMG": <c>oscdimg.exe</c> es una herramienta externa (Windows ADK),
/// no incluida en Windows. <see cref="OscdimgRunner"/> debe decir la verdad
/// sobre su disponibilidad en vez de fallar de forma confusa al primer intento
/// real (mismo principio que la comprobación de privilegios elevados de P17).
/// </summary>
public sealed class OscdimgRunnerTests
{
    [Fact]
    public void IsAvailable_is_false_for_an_explicit_path_that_does_not_exist()
    {
        var runner = new OscdimgRunner(new FakeProcessRunner(), oscdimgPath: @"C:\no-existe\oscdimg.exe");

        Assert.False(runner.IsAvailable());
    }

    [Fact]
    public void IsAvailable_is_true_when_an_explicit_existing_path_is_given()
    {
        var tempExe = Path.Combine(Path.GetTempPath(), "mrs-oscdimg-tests-" + Guid.NewGuid().ToString("N") + ".exe");
        File.WriteAllText(tempExe, "fake oscdimg");
        try
        {
            var runner = new OscdimgRunner(new FakeProcessRunner(), oscdimgPath: tempExe);

            Assert.True(runner.IsAvailable());
        }
        finally
        {
            File.Delete(tempExe);
        }
    }

    [Fact]
    public async Task BuildIsoAsync_throws_a_controlled_exception_when_unavailable()
    {
        var runner = new OscdimgRunner(new FakeProcessRunner(), oscdimgPath: @"C:\no-existe\oscdimg.exe");

        await Assert.ThrowsAsync<IsoEngineException>(() => runner.BuildIsoAsync(@"C:\workspace", @"C:\out.iso"));
    }

    [Fact]
    public async Task BuildIsoAsync_uses_the_dual_boot_bootdata_command_documented_by_Microsoft()
    {
        var tempExe = Path.Combine(Path.GetTempPath(), "mrs-oscdimg-tests-" + Guid.NewGuid().ToString("N") + ".exe");
        File.WriteAllText(tempExe, "fake oscdimg");
        try
        {
            var process = new FakeProcessRunner();
            var runner = new OscdimgRunner(process, oscdimgPath: tempExe);

            await runner.BuildIsoAsync(@"C:\workspace", @"C:\out\final.iso");

            var (fileName, arguments, _) = process.Calls.Single();
            Assert.Equal(tempExe, fileName);
            Assert.Contains("-m -o -u2 -udfver102", arguments);
            Assert.Contains("etfsboot.com", arguments);
            Assert.Contains("efisys.bin", arguments);
            Assert.Contains(@"""C:\workspace""", arguments);
            Assert.Contains(@"""C:\out\final.iso""", arguments);
        }
        finally
        {
            File.Delete(tempExe);
        }
    }
}
