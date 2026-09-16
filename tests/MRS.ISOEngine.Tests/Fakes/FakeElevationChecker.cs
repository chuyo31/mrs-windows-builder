using MRS.ISOEngine.Pipeline;

namespace MRS.ISOEngine.Tests.Fakes;

public sealed class FakeElevationChecker : IElevationChecker
{
    public bool Elevated { get; set; } = true;

    public bool IsElevated() => Elevated;
}
