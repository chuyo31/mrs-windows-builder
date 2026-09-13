using MRS.DismEngine.Processes;

namespace MRS.PostInstall.Tests.Fakes;

/// <summary><see cref="IProcessRunner"/> controlado: registra los comandos exactos y devuelve un ExitCode configurable, sin ejecutar ningún proceso real (P18, sección 19: nunca se lanzan instaladores reales).</summary>
internal sealed class FakeProcessRunner : IProcessRunner
{
    public int ExitCode { get; set; }
    public bool TimeOut { get; set; }
    public List<(string FileName, string Arguments, string? WorkingDirectory)> Calls { get; } = new();

    public Task<ProcessRunResult> RunAsync(
        string fileName, string arguments, CancellationToken cancellationToken = default,
        TimeSpan? timeout = null, string? workingDirectory = null)
    {
        Calls.Add((fileName, arguments, workingDirectory));
        return Task.FromResult(new ProcessRunResult(
            fileName, arguments, ExitCode, string.Empty, string.Empty, TimeSpan.FromMilliseconds(1), TimeOut));
    }
}
