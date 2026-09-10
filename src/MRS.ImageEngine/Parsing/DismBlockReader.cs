using System.Text.RegularExpressions;

namespace MRS.ImageEngine.Parsing;

/// <summary>
/// Divide la salida <c>/English</c> de DISM en bloques "clave : valor".
///
/// Robusto frente a: distinto orden de propiedades, campos opcionales, líneas
/// vacías, múltiples elementos, continuaciones de línea y cambios menores de
/// formato. Un bloque nuevo empieza cuando aparece cualquiera de las
/// <paramref name="boundaryKeys"/> (la primera propiedad de cada elemento en la
/// salida de DISM). Sólo se devuelven bloques que contengan una clave de
/// frontera, por lo que el "banner" de DISM se descarta.
/// </summary>
public static partial class DismBlockReader
{
    public static IReadOnlyList<IReadOnlyDictionary<string, string>> Read(string output, params string[] boundaryKeys)
    {
        if (string.IsNullOrEmpty(output) || boundaryKeys.Length == 0)
            return Array.Empty<IReadOnlyDictionary<string, string>>();

        var boundaries = new HashSet<string>(boundaryKeys, StringComparer.OrdinalIgnoreCase);
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

                if (current is null || boundaries.Contains(key))
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
                current[lastKey] = line.Trim();
            }
        }

        var withBoundary = blocks
            .Where(b => b.Keys.Any(boundaries.Contains))
            .Cast<IReadOnlyDictionary<string, string>>()
            .ToList();

        return withBoundary;
    }

    /// <summary>Devuelve el valor o cadena vacía.</summary>
    public static string Value(IReadOnlyDictionary<string, string> block, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (block.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        return string.Empty;
    }

    /// <summary>Devuelve el valor o <c>null</c> si falta o está vacío.</summary>
    public static string? Optional(IReadOnlyDictionary<string, string> block, params string[] keys)
    {
        var value = Value(block, keys);
        return value.Length == 0 ? null : value;
    }

    /// <summary>Interpreta "Yes"/"No" (o vacío) de forma tolerante.</summary>
    public static bool? YesNo(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var v = value.Trim().ToLowerInvariant();
        return v is "yes" or "true" or "1"
            ? true
            : v is "no" or "false" or "0" ? false : (bool?)null;
    }

    [GeneratedRegex(@"^\s*(?<key>[A-Za-z][A-Za-z0-9 /_.-]*?)\s*:\s*(?<value>.*?)\s*$")]
    private static partial Regex KeyValueRegex();
}
