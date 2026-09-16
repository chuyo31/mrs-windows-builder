using MRS.ISOEngine.Oscdimg;

namespace MRS.ISOEngine.Pipeline;

/// <summary>
/// Implementa la comprobación de entorno exigida por P20, sección 1, con los
/// dos mensajes de error exactos que el prompt requiere. Es intencionadamente
/// independiente de <see cref="IsoGenerationRequestValidator"/>: esa clase
/// valida la coherencia de la petición (rutas, índices, configuración);
/// esta clase valida si la máquina donde se ejecuta puede siquiera intentar
/// una generación real, algo que no depende de la petición en absoluto.
/// </summary>
public static class EnvironmentPreflightChecker
{
    public static EnvironmentPreflightResult Check(IElevationChecker elevationChecker, IOscdimgRunner oscdimgRunner)
    {
        var errors = new List<string>();

        if (!elevationChecker.IsElevated())
            errors.Add("Se requieren privilegios de administrador para ejecutar DISM.");

        if (!oscdimgRunner.IsAvailable())
            errors.Add("Windows ADK/oscdimg no está instalado.");

        return errors.Count == 0
            ? EnvironmentPreflightResult.Ready
            : new EnvironmentPreflightResult(false, errors);
    }
}
