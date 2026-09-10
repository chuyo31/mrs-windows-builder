namespace MRS.DismEngine.Logging;

/// <summary>
/// Una línea de log ya formada. <see cref="ToString"/> produce el formato
/// <c>[NIVEL] mensaje</c> que espera la interfaz.
/// </summary>
public sealed record LogEntry(DateTimeOffset Timestamp, LogLevel Level, string Message)
{
    public string Tag => $"[{Level.ToString().ToUpperInvariant()}]";

    public override string ToString() => $"{Tag} {Message}";
}
