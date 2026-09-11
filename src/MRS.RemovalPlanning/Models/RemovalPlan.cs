using MRS.ComponentCatalog.Models;

namespace MRS.RemovalPlanning.Models;

/// <summary>
/// Plan de eliminación producido por <see cref="RemovalPlanBuilder"/>. Es
/// puramente descriptivo: nada en este modelo ejecuta DISM ni modifica el WIM.
/// Lo consumirá el futuro <c>RemovalEngine</c>.
/// </summary>
public sealed record RemovalPlan
{
    /// <summary>Identifica la imagen/edición para la que se construyó el plan.</summary>
    public string ImageId { get; init; } = string.Empty;

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Un elemento por cada componente solicitado por el usuario.</summary>
    public IReadOnlyList<RemovalPlanItem> Components { get; init; } = Array.Empty<RemovalPlanItem>();

    /// <summary>Acciones concretas y descriptivas (solo de los componentes permitidos).</summary>
    public IReadOnlyList<RemovalAction> Actions { get; init; } = Array.Empty<RemovalAction>();

    public IReadOnlyList<RemovalWarning> Warnings { get; init; } = Array.Empty<RemovalWarning>();

    /// <summary>Errores de construcción del plan (p. ej. un ComponentId inexistente).</summary>
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Perfil de referencia, si el plan se originó a partir de uno. Los
    /// perfiles automáticos no se implementan todavía (fase posterior); por
    /// ahora es siempre <c>null</c> (selección manual / Custom).
    /// </summary>
    public RemovalProfile? Profile { get; init; }

    public int TotalSelected => Components.Count(c => c.Requested);
    public int TotalAllowed => Components.Count(c => c.Allowed);
    public int TotalBlocked => Components.Count(c => c.Requested && !c.Allowed);

    /// <summary>
    /// Un plan es válido si no hubo errores de construcción. Tener componentes
    /// bloqueados es normal y no invalida el plan: simplemente no generan
    /// ninguna acción.
    /// </summary>
    public bool IsValid => Errors.Count == 0;
}
