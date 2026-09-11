using System.Text.RegularExpressions;

namespace MRS.RemovalEngine;

/// <summary>Utilidades sobre la salida de <c>DISM /Get-MountedWimInfo</c>.</summary>
internal static partial class MountedWimInfo
{
    public static IEnumerable<string> ExtractMountDirs(string output)
        => output.Split('\n')
            .Select(line => MountDirRegex().Match(line.TrimEnd('\r')))
            .Where(match => match.Success)
            .Select(match => match.Groups["dir"].Value.Trim())
            .Where(dir => dir.Length > 0);

    public static bool ContainsMountDir(string output, string mountDir)
        => ExtractMountDirs(output).Any(dir => PathsEqual(dir, mountDir));

    public static bool IsUnder(string path, string root)
    {
        var full = Normalize(path);
        var normalizedRoot = Normalize(root);
        return full.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || string.Equals(full, normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static bool PathsEqual(string a, string b)
        => string.Equals(Normalize(a), Normalize(b), StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string path)
    {
        try { return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)); }
        catch { return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar); }
    }

    [GeneratedRegex(@"^\s*Mount Dir\s*:\s*(?<dir>.+?)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex MountDirRegex();
}
