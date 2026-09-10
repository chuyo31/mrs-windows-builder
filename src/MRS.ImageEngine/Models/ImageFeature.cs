namespace MRS.ImageEngine.Models;

/// <summary>Característica de Windows (<c>DISM /Get-Features</c>).</summary>
public sealed record ImageFeature
{
    public string Name { get; init; } = string.Empty;
    public string? State { get; init; }
}
