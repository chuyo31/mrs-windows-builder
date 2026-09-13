namespace MRS.DismEngine.Processes;

/// <summary>
/// Ejecutor de procesos externos. Abstraído para poder sustituirlo en tests.
/// </summary>
public interface IProcessRunner
{
    /// <summary>
    /// <paramref name="workingDirectory"/> (P18) es opcional: <c>null</c> hereda el
    /// directorio de trabajo del proceso actual, igual que antes de añadir este
    /// parámetro. Necesario para instaladores/ejecutables portables (p. ej. PCPI)
    /// que dependen de su propio directorio para localizar archivos relativos.
    /// </summary>
    Task<ProcessRunResult> RunAsync(
        string fileName,
        string arguments,
        CancellationToken cancellationToken = default,
        TimeSpan? timeout = null,
        string? workingDirectory = null);
}
