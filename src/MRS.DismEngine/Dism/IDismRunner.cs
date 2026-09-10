using MRS.DismEngine.Processes;

namespace MRS.DismEngine.Dism;

/// <summary>
/// Invoca DISM.exe de forma controlada para operaciones de solo lectura sobre
/// una imagen WIM/ESD. Devuelve siempre el <see cref="ProcessRunResult"/> crudo
/// para que la capa superior decida en función del código de salida.
/// </summary>
public interface IDismRunner
{
    /// <summary>Equivalente a <c>DISM /Get-WimInfo /WimFile:&lt;ruta&gt;</c>.</summary>
    Task<ProcessRunResult> GetWimInfoAsync(string imagePath, CancellationToken cancellationToken = default);

    /// <summary>Equivalente a <c>DISM /Get-WimInfo /WimFile:&lt;ruta&gt; /Index:N</c>.</summary>
    Task<ProcessRunResult> GetWimInfoAsync(string imagePath, int index, CancellationToken cancellationToken = default);
}
