using System.IO;
using AvyscanLab.Services;
using Xunit;

namespace AvyscanLab.Tests.Services;

public class SourceServiceTests
{
    private readonly SourceService _svc = new();

    // ── NormalizeConfiguredPath ───────────────────────────────────────────────

    [Fact]
    public void Normalize_TrimsWhitespace()
    {
        Assert.Equal("hello", _svc.NormalizeConfiguredPath("  hello  "));
    }

    [Fact]
    public void Normalize_RemovesSurroundingDoubleQuotes()
    {
        Assert.Equal(@"C:\path\file.avi", _svc.NormalizeConfiguredPath(@"""C:\path\file.avi"""));
    }

    [Fact]
    public void Normalize_TrimsBeforeStrippingQuotes()
    {
        Assert.Equal("file", _svc.NormalizeConfiguredPath("  \"file\"  "));
    }

    [Fact]
    public void Normalize_DoesNotStrip_WhenOnlyOneQuote()
    {
        Assert.Equal("\"file", _svc.NormalizeConfiguredPath("\"file"));
    }

    [Fact]
    public void Normalize_TreatsNullAsEmptyString()
    {
        Assert.Equal("", _svc.NormalizeConfiguredPath(null!));
    }

    // ── IsVideoSource ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData("video.avi")]
    [InlineData("video.mp4")]
    [InlineData("video.mov")]
    [InlineData("video.mkv")]
    [InlineData("video.wmv")]
    [InlineData("video.m4v")]
    [InlineData("video.mpeg")]
    [InlineData("video.mpg")]
    [InlineData("video.webm")]
    public void IsVideoSource_ReturnsTrue_ForEveryVideoExtension(string path)
    {
        Assert.True(_svc.IsVideoSource(path));
    }

    [Fact]
    public void IsVideoSource_IsCaseInsensitive()
    {
        Assert.True(_svc.IsVideoSource("video.AVI"));
        Assert.True(_svc.IsVideoSource("video.MP4"));
    }

    [Fact]
    public void IsVideoSource_HandlesQuotedPath()
    {
        Assert.True(_svc.IsVideoSource("\"video.mp4\""));
    }

    [Theory]
    [InlineData("image.tif")]
    [InlineData("document.pdf")]
    [InlineData("noextension")]
    [InlineData("")]
    public void IsVideoSource_ReturnsFalse_ForNonVideoExtensions(string path)
    {
        Assert.False(_svc.IsVideoSource(path));
    }

    // ── IsImageSource ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData("scan.tif")]
    [InlineData("scan.tiff")]
    [InlineData("scan.jpg")]
    [InlineData("scan.jpeg")]
    [InlineData("scan.png")]
    [InlineData("scan.bmp")]
    public void IsImageSource_ReturnsTrue_ForEveryImageExtension(string path)
    {
        Assert.True(_svc.IsImageSource(path));
    }

    [Fact]
    public void IsImageSource_IsCaseInsensitive()
    {
        Assert.True(_svc.IsImageSource("scan.TIF"));
        Assert.True(_svc.IsImageSource("scan.JPG"));
    }

    [Theory]
    [InlineData("video.mp4")]
    [InlineData("document.pdf")]
    [InlineData("noextension")]
    [InlineData("")]
    public void IsImageSource_ReturnsFalse_ForNonImageExtensions(string path)
    {
        Assert.False(_svc.IsImageSource(path));
    }

    // ── BuildImageSequenceSourcePath ──────────────────────────────────────────

    [Theory]
    [InlineData(@"C:\frames\%04d.tif")]   // explicit zero-padded
    [InlineData(@"C:\frames\%d.tif")]     // bare %d
    [InlineData(@"C:\frames\%4d.tif")]    // width without zero
    public void BuildImageSequencePath_ReturnsAsIs_WhenNameAlreadyHasPattern(string path)
    {
        Assert.Equal(path, _svc.BuildImageSequenceSourcePath(path));
    }

    [Fact]
    public void BuildImageSequencePath_ReturnsAsIs_WhenNameIsNotNumeric()
    {
        var path = @"C:\frames\scan001.tif";
        Assert.Equal(path, _svc.BuildImageSequenceSourcePath(path));
    }

    [Fact]
    public void BuildImageSequencePath_ReturnsAsIs_WhenNoDirectory()
    {
        // No directory part — cannot build a sequence pattern
        Assert.Equal("00001.tif", _svc.BuildImageSequenceSourcePath("00001.tif"));
    }

    [Fact]
    public void BuildImageSequencePath_BuildsPattern_WhenNameIsNumeric()
    {
        var input    = @"C:\frames\00001.tif";
        var expected = @"C:\frames\%05d.tif";
        Assert.Equal(expected, _svc.BuildImageSequenceSourcePath(input));
    }

    [Fact]
    public void BuildImageSequencePath_PaddingMatchesNameLength()
    {
        var input    = @"C:\frames\001.tif";
        var expected = @"C:\frames\%03d.tif";
        Assert.Equal(expected, _svc.BuildImageSequenceSourcePath(input));
    }

    [Fact]
    public void BuildImageSequencePath_HandlesQuotedPath()
    {
        var input    = "\"C:\\frames\\00001.tif\"";
        var expected = @"C:\frames\%05d.tif";
        Assert.Equal(expected, _svc.BuildImageSequenceSourcePath(input));
    }

    // ── ImageSequenceExists ───────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ImageSequenceExists_ReturnsFalse_ForBlankPath(string path)
    {
        Assert.False(_svc.ImageSequenceExists(path));
    }

    [Fact]
    public void ImageSequenceExists_ReturnsFalse_WhenDirectoryDoesNotExist()
    {
        Assert.False(_svc.ImageSequenceExists(@"C:\this_dir_cannot_exist_xyz123\00001.tif"));
    }

    [Fact]
    public void ImageSequenceExists_ReturnsTrue_WhenExactFileExists()
    {
        var dir  = Directory.CreateTempSubdirectory("cleanscan_tests_");
        var file = Path.Combine(dir.FullName, "00001.tif");
        File.WriteAllText(file, "");
        try
        {
            Assert.True(_svc.ImageSequenceExists(file));
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public void ImageSequenceExists_ReturnsTrue_WhenDirectoryContainsNumericTiff()
    {
        var dir = Directory.CreateTempSubdirectory("cleanscan_tests_");
        File.WriteAllText(Path.Combine(dir.FullName, "00001.tif"), "");
        try
        {
            // Pass a pattern path — the file itself won't exist, so the directory scan runs
            Assert.True(_svc.ImageSequenceExists(Path.Combine(dir.FullName, "%05d.tif")));
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public void ImageSequenceExists_ReturnsTrue_ForTiffExtension()
    {
        var dir = Directory.CreateTempSubdirectory("cleanscan_tests_");
        File.WriteAllText(Path.Combine(dir.FullName, "0001.tiff"), "");
        try
        {
            Assert.True(_svc.ImageSequenceExists(Path.Combine(dir.FullName, "%04d.tiff")));
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public void ImageSequenceExists_ReturnsFalse_WhenDirectoryHasOnlyNonTiffFiles()
    {
        var dir = Directory.CreateTempSubdirectory("cleanscan_tests_");
        File.WriteAllText(Path.Combine(dir.FullName, "00001.jpg"), "");
        try
        {
            Assert.False(_svc.ImageSequenceExists(Path.Combine(dir.FullName, "00001.tif")));
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public void ImageSequenceExists_ReturnsFalse_WhenTiffNameIsNotNumeric()
    {
        var dir = Directory.CreateTempSubdirectory("cleanscan_tests_");
        File.WriteAllText(Path.Combine(dir.FullName, "scan001.tif"), "");
        try
        {
            Assert.False(_svc.ImageSequenceExists(Path.Combine(dir.FullName, "00001.tif")));
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }
}
