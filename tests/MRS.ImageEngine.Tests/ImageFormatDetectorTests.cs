using MRS.ImageEngine.Models;
using MRS.ImageEngine.Parsing;
using Xunit;

namespace MRS.ImageEngine.Tests;

public class ImageFormatDetectorTests
{
    [Theory]
    [InlineData(@"X:\sources\install.wim", ImageFormat.Wim)]
    [InlineData(@"X:\sources\INSTALL.WIM", ImageFormat.Wim)]
    [InlineData(@"X:\sources\install.esd", ImageFormat.Esd)]
    [InlineData(@"X:\sources\install.ESD", ImageFormat.Esd)]
    [InlineData(@"C:\imagenes\win11.iso", ImageFormat.Unknown)]
    [InlineData("install", ImageFormat.Unknown)]
    [InlineData(null, ImageFormat.Unknown)]
    [InlineData("", ImageFormat.Unknown)]
    public void FromPath_detects_container_format(string? path, ImageFormat expected)
    {
        Assert.Equal(expected, ImageFormatDetector.FromPath(path));
    }
}
