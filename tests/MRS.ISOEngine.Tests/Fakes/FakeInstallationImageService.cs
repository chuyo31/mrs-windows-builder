using MRS.ISOEngine;
using MRS.ISOEngine.Models;
using InstallationOptionsModel = MRS.InstallationOptions.Models.InstallationOptions;

namespace MRS.ISOEngine.Tests.Fakes;

internal sealed class FakeInstallationImageService : IInstallationImageService
{
    public bool Success { get; set; } = true;
    public IReadOnlyList<string> Errors { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> AppliedLogLines { get; set; } = new[] { "[COMPAT] BypassTPM applied" };
    public int CallCount { get; private set; }
    public GenerationWorkspace? LastWorkspace { get; private set; }
    public InstallationOptionsModel? LastOptions { get; private set; }

    public bool FinalValidationSuccess { get; set; } = true;
    public IReadOnlyList<string> FinalValidationErrors { get; set; } = Array.Empty<string>();
    public int ValidateFinalCallCount { get; private set; }

    public Task<InstallationImageResult> ApplyAsync(
        string sourceIsoPath, GenerationWorkspace workspace, InstallationOptionsModel options,
        AutounattendConfiguration accountConfig, CancellationToken cancellationToken = default,
        IProgress<InstallationProgressInfo>? progress = null)
    {
        CallCount++;
        LastWorkspace = workspace;
        LastOptions = options;
        return Task.FromResult(new InstallationImageResult(Success, AppliedLogLines, Errors, Success ? "autounattend.xml" : null));
    }

    public Task<WorkspaceValidationResult> ValidateFinalAsync(
        GenerationWorkspace workspace, InstallationOptionsModel options, AutounattendConfiguration accountConfig,
        CancellationToken cancellationToken = default)
    {
        ValidateFinalCallCount++;
        return Task.FromResult(FinalValidationSuccess
            ? WorkspaceValidationResult.Valid
            : new WorkspaceValidationResult(false, FinalValidationErrors));
    }
}
