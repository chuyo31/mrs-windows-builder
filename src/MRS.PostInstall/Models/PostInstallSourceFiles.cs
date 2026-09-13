namespace MRS.PostInstall.Models;

/// <summary>
/// Rutas reales (en el equipo que genera el paquete) de los dos instaladores a
/// empaquetar. Son un parámetro de entrada en tiempo de ejecución — nunca una
/// ruta hardcodeada en el código ni en <see cref="PostInstallConfiguration"/>.
/// </summary>
public sealed record PostInstallSourceFiles
{
    public string DotNetInstallerPath { get; init; } = string.Empty;

    public string PcpiInstallerPath { get; init; } = string.Empty;
}
