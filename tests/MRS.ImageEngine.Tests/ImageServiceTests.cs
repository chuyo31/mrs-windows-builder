using MRS.ImageEngine.Models;
using MRS.ImageEngine.Tests.Data;
using MRS.ImageEngine.Tests.Fakes;
using Xunit;

namespace MRS.ImageEngine.Tests;

public class ImageServiceTests
{
    private static FakeDismRunner TwoEditionRunner() => new()
    {
        ListOutput = DismOutputs.ListTwoEditions,
        Details =
        {
            [1] = (DismOutputs.DetailIndex1, 0),
            [2] = (DismOutputs.DetailIndex2, 0),
        }
    };

    [Fact]
    public async Task AnalyzeImageFileAsync_aggregates_expected_values()
    {
        var service = new ImageService(TwoEditionRunner());

        var info = await service.AnalyzeImageFileAsync(@"X:\sources\install.wim");

        Assert.Equal("Windows 11", info.OperatingSystem);
        Assert.Equal("26H2", info.DisplayVersion);
        Assert.Equal("10.0.26300", info.Version);
        Assert.Equal("26300.9278", info.Build);
        Assert.Equal(ImageArchitecture.X64, info.Architecture);
        Assert.Equal("es-ES", info.Language);
        Assert.Equal(ImageFormat.Wim, info.Format);
    }

    [Fact]
    public async Task AnalyzeImageFileAsync_returns_all_editions()
    {
        var service = new ImageService(TwoEditionRunner());

        var info = await service.AnalyzeImageFileAsync(@"X:\sources\install.wim");

        Assert.Equal(2, info.Editions.Count);
        Assert.Collection(info.Editions,
            e => Assert.Equal("Windows 11 Home", e.Name),
            e => Assert.Equal("Windows 11 Pro", e.Name));
        Assert.Equal(new[] { 1, 2 }, info.Editions.Select(e => e.Index).ToArray());
    }

    [Fact]
    public async Task AnalyzeImageFileAsync_detects_esd_container()
    {
        var runner = new FakeDismRunner
        {
            ListOutput = DismOutputs.ListSingleEdition,
            Details = { [1] = (DismOutputs.DetailIndex2, 0) }
        };
        var service = new ImageService(runner);

        var info = await service.AnalyzeImageFileAsync(@"X:\sources\install.esd");

        Assert.Equal(ImageFormat.Esd, info.Format);
        Assert.Single(info.Editions);
    }

    [Fact]
    public async Task AnalyzeImageFileAsync_throws_with_exit_code_when_list_fails()
    {
        var runner = new FakeDismRunner { ListOutput = DismOutputs.DismFailure, ListExitCode = 1910 };
        var service = new ImageService(runner);

        var ex = await Assert.ThrowsAsync<ImageAnalysisException>(
            () => service.AnalyzeImageFileAsync(@"X:\sources\install.wim"));

        Assert.Equal(1910, ex.ExitCode);
        Assert.DoesNotContain("detail:", runner.Calls);
    }

    [Fact]
    public async Task AnalyzeImageFileAsync_throws_with_exit_code_when_detail_fails()
    {
        var runner = new FakeDismRunner
        {
            ListOutput = DismOutputs.ListTwoEditions,
            Details = { [1] = (string.Empty, 87) },
            DefaultDetailExitCode = 87
        };
        var service = new ImageService(runner);

        var ex = await Assert.ThrowsAsync<ImageAnalysisException>(
            () => service.AnalyzeImageFileAsync(@"X:\sources\install.wim"));

        Assert.Equal(87, ex.ExitCode);
    }

    [Fact]
    public async Task AnalyzeImageFileAsync_throws_when_dism_times_out()
    {
        var runner = new FakeDismRunner { ListOutput = string.Empty, ListTimedOut = true };
        var service = new ImageService(runner);

        var ex = await Assert.ThrowsAsync<ImageAnalysisException>(
            () => service.AnalyzeImageFileAsync(@"X:\sources\install.wim"));

        Assert.Equal(-1, ex.ExitCode);
    }

    [Fact]
    public async Task AnalyzeImageFileAsync_throws_when_no_indexes_returned()
    {
        var runner = new FakeDismRunner { ListOutput = "sin indices", ListExitCode = 0 };
        var service = new ImageService(runner);

        await Assert.ThrowsAsync<ImageAnalysisException>(
            () => service.AnalyzeImageFileAsync(@"X:\sources\install.wim"));
    }

    [Fact]
    public async Task AnalyzeImageFileAsync_queries_list_before_each_index_detail()
    {
        var runner = TwoEditionRunner();
        var service = new ImageService(runner);

        await service.AnalyzeImageFileAsync(@"X:\sources\install.wim");

        Assert.Equal(
            new[] { @"list:X:\sources\install.wim", "detail:1", "detail:2" },
            runner.Calls.ToArray());
    }
}
