using MRS.DismEngine.Dism;
using MRS.DismEngine.Processes;

namespace MRS.ImageEngine.Tests.Fakes;

/// <summary>
/// <see cref="IDismRunner"/> controlado: devuelve salidas y códigos de salida
/// predefinidos sin ejecutar DISM.
/// </summary>
internal sealed class FakeDismRunner : IDismRunner
{
    public string ListOutput { get; set; } = string.Empty;
    public int ListExitCode { get; set; }
    public bool ListTimedOut { get; set; }

    public Dictionary<int, (string Output, int ExitCode)> Details { get; } = new();
    public int DefaultDetailExitCode { get; set; }
    public bool DetailTimedOut { get; set; }

    public List<string> Calls { get; } = new();

    public Task<ProcessRunResult> GetWimInfoAsync(string imagePath, CancellationToken cancellationToken = default)
    {
        Calls.Add($"list:{imagePath}");
        return Task.FromResult(new ProcessRunResult(
            "Dism.exe",
            $"/Get-WimInfo /WimFile:\"{imagePath}\"",
            ListExitCode,
            ListOutput,
            string.Empty,
            TimeSpan.FromMilliseconds(5),
            ListTimedOut));
    }

    public Task<ProcessRunResult> GetWimInfoAsync(string imagePath, int index, CancellationToken cancellationToken = default)
    {
        Calls.Add($"detail:{index}");
        var (output, exitCode) = Details.TryGetValue(index, out var entry)
            ? entry
            : (string.Empty, DefaultDetailExitCode);

        return Task.FromResult(new ProcessRunResult(
            "Dism.exe",
            $"/Get-WimInfo /WimFile:\"{imagePath}\" /Index:{index}",
            exitCode,
            output,
            string.Empty,
            TimeSpan.FromMilliseconds(5),
            DetailTimedOut));
    }
}
