using MRS.ComponentCatalog.Models;

namespace MRS.ComponentCatalog.Rules;

/// <summary>
/// Regla de identificación: si el texto de un componente contiene
/// <see cref="Pattern"/> (sin distinguir mayúsculas/minúsculas), se le asigna
/// <see cref="Category"/> y las <see cref="Tags"/> indicadas. No decide
/// protección: eso es responsabilidad de <see cref="ProtectionRule"/>.
/// </summary>
public sealed record ClassificationRule
{
    public string Id { get; init; } = string.Empty;
    public string Pattern { get; init; } = string.Empty;
    public ComponentCategory Category { get; init; } = ComponentCategory.Unknown;
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
    public bool Enabled { get; init; } = true;
}
