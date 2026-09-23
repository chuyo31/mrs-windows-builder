using MRS.ISOEngine.InstallWim;
using MRS.ISOEngine.Models;

namespace MRS.ISOEngine.Tests.Fakes;

internal sealed class FakeInstallWimOobeConfigurator : IInstallWimOobeConfigurator
{
    public bool Success { get; set; } = true;
    public IReadOnlyList<string> Errors { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> AppliedLogLines { get; set; } = new[] { "[COMPAT] BypassNRO applied to install.wim" };
    public int CallCount { get; private set; }
    public (string InstallWimPath, int Index, string MountDir)? LastCall { get; private set; }

    public Task<InstallWimOobeConfigurationResult> ApplyOfflineOobeBypassAsync(
        string installWimPath, int index, string mountDir,
        CancellationToken cancellationToken = default, IProgress<InstallationProgressInfo>? progress = null)
    {
        CallCount++;
        LastCall = (installWimPath, index, mountDir);
        return Task.FromResult(new InstallWimOobeConfigurationResult(Success, AppliedLogLines, Errors));
    }
}
