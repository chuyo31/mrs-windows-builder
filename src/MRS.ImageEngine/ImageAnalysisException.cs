namespace MRS.ImageEngine;

/// <summary>
/// Error de análisis de una imagen. Conserva el código de salida de DISM
/// (o -1 para fallos que no provienen de un código concreto) para poder
/// registrarlo sin ocultarlo al usuario avanzado.
/// </summary>
public sealed class ImageAnalysisException : Exception
{
    public int ExitCode { get; }

    public ImageAnalysisException(int exitCode, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        ExitCode = exitCode;
    }
}
