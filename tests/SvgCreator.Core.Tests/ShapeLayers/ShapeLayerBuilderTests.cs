using System.Collections.Immutable;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using SvgCreator.Core.Models;
using SvgCreator.Core.ShapeLayers;

namespace SvgCreator.Core.Tests.ShapeLayers;

public sealed class ShapeLayerBuilderTests
{
    [Fact]
    // 2色に量子化された 2x2 画像からレイヤーを抽出できることを確認
    public async Task BuildLayersAsync_WithTwoColorColumns_ReturnsTwoShapeLayers()
    {
        var pixels = new byte[]
        {
            10, 20, 30,
            10, 20, 30,
            200, 210, 220,
            200, 210, 220
        };

        var image = new ImageData(width: 2, height: 2, PixelFormat.Rgb, pixels);
        var palette = ImmutableArray.Create(new RgbColor(10, 20, 30), new RgbColor(200, 210, 220));
        var labels = ImmutableArray.Create(0, 1, 0, 1);
        var quantization = new QuantizationResult(image, palette, labels);

        var builder = new ShapeLayerBuilder();

        var layers = (await builder.BuildLayersAsync(quantization, CancellationToken.None))
            .OrderBy(layer => layer.Id)
            .ToArray();

        Assert.Equal(2, layers.Length);

        var left = layers[0];
        Assert.Equal("layer-0001", left.Id);
        Assert.Equal(2, left.Area);
        Assert.Equal(new RgbColor(10, 20, 30), left.Color);
        Assert.True(left.Mask[0, 0]);
        Assert.True(left.Mask[0, 1]);
        Assert.False(left.Mask[1, 0]);
        Assert.Equal(4, left.Boundary.Length);
        Assert.Contains(new Vector2(0, 0), left.Boundary);
        Assert.Contains(new Vector2(1, 2), left.Boundary);

        var right = layers[1];
        Assert.Equal("layer-0002", right.Id);
        Assert.Equal(2, right.Area);
        Assert.Equal(new RgbColor(200, 210, 220), right.Color);
        Assert.True(right.Mask[1, 0]);
        Assert.True(right.Mask[1, 1]);
        Assert.False(right.Mask[0, 0]);
        Assert.Equal(4, right.Boundary.Length);
        Assert.Contains(new Vector2(2, 0), right.Boundary);
        Assert.Contains(new Vector2(1, 2), right.Boundary);
    }
}
