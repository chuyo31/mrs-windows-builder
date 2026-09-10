namespace MRS.DismEngine.Logging;

/// <summary>
/// Sistema de logging sencillo y reutilizable. Cada mensaje emitido se publica
/// mediante <see cref="Entry"/> para que la capa de interfaz lo muestre.
/// </summary>
public interface IAppLogger
{
    event EventHandler<LogEntry>? Entry;

    void Info(string message);
    void Warn(string message);
    void Error(string message);
    void Dism(string message);
}
