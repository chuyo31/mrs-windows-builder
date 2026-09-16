using MRS.DismEngine.Processes;
using MRS.ISOEngine.Oscdimg;

namespace MRS.ISOEngine.Tests.Fakes;

internal sealed class FakeOscdimgRunner : IOscdimgRunner
{
    public bool Available { get; set; } = true;
    public int ExitCode { get; set; }
    public int CallCount { get; private set; }
    public (string WorkspaceRoot, string OutputIsoPath)? LastCall { get; private set; }

    public bool IsAvailable() => Available;

    public Task<ProcessRunResult> BuildIsoAsync(string workspaceRoot, string outputIsoPath, CancellationToken cancellationToken = default)
    {
        CallCount++;
        LastCall = (workspaceRoot, outputIsoPath);
        return Task.FromResult(new ProcessRunResult(
            "oscdimg.exe", string.Empty, ExitCode, string.Empty, string.Empty, TimeSpan.FromMilliseconds(1), false));
    }
}
