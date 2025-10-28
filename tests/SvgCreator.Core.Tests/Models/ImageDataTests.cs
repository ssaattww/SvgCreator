using System;
using SvgCreator.Core.Models;

namespace SvgCreator.Core.Tests.Models;

public sealed class ImageDataTests
{
    // 幅が 1 未満の場合に例外となることを確認
    [Fact]
    public void Constructor_Throws_WhenWidthIsNotPositive()
    {
        var pixels = new byte[3];
        Assert.Throws<ArgumentOutOfRangeException>(() => new ImageData(0, 1, PixelFormat.Rgb, pixels));
    }

    // 高さが 1 未満の場合に例外となることを確認
    [Fact]
    public void Constructor_Throws_WhenHeightIsNotPositive()
    {
        var pixels = new byte[3];
        Assert.Throws<ArgumentOutOfRangeException>(() => new ImageData(1, 0, PixelFormat.Rgb, pixels));
    }

    // ピクセル配列長が期待値と合わないときに例外となることを確認
    [Fact]
    public void Constructor_Throws_WhenPixelLengthDoesNotMatchExpected()
    {
        var pixels = new byte[2]; // should be 3 bytes for 1x1 RGB
        Assert.Throws<ArgumentException>(() => new ImageData(1, 1, PixelFormat.Rgb, pixels));
    }

    // 入力配列がコピーされていることを確認
    [Fact]
    public void Constructor_CopiesPixelBuffer()
    {
        var pixels = new byte[] { 1, 2, 3 };
        var image = new ImageData(1, 1, PixelFormat.Rgb, pixels);

        pixels[0] = 42;

        Assert.Equal(1, image.Pixels.Span[0]);
    }
}
