namespace MRS.ISOEngine.Pipeline;

/// <summary>
/// Comprueba si el proceso actual tiene privilegios de administrador (P20,
/// sección 1). Separado en una interfaz para poder simularlo en tests: la
/// sesión que ejecuta los tests tampoco está elevada, y no tendría sentido
/// que el resultado de un test dependiera de cómo se lanzó el propio proceso
/// de pruebas.
/// </summary>
public interface IElevationChecker
{
    bool IsElevated();
}
