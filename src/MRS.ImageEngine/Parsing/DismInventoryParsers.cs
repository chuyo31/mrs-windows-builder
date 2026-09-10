using MRS.ImageEngine.Models;

namespace MRS.ImageEngine.Parsing;

/// <summary>Parser de <c>DISM /Get-Packages</c>.</summary>
public sealed class DismPackageParser
{
    public IReadOnlyList<ImagePackage> Parse(string output)
        => DismBlockReader.Read(output, "Package Identity")
            .Select(b => new ImagePackage
            {
                PackageIdentity = DismBlockReader.Value(b, "Package Identity"),
                State = DismBlockReader.Optional(b, "State"),
                ReleaseType = DismBlockReader.Optional(b, "Release Type"),
                InstallTime = DismBlockReader.Optional(b, "Install Time"),
                Description = DismBlockReader.Optional(b, "Description"),
            })
            .Where(p => p.PackageIdentity.Length > 0)
            .ToList();
}

/// <summary>Parser de <c>DISM /Get-Features</c>.</summary>
public sealed class DismFeatureParser
{
    public IReadOnlyList<ImageFeature> Parse(string output)
        => DismBlockReader.Read(output, "Feature Name")
            .Select(b => new ImageFeature
            {
                Name = DismBlockReader.Value(b, "Feature Name"),
                State = DismBlockReader.Optional(b, "State"),
            })
            .Where(f => f.Name.Length > 0)
            .ToList();
}

/// <summary>Parser de <c>DISM /Get-Capabilities</c>.</summary>
public sealed class DismCapabilityParser
{
    public IReadOnlyList<ImageCapability> Parse(string output)
        => DismBlockReader.Read(output, "Capability Identity")
            .Select(b => new ImageCapability
            {
                Identity = DismBlockReader.Value(b, "Capability Identity"),
                State = DismBlockReader.Optional(b, "State"),
            })
            .Where(c => c.Identity.Length > 0)
            .ToList();
}

/// <summary>Parser de <c>DISM /Get-ProvisionedAppxPackages</c>.</summary>
public sealed class DismProvisionedAppParser
{
    public IReadOnlyList<ProvisionedApp> Parse(string output)
        => DismBlockReader.Read(output, "DisplayName")
            .Select(b =>
            {
                var packageName = DismBlockReader.Value(b, "PackageName");
                return new ProvisionedApp
                {
                    DisplayName = DismBlockReader.Value(b, "DisplayName"),
                    PackageName = packageName,
                    Version = DismBlockReader.Optional(b, "Version"),
                    Architecture = DismBlockReader.Optional(b, "Architecture"),
                    ResourceId = DismBlockReader.Optional(b, "ResourceId"),
                    PublisherId = DismBlockReader.Optional(b, "PublisherId")
                                  ?? ExtractPublisherId(packageName),
                };
            })
            .Where(a => a.DisplayName.Length > 0 || a.PackageName.Length > 0)
            .ToList();

    /// <summary>
    /// El PackageName tiene la forma
    /// <c>Nombre_Version_Arquitectura_ResourceId_PublisherId</c>; el último
    /// segmento es el PublisherId.
    /// </summary>
    internal static string? ExtractPublisherId(string packageName)
    {
        if (string.IsNullOrWhiteSpace(packageName))
            return null;

        var parts = packageName.Split('_', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 ? parts[^1] : null;
    }
}

/// <summary>Parser de <c>DISM /Get-Drivers</c>.</summary>
public sealed class DismDriverParser
{
    public IReadOnlyList<ImageDriver> Parse(string output)
        => DismBlockReader.Read(output, "Published Name")
            .Select(b => new ImageDriver
            {
                PublishedName = DismBlockReader.Value(b, "Published Name"),
                OriginalFileName = DismBlockReader.Optional(b, "Original File Name"),
                Provider = DismBlockReader.Optional(b, "Provider Name", "Provider"),
                Class = DismBlockReader.Optional(b, "Class Name", "Class"),
                Version = DismBlockReader.Optional(b, "Version"),
                Date = DismBlockReader.Optional(b, "Date"),
                BootCritical = DismBlockReader.YesNo(DismBlockReader.Optional(b, "Boot Critical")),
            })
            .Where(d => d.PublishedName.Length > 0)
            .ToList();
}
