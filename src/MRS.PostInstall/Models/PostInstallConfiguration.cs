namespace MRS.PostInstall.Models;

/// <summary>
/// Configuración de PostInstall (P18): qué runtime/paquete instalar tras el
/// primer arranque de Windows. Solo guarda nombres/versiones/flags — nunca una
/// ruta absoluta del equipo del desarrollador (esas viven en
/// <see cref="PostInstallSourceFiles"/>, que es un parámetro de entrada en
/// tiempo de ejecución, no un valor por defecto de este modelo).
/// </summary>
public sealed record PostInstallConfiguration
{
    /// <summary>Si es <c>false</c>, no se genera ningún paquete PostInstall.</summary>
    public bool Enabled { get; init; } = true;

    public string DotNetRuntimeVersion { get; init; } = "8.0.26";

    /// <summary>Única arquitectura soportada en esta fase.</summary>
    public string Architecture { get; init; } = "x64";

    public string DotNetInstallerFileName { get; init; } = "windowsdesktop-runtime-8.0.26-win-x64.exe";

    public string PcpiFileName { get; init; } = "PCPI-Retro-Minimals-Portable-0.0.5.exe";

    /// <summary>Si es <c>false</c>, el paquete solo instala el runtime y nunca lanza PCPI.</summary>
    public bool RunPcpiAfterRuntime { get; init; } = true;
}
