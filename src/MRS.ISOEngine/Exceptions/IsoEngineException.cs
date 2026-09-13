namespace MRS.ISOEngine.Exceptions;

/// <summary>
/// Error controlado de <c>MRS.ISOEngine</c> (P16): un paso de la preparación de
/// boot.wim/autounattend.xml falló de una forma esperada (DISM devolvió un
/// <c>ExitCode</c> distinto de 0, una validación previa impidió continuar, etc.).
/// Mismo espíritu que <c>RemovalEngineException</c>/<c>ImageAnalysisException</c>
/// en los otros motores: nunca se decide un fallo mirando texto en la salida.
/// </summary>
public sealed class IsoEngineException : Exception
{
    public int? ExitCode { get; }

    public IsoEngineException(string message) : base(message) { }

    public IsoEngineException(string message, int exitCode) : base(message) => ExitCode = exitCode;

    public IsoEngineException(string message, Exception innerException) : base(message, innerException) { }
}
