namespace MRS.ISOEngine.Models;

/// <summary>
/// Estado real de implementación de una <see cref="InstallationConfigurationAction"/> (P16).
/// Distingue explícitamente "implementado y aplicado" de "mecanismo pendiente de
/// confirmar" de "no implementado": el prompt exige no aparentar que algo está
/// operativo cuando no lo está.
/// </summary>
public enum InstallationActionStatus
{
    /// <summary>Se aplica realmente sobre el workspace (registro offline / autounattend.xml).</summary>
    Implemented,

    /// <summary>
    /// El mecanismo existe en el código pero no se aplica automáticamente todavía:
    /// su fiabilidad en la build objetivo no se ha podido confirmar en esta fase
    /// (P16, sección 7 — OOBE sin conexión / BypassNRO).
    /// </summary>
    PendingReliableMechanism,

    /// <summary>Sin mecanismo fiable conocido; la opción no debe aparentar estar implementada (P16, sección 9 — almacenamiento).</summary>
    NotImplemented,
}
