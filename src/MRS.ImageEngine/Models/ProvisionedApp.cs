namespace MRS.ImageEngine.Models;

/// <summary>
/// Aplicación provisionada en la imagen
/// (<c>DISM /Get-ProvisionedAppxPackages</c>).
/// </summary>
public sealed record ProvisionedApp
{
    public string DisplayName { get; init; } = string.Empty;
    public string PackageName { get; init; } = string.Empty;
    public string? Version { get; init; }
    public string? Architecture { get; init; }
    public string? ResourceId { get; init; }
    public string? PublisherId { get; init; }
}
