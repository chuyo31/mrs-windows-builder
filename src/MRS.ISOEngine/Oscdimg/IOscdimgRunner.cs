using MRS.DismEngine.Processes;

namespace MRS.ISOEngine.Oscdimg;

/// <summary>
/// Genera la ISO final a partir de un workspace ya preparado, vía
/// <c>oscdimg.exe</c> (Windows ADK — Deployment Tools). Es una herramienta
/// externa, no incluida en Windows: si no está instalada,
/// <see cref="IsAvailable"/> debe decir la verdad en vez de que el primer
/// intento de ejecución falle de forma confusa.
/// </summary>
public interface IOscdimgRunner
{
    /// <summary>Comprueba si <c>oscdimg.exe</c> está disponible en este equipo, sin ejecutar nada.</summary>
    bool IsAvailable();

    /// <summary>
    /// <c>oscdimg -m -o -u2 -udfver102 -bootdata:2#p0,e,b&lt;etfsboot.com&gt;#pEF,e,b&lt;efisys.bin&gt; &lt;workspaceRoot&gt; &lt;outputIsoPath&gt;</c>
    /// (arranque dual BIOS/UEFI, el mismo comando documentado por Microsoft para
    /// generar medios de instalación de Windows). Los dos archivos de arranque
    /// deben existir ya dentro de <paramref name="workspaceRoot"/> (los copia
    /// <see cref="MRS.ISOEngine.TreeCopy.IIsoTreeCopier"/> junto con el resto del
    /// árbol de la ISO original).
    /// </summary>
    Task<ProcessRunResult> BuildIsoAsync(string workspaceRoot, string outputIsoPath, CancellationToken cancellationToken = default);
}
