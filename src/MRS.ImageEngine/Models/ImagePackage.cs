namespace MRS.ImageEngine.Models;

/// <summary>Paquete instalado en la imagen (<c>DISM /Get-Packages</c>).</summary>
public sealed record ImagePackage
{
    public string PackageIdentity { get; init; } = string.Empty;
    public string? State { get; init; }
    public string? ReleaseType { get; init; }
    public string? InstallTime { get; init; }
    public string? Description { get; init; }
}
