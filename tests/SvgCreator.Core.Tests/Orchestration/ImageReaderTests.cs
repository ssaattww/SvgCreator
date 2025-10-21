using System;
using System.Threading;
using System.Threading.Tasks;
using OpenCvSharp;
using SvgCreator.Core.Orchestration;
using SvgCreator.Core.Models;
using SvgCreator.Core;
using IOPath = System.IO.Path;
using System.IO;

namespace SvgCreator.Core.Tests.Orchestration;

public sealed class ImageReaderTests
{
    [Fact]
    public async Task ReadAsync_WithValidPng_ReturnsRgbImageData()
    {
        using var temp = new TempDirectory();
        var imagePath = IOPath.Combine(temp.Path, "input.png");

        using (var mat = new Mat(new Size(2, 1), MatType.CV_8UC3))
        {
            mat.Set(0, 0, new Vec3b(10, 20, 30));
            mat.Set(0, 1, new Vec3b(70, 80, 90));
            Cv2.ImWrite(imagePath, mat);
        }

        var options = new SvgCreatorRunOptions(imagePath, temp.Path);
        var reader = new ImageReader();

        var image = await reader.ReadAsync(options, CancellationToken.None);

        Assert.Equal(2, image.Width);
        Assert.Equal(1, image.Height);
        Assert.Equal(PixelFormat.Rgb, image.Format);

        var pixels = image.Pixels.ToArray();
        Assert.Equal(new byte[] { 30, 20, 10, 90, 80, 70 }, pixels);
    }

    [Fact]
    public async Task ReadAsync_WhenFileDoesNotExist_ThrowsFileNotFound()
    {
        var missingPath = IOPath.Combine(IOPath.GetTempPath(), Guid.NewGuid().ToString("N") + ".png");
        var options = new SvgCreatorRunOptions(missingPath, IOPath.GetTempPath());
        var reader = new ImageReader();

        await Assert.ThrowsAsync<FileNotFoundException>(() => reader.ReadAsync(options, CancellationToken.None));
    }

    [Fact]
    public async Task ReadAsync_WithUnsupportedExtension_ThrowsNotSupported()
    {
        using var temp = new TempDirectory();
        var textPath = IOPath.Combine(temp.Path, "input.txt");
        File.WriteAllText(textPath, "not an image");

        var options = new SvgCreatorRunOptions(textPath, temp.Path);
        var reader = new ImageReader();

        await Assert.ThrowsAsync<NotSupportedException>(() => reader.ReadAsync(options, CancellationToken.None));
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = IOPath.Combine(IOPath.GetTempPath(), "svgcreator-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, recursive: true);
                }
            }
            catch
            {
                // テスト終了時点で削除できなくても致命的ではないため握りつぶす。
            }
        }
    }
}
