using System.Text.RegularExpressions;

namespace MRS.ImageEngine.Parsing;

/// <summary>
/// Utilidades para derivar datos legibles a partir de los valores crudos de
/// DISM. DISM no expone directamente el nombre comercial ("Windows 11") ni la
/// versión comercial ("26H2"), así que se calculan a partir del número de build.
/// El mapa cubre Windows 10 y Windows 11 y devuelve <c>null</c> si la build no
/// se reconoce (nunca se inventa un valor).
/// </summary>
public static partial class WindowsVersionResolver
{
    private static readonly IReadOnlyDictionary<int, string> DisplayVersionsByBuild = new Dictionary<int, string>
    {
        [10240] = "1507",
        [10586] = "1511",
        [14393] = "1607",
        [15063] = "1703",
        [16299] = "1709",
        [17134] = "1803",
        [17763] = "1809",
        [18362] = "1903",
        [18363] = "1909",
        [19041] = "2004",
        [19042] = "20H2",
        [19043] = "21H1",
        [19044] = "21H2",
        [19045] = "22H2",
        [22000] = "21H2",
        [22621] = "22H2",
        [22631] = "23H2",
        [26100] = "24H2",
        [26200] = "25H2",
        [26300] = "26H2",
    };

    /// <summary>
    /// Extrae el número de build (p. ej. 26300) desde la versión cruda de DISM
    /// ("10.0.26300"). Devuelve 0 si no se puede determinar.
    /// </summary>
    public static int ExtractBuildNumber(string? version, string? servicePackBuild = null)
    {
        if (!string.IsNullOrWhiteSpace(version))
        {
            var parts = version.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (parts.Length >= 3 && int.TryParse(parts[2], out var third))
                return third;

            if (parts.Length > 0 && int.TryParse(parts[^1], out var last) && last > 1000)
                return last;
        }

        return 0;
    }

    /// <summary>Nombre comercial del sistema, p. ej. "Windows 11".</summary>
    public static string ResolveProductName(int build, string? editionName = null)
    {
        if (build >= 22000) return "Windows 11";
        if (build >= 10240) return "Windows 10";

        if (!string.IsNullOrWhiteSpace(editionName))
        {
            var match = ProductPrefixRegex().Match(editionName);
            if (match.Success)
                return match.Groups[1].Value;
        }

        return build > 0 ? $"Windows (build {build})" : "Windows";
    }

    /// <summary>Versión comercial, p. ej. "26H2". <c>null</c> si no se reconoce.</summary>
    public static string? ResolveDisplayVersion(int build)
        => DisplayVersionsByBuild.TryGetValue(build, out var value) ? value : null;

    [GeneratedRegex(@"^(Windows\s+\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex ProductPrefixRegex();
}
