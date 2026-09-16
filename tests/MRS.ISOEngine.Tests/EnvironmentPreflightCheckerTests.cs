using MRS.ISOEngine.Pipeline;
using MRS.ISOEngine.Tests.Fakes;
using Xunit;

namespace MRS.ISOEngine.Tests;

/// <summary>P20, sección 1: los dos mensajes de aborto son literales y no deben cambiar.</summary>
public sealed class EnvironmentPreflightCheckerTests
{
    [Fact]
    public void Elevated_and_oscdimg_available_is_ready_with_no_errors()
    {
        var result = EnvironmentPreflightChecker.Check(
            new FakeElevationChecker { Elevated = true },
            new FakeOscdimgRunner { Available = true });

        Assert.True(result.IsReady);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Not_elevated_reports_the_exact_required_message()
    {
        var result = EnvironmentPreflightChecker.Check(
            new FakeElevationChecker { Elevated = false },
            new FakeOscdimgRunner { Available = true });

        Assert.False(result.IsReady);
        Assert.Contains("Se requieren privilegios de administrador para ejecutar DISM.", result.Errors);
    }

    [Fact]
    public void Missing_oscdimg_reports_the_exact_required_message()
    {
        var result = EnvironmentPreflightChecker.Check(
            new FakeElevationChecker { Elevated = true },
            new FakeOscdimgRunner { Available = false });

        Assert.False(result.IsReady);
        Assert.Contains("Windows ADK/oscdimg no está instalado.", result.Errors);
    }

    [Fact]
    public void Both_missing_reports_both_messages()
    {
        var result = EnvironmentPreflightChecker.Check(
            new FakeElevationChecker { Elevated = false },
            new FakeOscdimgRunner { Available = false });

        Assert.False(result.IsReady);
        Assert.Equal(2, result.Errors.Count);
    }
}
