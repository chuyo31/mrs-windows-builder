namespace MRS.ProfileEngine.Models;

/// <summary>
/// Perfil de eliminación cargado desde JSON (<c>profiles/*.json</c>). Es un dato
/// puro: no ejecuta nada, no conoce DISM ni el catálogo real, y no decide nada
/// por sí mismo. Un perfil solo produce una lista de <see cref="ComponentIds"/>
/// candidatos; la decisión final (protección, dependencias, plan) sigue
/// pasando siempre por <c>MRS.ComponentCatalog</c> y <c>MRS.RemovalPlanning</c>.
/// </summary>
public sealed record ProfileDefinition
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public int Version { get; init; } = 1;

    /// <summary>
    /// ComponentId candidatos (formato real del catálogo: <c>appx:</c>, <c>package:</c>,
    /// <c>feature:</c>, <c>capability:</c>, <c>driver:</c>). Nunca se inventan aquí: solo
    /// contiene lo que ya exista en el catálogo real de la imagen analizada.
    /// </summary>
    public IReadOnlyList<string> ComponentIds { get; init; } = Array.Empty<string>();

    /// <summary>Metadatos opcionales (por ejemplo <c>"kind": "custom"</c>).</summary>
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }

    /// <summary>
    /// Configuración de seguridad del perfil (P13): si mantiene Defender y Windows
    /// Update. Por defecto ambos protegidos (<see cref="SecurityOptions.Safe"/>) —
    /// un JSON de perfil sin <c>securityOptions</c> usa este mismo valor seguro.
    /// Para MÍNIMO/LIGERO/RECOMENDADO, usar siempre <see cref="EffectiveSecurityOptions"/>
    /// en vez de esta propiedad directamente: esos tres perfiles no pueden dejar de
    /// proteger Defender/Windows Update pase lo que pase en el JSON.
    /// </summary>
    public SecurityOptions SecurityOptions { get; init; } = SecurityOptions.Safe;

    /// <summary>
    /// Un perfil "Personalizado" no impone una lista fija de componentes: representa
    /// la selección manual que el usuario ya ha hecho a mano en la pantalla de
    /// COMPONENTES. Se detecta por convención (id "custom" o metadata "kind":"custom")
    /// en vez de crear un tipo de C# aparte solo para este caso especial.
    /// </summary>
    public bool IsCustom =>
        string.Equals(Id, "custom", StringComparison.OrdinalIgnoreCase) ||
        (Metadata is not null
            && Metadata.TryGetValue("kind", out var kind)
            && string.Equals(kind, "custom", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// MÍNIMO, LIGERO y RECOMENDADO (P13): Defender y Windows Update deben permanecer
    /// siempre protegidos en estos tres perfiles, sin ninguna forma de excepción
    /// accidental (ni siquiera si el JSON del perfil declarase lo contrario). Se
    /// identifican por Id, no por metadata: es una política fija del producto, no
    /// una opción configurable perfil a perfil.
    /// </summary>
    public bool IsSecurityLocked => SecurityLockedProfileIds.Contains(Id);

    private static readonly HashSet<string> SecurityLockedProfileIds =
        new(StringComparer.OrdinalIgnoreCase) { "minimal", "light", "recommended" };

    /// <summary>
    /// La configuración de seguridad que realmente debe aplicarse. Para los perfiles
    /// bloqueados (<see cref="IsSecurityLocked"/>) siempre es <see cref="SecurityOptions.Safe"/>,
    /// pase lo que pase en <see cref="SecurityOptions"/>; para el resto (Limpio,
    /// Personalizado) es la configuración del perfil, modificable desde la UI.
    /// </summary>
    public SecurityOptions EffectiveSecurityOptions => IsSecurityLocked ? SecurityOptions.Safe : SecurityOptions;
}
