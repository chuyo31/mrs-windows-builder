namespace MRS.ComponentCatalog.Models;

/// <summary>
/// Componente clasificado del catálogo. Se construye a partir de un elemento
/// real del <see cref="MRS.ImageEngine.Models.ImageInventory"/> (paquete, AppX,
/// feature, capability o driver); no existe independientemente de un inventario.
/// Es un dato puro, sin ninguna dependencia de UI ni de DISM.
/// </summary>
public sealed record ComponentDefinition
{
    /// <summary>Identificador estable dentro del catálogo (origen + nombre técnico).</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Nombre técnico (Package Identity / PackageName / Feature Name / Capability Identity).</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Nombre legible para el usuario.</summary>
    public string DisplayName { get; init; } = string.Empty;

    public string? Description { get; init; }

    public ComponentCategory Category { get; init; } = ComponentCategory.Unknown;

    public ComponentSourceType SourceType { get; init; } = ComponentSourceType.Unknown;

    public ComponentRisk Risk { get; init; } = ComponentRisk.Unknown;

    /// <summary>Mecanismo de eliminación descriptivo; no se ejecuta en esta fase.</summary>
    public RemovalMode RemovalMode { get; init; } = RemovalMode.None;

    public ComponentProtection Protection { get; init; } = ComponentProtection.Unknown;

    /// <summary>Explica por qué <see cref="Protection"/> es <c>Protected</c> (o similar).</summary>
    public string? ProtectionReason { get; init; }

    /// <summary>Presente en el inventario analizado.</summary>
    public bool Detected { get; init; } = true;

    /// <summary>Estado DISM: instalado (Installed / Enabled, según el origen).</summary>
    public bool Installed { get; init; }

    /// <summary>Estado DISM: reemplazado por una versión más reciente.</summary>
    public bool Superseded { get; init; }

    /// <summary>Dependencias salientes de este componente (Source = este Id).</summary>
    public IReadOnlyList<ComponentDependency> Dependencies { get; init; } = Array.Empty<ComponentDependency>();

    /// <summary>Ids de componentes en conflicto conocido (estructura preparada, sin resolver todavía).</summary>
    public IReadOnlyList<string> Conflicts { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Perfiles en los que se sugeriría eliminar este componente. Preparado para
    /// la fase de perfiles (Normal/Light/Medium/Ultra/Custom); vacío por ahora.
    /// </summary>
    public IReadOnlyList<RemovalProfile> SuggestedProfiles { get; init; } = Array.Empty<RemovalProfile>();
}
