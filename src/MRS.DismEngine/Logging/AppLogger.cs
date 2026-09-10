namespace MRS.DismEngine.Logging;

/// <summary>
/// Implementación por defecto de <see cref="IAppLogger"/>. No hace ningún tipo
/// de E/S: simplemente reemite cada mensaje como evento.
/// </summary>
public sealed class AppLogger : IAppLogger
{
    public event EventHandler<LogEntry>? Entry;

    public void Info(string message) => Emit(LogLevel.Info, message);
    public void Warn(string message) => Emit(LogLevel.Warn, message);
    public void Error(string message) => Emit(LogLevel.Error, message);
    public void Dism(string message) => Emit(LogLevel.Dism, message);

    private void Emit(LogLevel level, string message)
        => Entry?.Invoke(this, new LogEntry(DateTimeOffset.Now, level, message));
}

/// <summary>Logger que descarta todo. Útil como valor por defecto y en tests.</summary>
public sealed class NullAppLogger : IAppLogger
{
    public static readonly NullAppLogger Instance = new();

    public event EventHandler<LogEntry>? Entry { add { } remove { } }

    public void Info(string message) { }
    public void Warn(string message) { }
    public void Error(string message) { }
    public void Dism(string message) { }
}
