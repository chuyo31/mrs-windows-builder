using MRS.RemovalEngine.Models;
using MRS.RemovalPlanning.Models;

namespace MRS.RemovalEngine;

/// <summary>
///   RemovalPlan (MRS.RemovalPlanning)  ->  IRemovalEngine  ->  DismRunner  ->  DISM.exe
///
/// VALIDAR -&gt; MONTAR (ReadWrite) -&gt; EJECUTAR -&gt; VALIDAR -&gt; COMMIT/DISCARD.
/// No clasifica componentes, no decide qué proteger y no construye el
/// RemovalPlan: eso ya está hecho antes de llegar aquí.
/// </summary>
public interface IRemovalEngine
{
    /// <summary>
    /// <paramref name="progress"/> es puramente informativo (etapa/porcentaje/
    /// mensaje/nivel); es opcional y nunca condiciona el resultado de la
    /// operación. No acopla esta capa a ninguna UI concreta.
    /// </summary>
    Task<RemovalExecutionResult> ExecuteAsync(
        WorkingImage image, RemovalPlan plan, CancellationToken cancellationToken = default,
        IProgress<ProgressInfo>? progress = null);
}
