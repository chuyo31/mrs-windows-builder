namespace MRS.ImageEngine.Models;

/// <summary>
/// Inventario de SOLO LECTURA de una edición de la imagen. Independiente de la
/// interfaz. Los servicios y las tareas programadas quedan para una fase
/// posterior (requerirían cargar hives / modificar la imagen).
/// </summary>
public sealed record ImageInventory
{
    public IReadOnlyList<ImagePackage> Packages { get; init; } = Array.Empty<ImagePackage>();
    public IReadOnlyList<ImageFeature> Features { get; init; } = Array.Empty<ImageFeature>();
    public IReadOnlyList<ImageCapability> Capabilities { get; init; } = Array.Empty<ImageCapability>();
    public IReadOnlyList<ProvisionedApp> ProvisionedApps { get; init; } = Array.Empty<ProvisionedApp>();
    public IReadOnlyList<ImageDriver> Drivers { get; init; } = Array.Empty<ImageDriver>();

    public int PackageCount => Packages.Count;
    public int FeatureCount => Features.Count;
    public int CapabilityCount => Capabilities.Count;
    public int ProvisionedAppCount => ProvisionedApps.Count;
    public int DriverCount => Drivers.Count;

    public static ImageInventory Empty { get; } = new();
}
