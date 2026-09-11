namespace MRS.RemovalEngine;

/// <summary>Error irrecuperable durante la preparación o ejecución del RemovalEngine. Conserva el ExitCode de DISM cuando existe.</summary>
public sealed class RemovalEngineException : Exception
{
    public int ExitCode { get; }

    public RemovalEngineException(int exitCode, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        ExitCode = exitCode;
    }
}
