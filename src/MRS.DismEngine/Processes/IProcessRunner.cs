namespace MRS.DismEngine.Processes;

/// <summary>
/// Ejecutor de procesos externos. Abstraído para poder sustituirlo en tests.
/// </summary>
public interface IProcessRunner
{
    Task<ProcessRunResult> RunAsync(
        string fileName,
        string arguments,
        CancellationToken cancellationToken = default,
        TimeSpan? timeout = null);
}
