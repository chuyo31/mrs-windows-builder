using MRS.ImageEngine.Parsing;
using Xunit;

namespace MRS.ImageEngine.Tests;

public class WindowsVersionResolverTests
{
    [Theory]
    [InlineData("10.0.26300", "9278", 26300)]
    [InlineData("10.0.19044", null, 19044)]
    [InlineData("10.0.22000", "", 22000)]
    [InlineData("", null, 0)]
    [InlineData(null, null, 0)]
    [InlineData("no-version", null, 0)]
    public void ExtractBuildNumber_reads_third_component(string? version, string? servicePackBuild, int expected)
    {
        Assert.Equal(expected, WindowsVersionResolver.ExtractBuildNumber(version, servicePackBuild));
    }

    [Theory]
    [InlineData(26300, "Windows 11")]
    [InlineData(22000, "Windows 11")]
    [InlineData(19045, "Windows 10")]
    [InlineData(10240, "Windows 10")]
    public void ResolveProductName_from_build(int build, string expected)
    {
        Assert.Equal(expected, WindowsVersionResolver.ResolveProductName(build));
    }

    [Fact]
    public void ResolveProductName_falls_back_to_edition_name_when_build_unknown()
    {
        Assert.Equal("Windows 11", WindowsVersionResolver.ResolveProductName(0, "Windows 11 Pro"));
    }

    [Theory]
    [InlineData(26300, "26H2")]
    [InlineData(26100, "24H2")]
    [InlineData(22631, "23H2")]
    [InlineData(19045, "22H2")]
    public void ResolveDisplayVersion_maps_known_builds(int build, string expected)
    {
        Assert.Equal(expected, WindowsVersionResolver.ResolveDisplayVersion(build));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(99999)]
    public void ResolveDisplayVersion_returns_null_for_unknown_builds(int build)
    {
        Assert.Null(WindowsVersionResolver.ResolveDisplayVersion(build));
    }
}
