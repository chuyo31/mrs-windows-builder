using MRS.PostInstall.Models;

namespace MRS.PostInstall.Execution;

/// <summary>
/// Único lugar donde se construyen las líneas de comandos reales (P18, sección
/// 5/6): "/install /quiet /norestart" para el runtime, y el lanzamiento sin
/// argumentos de PCPI. Tanto un <see cref="ICommandExecutor"/> real como
/// <c>SetupCompleteScriptGenerator</c> (que solo necesita <see cref="CommandExecutionSpec.Arguments"/>)
/// derivan de aquí — nunca se repite el literal en más de un sitio.
/// </summary>
public static class PostInstallCommands
{
    /// <param name="installerDirectory">
    /// Directorio donde vive <see cref="PostInstallConfiguration.DotNetInstallerFileName"/>.
    /// Puede ser una ruta absoluta real (ejecución/prueba en el equipo que
    /// construye el paquete) o un token de ruta de un script <c>.cmd</c> (p. ej.
    /// <c>%SCRIPT_DIR%dotnet</c>) — esta función no le da ningún significado
    /// especial, solo concatena.
    /// </param>
    public static CommandExecutionSpec DotNetRuntimeInstall(PostInstallConfiguration config, string installerDirectory)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentException.ThrowIfNullOrWhiteSpace(installerDirectory);

        return new CommandExecutionSpec
        {
            Executable = CombinePath(installerDirectory, config.DotNetInstallerFileName),
            Arguments = "/install /quiet /norestart",
            WorkingDirectory = installerDirectory,
            Timeout = TimeSpan.FromMinutes(15),
            ExpectedExitCodes = new[] { 0 },
        };
    }

    /// <param name="pcpiDirectory">Directorio donde vive <see cref="PostInstallConfiguration.PcpiFileName"/> (misma nota que <see cref="DotNetRuntimeInstall"/>).</param>
    public static CommandExecutionSpec Pcpi(PostInstallConfiguration config, string pcpiDirectory)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentException.ThrowIfNullOrWhiteSpace(pcpiDirectory);

        return new CommandExecutionSpec
        {
            Executable = CombinePath(pcpiDirectory, config.PcpiFileName),
            Arguments = string.Empty,
            WorkingDirectory = pcpiDirectory,
            Timeout = TimeSpan.FromMinutes(10),
            // PCPI es un ejecutable portable de terceros: no hay garantía de que use
            // el convenio "0 = éxito". Se documenta esta incertidumbre explícitamente
            // (P18, sección 7) en vez de asumir un contrato que no se ha confirmado.
            ExpectedExitCodes = new[] { 0 },
        };
    }

    private static string CombinePath(string directory, string fileName)
    {
        var trimmed = directory.TrimEnd('\\', '/');
        return $"{trimmed}\\{fileName}";
    }
}
