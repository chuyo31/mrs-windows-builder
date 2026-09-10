using MRS.ImageEngine.Models;
using MRS.ImageEngine.Parsing;
using MRS.ImageEngine.Tests.Data;
using Xunit;

namespace MRS.ImageEngine.Tests;

public class DismWimInfoParserTests
{
    private readonly DismWimInfoParser _parser = new();

    [Fact]
    public void ParseEditionList_returns_every_index()
    {
        var entries = _parser.ParseEditionList(DismOutputs.ListTwoEditions);

        Assert.Equal(2, entries.Count);
        Assert.Equal(new[] { 1, 2 }, entries.Select(e => e.Index).ToArray());
        Assert.Equal("Windows 11 Home", entries[0].Name);
        Assert.Equal("Windows 11 Pro", entries[1].Name);
        Assert.Equal("Windows 11 Pro", entries[1].Description);
    }

    [Fact]
    public void ParseEditionList_supports_single_index()
    {
        var entries = _parser.ParseEditionList(DismOutputs.ListSingleEdition);

        var entry = Assert.Single(entries);
        Assert.Equal(1, entry.Index);
        Assert.Equal("Windows 11 Pro", entry.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("texto sin estructura de DISM")]
    public void ParseEditionList_returns_empty_for_unusable_output(string output)
    {
        Assert.Empty(_parser.ParseEditionList(output));
    }

    [Fact]
    public void ParseEditionDetail_extracts_core_fields_with_continuation_language()
    {
        var edition = _parser.ParseEditionDetail(DismOutputs.DetailIndex1, index: 1);

        Assert.Equal(1, edition.Index);
        Assert.Equal("Windows 11 Home", edition.Name);
        Assert.Equal(ImageArchitecture.X64, edition.Architecture);
        Assert.Equal("10.0.26300", edition.Version);
        Assert.Equal("9278", edition.ServicePackBuild);
        Assert.Equal("26300.9278", edition.Build);
        Assert.Equal("es-ES", edition.Language);
        Assert.Equal("Core", edition.Edition);
    }

    [Fact]
    public void ParseEditionDetail_extracts_language_on_same_line()
    {
        var edition = _parser.ParseEditionDetail(DismOutputs.DetailIndex2, index: 2);

        Assert.Equal("Windows 11 Pro", edition.Name);
        Assert.Equal("es-ES", edition.Language);
        Assert.Equal("Professional", edition.Edition);
        Assert.Equal("26300.9278", edition.Build);
    }

    [Fact]
    public void ParseEditionDetail_reads_arm64_and_different_version()
    {
        var edition = _parser.ParseEditionDetail(DismOutputs.DetailArm64Windows10, index: 1);

        Assert.Equal(ImageArchitecture.Arm64, edition.Architecture);
        Assert.Equal("10.0.19044", edition.Version);
        Assert.Equal("19044.1288", edition.Build);
        Assert.Equal("en-US", edition.Language);
    }

    [Fact]
    public void ParseEditionDetail_uses_fallbacks_when_missing()
    {
        var edition = _parser.ParseEditionDetail("sin datos", index: 3, fallbackName: "Windows 11 Team", fallbackDescription: "Superficie");

        Assert.Equal(3, edition.Index);
        Assert.Equal("Windows 11 Team", edition.Name);
        Assert.Equal("Superficie", edition.Description);
        Assert.Equal(ImageArchitecture.Unknown, edition.Architecture);
        Assert.Null(edition.Build);
    }

    [Theory]
    [InlineData("x64", ImageArchitecture.X64)]
    [InlineData("X64", ImageArchitecture.X64)]
    [InlineData("amd64", ImageArchitecture.X64)]
    [InlineData("x86", ImageArchitecture.X86)]
    [InlineData("i386", ImageArchitecture.X86)]
    [InlineData("ARM64", ImageArchitecture.Arm64)]
    [InlineData("arm64", ImageArchitecture.Arm64)]
    [InlineData("", ImageArchitecture.Unknown)]
    [InlineData("nonsense", ImageArchitecture.Unknown)]
    public void ParseArchitecture_maps_dism_values(string raw, ImageArchitecture expected)
    {
        Assert.Equal(expected, DismWimInfoParser.ParseArchitecture(raw));
    }
}
