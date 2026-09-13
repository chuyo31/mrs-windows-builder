using MRS.DismEngine.Processes;

namespace MRS.ISOEngine.Registry;

/// <summary>
/// Edita el registro de una imagen WIM montada (P16, sección 4): carga un hive
/// offline (p. ej. <c>&lt;mount&gt;\Windows\System32\config\SYSTEM</c>) bajo una clave
/// temporal de <c>HKLM</c>, permite leer/escribir/eliminar valores DWORD, y lo
/// descarga. Cada método devuelve el <see cref="ProcessRunResult"/> crudo de
/// <c>reg.exe</c>: el llamador decide en función de <c>ExitCode</c>, nunca del texto
/// de la salida.
/// </summary>
public interface IOfflineRegistryEditor
{
    /// <summary><c>reg load HKLM\&lt;hiveKeyName&gt; "&lt;hiveFilePath&gt;"</c>.</summary>
    Task<ProcessRunResult> LoadHiveAsync(string hiveKeyName, string hiveFilePath, CancellationToken cancellationToken = default);

    /// <summary><c>reg add "HKLM\&lt;hiveKeyName&gt;\&lt;subKeyPath&gt;" /v &lt;valueName&gt; /t REG_DWORD /d &lt;value&gt; /f</c>. Idempotente: sobrescribe si ya existe.</summary>
    Task<ProcessRunResult> SetDwordAsync(string hiveKeyName, string subKeyPath, string valueName, int value, CancellationToken cancellationToken = default);

    /// <summary><c>reg query "HKLM\&lt;hiveKeyName&gt;\&lt;subKeyPath&gt;" /v &lt;valueName&gt;</c>. Para verificación post-modificación.</summary>
    Task<ProcessRunResult> QueryValueAsync(string hiveKeyName, string subKeyPath, string valueName, CancellationToken cancellationToken = default);

    /// <summary><c>reg delete "HKLM\&lt;hiveKeyName&gt;\&lt;subKeyPath&gt;" /v &lt;valueName&gt; /f</c>. No falla si el valor no existía.</summary>
    Task<ProcessRunResult> DeleteValueAsync(string hiveKeyName, string subKeyPath, string valueName, CancellationToken cancellationToken = default);

    /// <summary><c>reg unload HKLM\&lt;hiveKeyName&gt;</c>. Debe llamarse siempre, incluso tras un fallo previo.</summary>
    Task<ProcessRunResult> UnloadHiveAsync(string hiveKeyName, CancellationToken cancellationToken = default);
}
