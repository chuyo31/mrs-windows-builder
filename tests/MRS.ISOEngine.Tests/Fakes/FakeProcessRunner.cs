using MRS.DismEngine.Processes;

namespace MRS.ISOEngine.Tests.Fakes;

/// <summary><see cref="IProcessRunner"/> controlado: registra los comandos exactos y devuelve un ExitCode configurable, sin ejecutar ningún proceso real.</summary>
internal sealed class FakeProcessRunner : IProcessRunner
{
    public int ExitCode { get; set; }
    public List<(string FileName, string Arguments)> Calls { get; } = new();

    public Task<ProcessRunResult> RunAsync(string fileName, string arguments, CancellationToken cancellationToken = default, TimeSpan? timeout = null)
    {
        Calls.Add((fileName, arguments));
        return Task.FromResult(new ProcessRunResult(fileName, arguments, ExitCode, string.Empty, string.Empty, TimeSpan.FromMilliseconds(1), false));
    }
}
