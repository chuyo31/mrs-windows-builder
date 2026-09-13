namespace MRS.InstallationOptions.Models;

/// <summary>
/// Configuración del comportamiento del instalador/OOBE de la ISO final (P15):
/// cuenta local, OOBE sin conexión, y los bypasses de compatibilidad de
/// hardware (TPM, Secure Boot, CPU, RAM, almacenamiento).
///
/// Es una capacidad propia de la ISO, deliberadamente independiente de
/// <c>MRS.ComponentCatalog.Models.SecurityOptions</c> (Defender/Windows
/// Update, P13) y de <c>MRS.RemovalPlanning</c>/<c>ProfileDefinition</c>: no
/// decide qué componentes se eliminan, ni afecta a la protección de ningún
/// componente. Solo describe cómo debe comportarse el instalador al arrancar
/// la ISO, mucho antes de que exista ningún catálogo o plan de eliminación.
///
/// Todos los campos son booleanos independientes con valores seguros por
/// defecto para este proyecto (todos habilitados): desactivar uno nunca
/// activa ni desactiva ningún otro, y cualquier combinación de los 7 es
/// representable y coherente (no existe un estado "parcial" o inválido).
/// </summary>
public sealed record InstallationOptions
{
    /// <summary>Permite completar OOBE creando una cuenta local (sin exigir una cuenta Microsoft).</summary>
    public bool AllowLocalAccount { get; init; } = true;

    /// <summary>Permite continuar OOBE sin conexión a Internet.</summary>
    public bool AllowOfflineOobe { get; init; } = true;

    /// <summary>Omite el requisito de TPM 2.0 del instalador.</summary>
    public bool BypassTpm { get; init; } = true;

    /// <summary>Omite el requisito de Secure Boot del instalador.</summary>
    public bool BypassSecureBoot { get; init; } = true;

    /// <summary>Omite el requisito de CPU compatible del instalador.</summary>
    public bool BypassCpu { get; init; } = true;

    /// <summary>
    /// Omite la comprobación de RAM mínima del instalador. Esto NO garantiza que
    /// Windows 11 funcione con fluidez con poca RAM (p. ej. 2 GB): solo evita que
    /// el instalador bloquee la instalación por esa comprobación concreta. Ver
    /// prompts/15-resultado.md para la distinción completa.
    /// </summary>
    public bool BypassRam { get; init; } = true;

    /// <summary>Omite el requisito de almacenamiento mínimo del instalador.</summary>
    public bool BypassStorage { get; init; } = true;

    /// <summary>Valores seguros por defecto del proyecto: todos los bypasses habilitados.</summary>
    public static InstallationOptions Default { get; } = new();
}
