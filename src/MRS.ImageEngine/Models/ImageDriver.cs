namespace MRS.ImageEngine.Models;

/// <summary>Driver de terceros de la imagen (<c>DISM /Get-Drivers</c>).</summary>
public sealed record ImageDriver
{
    public string PublishedName { get; init; } = string.Empty;
    public string? OriginalFileName { get; init; }
    public string? Provider { get; init; }
    public string? Class { get; init; }
    public string? Version { get; init; }
    public string? Date { get; init; }
    public bool? BootCritical { get; init; }
}
