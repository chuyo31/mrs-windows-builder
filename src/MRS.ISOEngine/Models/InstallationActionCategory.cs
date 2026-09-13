namespace MRS.ISOEngine.Models;

/// <summary>Grupo al que pertenece una <see cref="InstallationConfigurationAction"/>, usado como prefijo de log (P15).</summary>
public enum InstallationActionCategory
{
    /// <summary>Comportamiento de OOBE/cuenta (cuenta local, OOBE sin conexión). Prefijo de log: <c>[INSTALL]</c>.</summary>
    Install,

    /// <summary>Bypass de un requisito de compatibilidad de hardware. Prefijo de log: <c>[COMPAT]</c>.</summary>
    Compat,
}
