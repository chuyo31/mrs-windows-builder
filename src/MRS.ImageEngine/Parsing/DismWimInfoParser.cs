using System.Text.RegularExpressions;
using MRS.ImageEngine.Models;

namespace MRS.ImageEngine.Parsing;

/// <summary>Una entrada del listado de índices (<c>DISM /Get-WimInfo</c> sin <c>/Index</c>).</summary>
public sealed record EditionListEntry(int Index, string Name, string Description);

/// <summary>
/// Parser de la salida de <c>DISM /Get-WimInfo</c> (forzada a <c>/English</c>).
///
/// El parseo es genérico: divide la salida en bloques "clave : valor" (un bloque
/// nuevo empieza en cada línea <c>Index :</c>) y no depende del orden de los
/// campos. Las líneas de continuación indentadas (típico de <c>Languages :</c>)
/// se adjuntan a la última clave. Preparado para Windows en inglés o español,
/// ya que siempre se solicita la salida en inglés.
/// </summary>
public sealed partial class DismWimInfoParser
{
    /// <summary>Lista los índices disponibles en la imagen.</summary>
    public IReadOnlyList<EditionListEntry> ParseEditionList(string dismOutput)
    {
        var entries = new List<EditionListEntry>();

        foreach (var block in SplitIntoBlocks(dismOutput))
        {
            if (!block.TryGetValue("Index", out var indexRaw) ||
                !int.TryParse(indexRaw, out var index))
                continue;

            block.TryGetValue("Name", out var name);
            block.TryGetValue("Description", out var description);

            entries.Add(new EditionListEntry(
                index,
                name?.Trim() ?? string.Empty,
                Coalesce(description, name)));
        }

        return entries;
    }

    /// <summary>
    /// Extrae el detalle de un índice concreto (<c>DISM /Get-WimInfo /Index:N</c>).
    /// </summary>
    public ImageEdition ParseEditionDetail(
        string dismOutput,
        int index,
        string? fallbackName = null,
        string? fallbackDescription = null)
    {
        var blocks = SplitIntoBlocks(dismOutput);
        var block =
            blocks.FirstOrDefault(b =>
                b.TryGetValue("Index", out var i) && int.TryParse(i, out var n) && n == index)
            ?? blocks.FirstOrDefault()
            ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        block.TryGetValue("Name", out var name);
        block.TryGetValue("Description", out var description);
        block.TryGetValue("Architecture", out var architectureRaw);
        block.TryGetValue("Version", out var version);
        block.TryGetValue("ServicePack Build", out var servicePackBuild);
        block.TryGetValue("Edition", out var edition);

        var language = ExtractLanguage(block);
        var buildNumber = ExtractBuildNumberText(version);
        var readableBuild = ComposeBuild(buildNumber, servicePackBuild);

        return new ImageEdition
        {
            Index = index,
            Name = Coalesce(name, fallbackName),
            Description = Coalesce(description, fallbackDescription, name, fallbackName),
            Architecture = ParseArchitecture(architectureRaw),
            Version = NullIfBlank(version),
            ServicePackBuild = NullIfBlank(servicePackBuild),
            Build = NullIfBlank(readableBuild),
            Language = language,
            Edition = NullIfBlank(edition),
        };
    }

    /// <summary>Normaliza la arquitectura textual de DISM.</summary>
    public static ImageArchitecture ParseArchitecture(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return ImageArchitecture.Unknown;

        var value = raw.Trim().ToLowerInvariant();

        if (value.Contains("arm64") || value.Contains("aarch64"))
            return ImageArchitecture.Arm64;
        if (value.Contains("x64") || value.Contains("amd64") || value == "9")
            return ImageArchitecture.X64;
        if (value.Contains("x86") || value.Contains("i386") || value == "0")
            return ImageArchitecture.X86;

        return ImageArchitecture.Unknown;
    }

    // ---- internos -------------------------------------------------------------

    private static string? ExtractLanguage(IReadOnlyDictionary<string, string> block)
    {
        foreach (var key in new[] { "Default Language", "Languages", "Language" })
        {
            if (!block.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
                continue;

            var token = value
                .Split(new[] { ' ', '\t', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(token))
                return token.Trim();
        }

        return null;
    }

    private static string? ExtractBuildNumberText(string? version)
    {
        if (string.IsNullOrWhiteSpace(version) || !version.Contains('.'))
            return null;

        var tail = version[(version.LastIndexOf('.') + 1)..].Trim();
        return string.IsNullOrEmpty(tail) ? null : tail;
    }

    private static string? ComposeBuild(string? buildNumber, string? servicePackBuild)
    {
        if (string.IsNullOrWhiteSpace(buildNumber))
            return null;

        return string.IsNullOrWhiteSpace(servicePackBuild)
            ? buildNumber
            : $"{buildNumber}.{servicePackBuild.Trim()}";
    }

    private static IReadOnlyList<Dictionary<string, string>> SplitIntoBlocks(string output)
    {
        var blocks = new List<Dictionary<string, string>>();
        Dictionary<string, string>? current = null;
        string? lastKey = null;

        foreach (var rawLine in output.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');

            if (string.IsNullOrWhiteSpace(line))
            {
                lastKey = null;
                continue;
            }

            var match = KeyValueRegex().Match(line);
            if (match.Success)
            {
                var key = match.Groups["key"].Value.Trim();
                var value = match.Groups["value"].Value.Trim();

                if (key.Equals("Index", StringComparison.OrdinalIgnoreCase) || current is null)
                {
                    current = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    blocks.Add(current);
                }

                current[key] = value;
                lastKey = key;
            }
            else if (current is not null && lastKey is not null &&
                     current.TryGetValue(lastKey, out var existing) && existing.Length == 0)
            {
                // Continuación indentada de la clave anterior (p. ej. "Languages :").
                current[lastKey] = line.Trim();
            }
        }

        var withIndex = blocks.Where(b => b.ContainsKey("Index")).ToList();
        return withIndex.Count > 0 ? withIndex : blocks;
    }

    private static string Coalesce(params string?[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (!string.IsNullOrWhiteSpace(candidate))
                return candidate.Trim();
        }

        return string.Empty;
    }

    private static string? NullIfBlank(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    [GeneratedRegex(@"^\s*(?<key>[A-Za-z][A-Za-z0-9 /_.-]*?)\s*:\s*(?<value>.*?)\s*$")]
    private static partial Regex KeyValueRegex();
}
