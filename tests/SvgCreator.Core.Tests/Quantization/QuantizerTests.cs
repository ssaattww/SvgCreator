using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using SvgCreator.Core;
using SvgCreator.Core.Models;
using SvgCreator.Core.Orchestration;

namespace SvgCreator.Core.Tests.Quantization;

public sealed class QuantizerTests
{
    // 2つの異なるピクセルが指定クラスタ数2で別々のクラスタに割り当てられることを確認
    [Fact]
    public async Task QuantizeAsync_WithTwoDistinctColors_ReturnsTwoClusters()
    {
        var pixels = new byte[]
        {
            0, 0, 0,
            255, 0, 0
        };
        var image = new ImageData(2, 1, PixelFormat.Rgb, pixels);
        var options = new SvgCreatorRunOptions("input.png", "out")
        {
            QuantizationClusterCount = 2
        };

        var quantizer = new Quantizer(new QuantizerSettings
        {
            MinimumClusterCount = 1,
            MaximumClusterCount = 8,
            DefaultClusterCount = 2,
            MaxIterations = 10,
            RandomSeed = 7
        });

        var result = await quantizer.QuantizeAsync(image, options, CancellationToken.None);

        Assert.Equal(image, result.Image);
        Assert.Equal(image.Width * image.Height, result.LabelIndices.Length);
        Assert.Equal(2, result.Palette.Length);
        Assert.Contains(new RgbColor(0, 0, 0), result.Palette);
        Assert.Contains(new RgbColor(255, 0, 0), result.Palette);
        Assert.Equal(new[] { 0, 1 }, result.LabelIndices.ToArray());
    }

    // 色の重複があっても最終的なパレットがユニーク色のみになることを確認
    [Fact]
    public async Task QuantizeAsync_TrimsClustersToUniqueColors()
    {
        var pixels = new byte[]
        {
            10, 20, 30,
            10, 20, 30,
            200, 210, 220
        };
        var image = new ImageData(3, 1, PixelFormat.Rgb, pixels);
        var options = new SvgCreatorRunOptions("input.png", "out")
        {
            QuantizationClusterCount = 5
        };

        var quantizer = new Quantizer(new QuantizerSettings
        {
            MinimumClusterCount = 1,
            MaximumClusterCount = 8,
            DefaultClusterCount = 3,
            MaxIterations = 10,
            RandomSeed = 11
        });

        var result = await quantizer.QuantizeAsync(image, options, CancellationToken.None);

        Assert.Equal(2, result.Palette.Length);
        Assert.Contains(new RgbColor(10, 20, 30), result.Palette);
        Assert.Contains(new RgbColor(200, 210, 220), result.Palette);
    }
}
