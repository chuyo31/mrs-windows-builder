namespace MRS.ISOEngine.Models;

/// <summary>
/// Una modificación concreta que debería aplicarse al workspace de generación
/// para reflejar una opción de <c>InstallationOptions</c> habilitada (P15). Es
/// puramente descriptiva: <see cref="InstallationConfigurationPlanner"/> la
/// produce, pero ninguna clase de esta fase la ejecuta todavía sobre un
/// <c>boot.wim</c> real (ver prompts/15-resultado.md, "qué queda pendiente").
/// </summary>
public sealed record InstallationConfigurationAction
{
    public InstallationActionCategory Category { get; init; }

    /// <summary>Texto corto, en el mismo estilo que los ejemplos del prompt (p. ej. "Local account enabled").</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Mecanismo real por el que se aplicaría (registro offline, autounattend.xml, script de WinPE...).</summary>
    public string Mechanism { get; init; } = string.Empty;

    /// <summary>Archivo/hive/artefacto concreto que se modificaría dentro del workspace.</summary>
    public string TargetArtifact { get; init; } = string.Empty;

    /// <summary>
    /// Estado real de implementación (P16): la mayoría son <see cref="InstallationActionStatus.Implemented"/>;
    /// "Storage bypass" es <see cref="InstallationActionStatus.NotImplemented"/> y "Offline OOBE" es
    /// <see cref="InstallationActionStatus.PendingReliableMechanism"/> hasta confirmarse en la build objetivo.
    /// </summary>
    public InstallationActionStatus Status { get; init; } = InstallationActionStatus.Implemented;

    public string Tag => Category switch
    {
        InstallationActionCategory.Install => "[INSTALL]",
        InstallationActionCategory.Compat => "[COMPAT]",
        _ => "[?]",
    };

    /// <summary>Línea de log exactamente en el formato del prompt: <c>"[INSTALL] Local account enabled"</c>.</summary>
    public string ToLogLine() => $"{Tag} {Description}";
}
