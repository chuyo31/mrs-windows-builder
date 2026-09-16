using MRS.RemovalEngine;
using MRS.RemovalEngine.Models;
using MRS.RemovalPlanning.Models;

namespace MRS.ISOEngine.Tests.Fakes;

internal sealed class FakeRemovalEngine : IRemovalEngine
{
    public bool Success { get; set; } = true;
    public IReadOnlyList<string> Errors { get; set; } = Array.Empty<string>();
    public int CallCount { get; private set; }
    public RemovalPlan? LastPlan { get; private set; }
    public WorkingImage? LastImage { get; private set; }

    public Task<RemovalExecutionResult> ExecuteAsync(
        WorkingImage image, RemovalPlan plan, CancellationToken cancellationToken = default,
        IProgress<ProgressInfo>? progress = null)
    {
        CallCount++;
        LastImage = image;
        LastPlan = plan;

        return Task.FromResult(new RemovalExecutionResult
        {
            Success = Success,
            Phase = Success ? RemovalExecutionPhase.Completed : RemovalExecutionPhase.Failed,
            Errors = Errors,
            Committed = Success,
            Discarded = !Success,
            Workspace = image.WorkspacePath,
            WorkingImage = image,
        });
    }
}
