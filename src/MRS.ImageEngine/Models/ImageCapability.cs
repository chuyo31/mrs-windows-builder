namespace MRS.ImageEngine.Models;

/// <summary>Capacidad opcional (<c>DISM /Get-Capabilities</c>).</summary>
public sealed record ImageCapability
{
    public string Identity { get; init; } = string.Empty;
    public string? State { get; init; }
}
