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
        var palette = ImmutableArray.Create(new RgbColor(10, 20, 30), new RgbColor(200, 210, 220));
        var labels = ImmutableArray.Create(0, 1, 0, 1);
        var quantization = CreateQuantization(width: 2, height: 2, palette, labels);

        var builder = new ShapeLayerBuilder();

        var result = await builder.BuildLayersAsync(quantization, CancellationToken.None);
        var layers = result.ShapeLayers.OrderBy(layer => layer.Id).ToArray();

        Assert.Empty(result.NoisyLayers);

        Assert.Collection(
            layers,
            left =>
            {
                Assert.Equal("layer-0001", left.Id);
                Assert.Equal(2, left.Area);
                Assert.Equal(new RgbColor(10, 20, 30), left.Color);
                Assert.True(left.Mask[0, 0]);
                Assert.True(left.Mask[0, 1]);
                Assert.False(left.Mask[1, 0]);
                Assert.Equal(4, left.Boundary.Length);
                Assert.Contains(new Vector2(0, 0), left.Boundary);
                Assert.Contains(new Vector2(1, 2), left.Boundary);
            },
            right =>
            {
                Assert.Equal("layer-0002", right.Id);
                Assert.Equal(2, right.Area);
                Assert.Equal(new RgbColor(200, 210, 220), right.Color);
                Assert.True(right.Mask[1, 0]);
                Assert.True(right.Mask[1, 1]);
                Assert.False(right.Mask[0, 0]);
                Assert.Equal(4, right.Boundary.Length);
                Assert.Contains(new Vector2(2, 0), right.Boundary);
                Assert.Contains(new Vector2(1, 2), right.Boundary);
            });
    }

    [Fact]
    // 穴領域を検出し ShapeLayer.Holes に格納できることを確認
    public async Task BuildLayersAsync_WithNestedComponent_ExtractsHoleBoundary()
    {
        var palette = ImmutableArray.Create(new RgbColor(50, 60, 70), new RgbColor(200, 210, 220));
        var labels = ImmutableArray.Create(
            0, 0, 0,
            0, 1, 0,
            0, 0, 0);

        var quantization = CreateQuantization(width: 3, height: 3, palette, labels);
        var options = new ShapeLayerBuilderOptions
        {
            NoisyComponentMinimumPixelCount = 2
        };

        var builder = new ShapeLayerBuilder(options);

        var result = await builder.BuildLayersAsync(quantization, CancellationToken.None);

        Assert.Single(result.ShapeLayers);
        var layer = result.ShapeLayers[0];
        Assert.Equal("layer-0001", layer.Id);
        Assert.Equal(8, layer.Area);
        Assert.Single(layer.Holes);

        var hole = layer.Holes[0].ToArray();
        Assert.True(hole.Length == 4);
        Assert.Contains(new Vector2(1, 1), hole);
        Assert.Contains(new Vector2(2, 1), hole);
        Assert.Contains(new Vector2(2, 2), hole);
        Assert.Contains(new Vector2(1, 2), hole);

        Assert.Single(result.NoisyLayers);
        var noise = result.NoisyLayers[0];
        Assert.Equal("noise-0001", noise.Id);
        Assert.Equal(1, noise.Area);
        Assert.Equal(new RgbColor(200, 210, 220), noise.Color);
    }

    [Fact]
    // 面積しきい値に基づき微小成分をノイズとして除外することを確認
    public async Task BuildLayersAsync_WithAreaThreshold_ClassifiesNoisyComponents()
    {
        var palette = ImmutableArray.Create(new RgbColor(10, 20, 30), new RgbColor(200, 210, 220));
        var labels = ImmutableArray.Create(0, 0, 0, 1, 1);

        var quantization = CreateQuantization(width: 5, height: 1, palette, labels);
        var options = new ShapeLayerBuilderOptions
        {
            NoisyComponentMinimumPixelCount = 3
        };

        var builder = new ShapeLayerBuilder(options);

        var result = await builder.BuildLayersAsync(quantization, CancellationToken.None);

        Assert.Single(result.ShapeLayers);
        var shape = result.ShapeLayers[0];
        Assert.Equal("layer-0001", shape.Id);
        Assert.Equal(3, shape.Area);

        Assert.Single(result.NoisyLayers);
        var noise = result.NoisyLayers[0];
        Assert.Equal("noise-0001", noise.Id);
        Assert.Equal(2, noise.Area);
    }

    [Fact]
    // しきい値と同値の面積・周長を持つ成分は除外されないことを確認
    public async Task BuildLayersAsync_WithThresholdBoundary_DoesNotDropComponent()
    {
        var palette = ImmutableArray.Create(new RgbColor(10, 20, 30), new RgbColor(200, 210, 220));
        var labels = ImmutableArray.Create(0, 0, 1);

        var quantization = CreateQuantization(width: 3, height: 1, palette, labels);
        var options = new ShapeLayerBuilderOptions
        {
            NoisyComponentMinimumPixelCount = 2,
            NoisyComponentMinimumPerimeter = 6f
        };

        var builder = new ShapeLayerBuilder(options);

        var result = await builder.BuildLayersAsync(quantization, CancellationToken.None);

        Assert.Single(result.ShapeLayers);
        var shape = result.ShapeLayers[0];
        Assert.Equal(2, shape.Area);
        Assert.Equal("layer-0001", shape.Id);

        Assert.Single(result.NoisyLayers);
        var noise = result.NoisyLayers[0];
        Assert.Equal(1, noise.Area);
        Assert.Equal("noise-0001", noise.Id);
    }

    private static QuantizationResult CreateQuantization(int width, int height, ImmutableArray<RgbColor> palette, ImmutableArray<int> labels)
    {
        var pixels = new byte[width * height * 3];
        for (var i = 0; i < pixels.Length; i++)
        {
            pixels[i] = (byte)(i % 253 + 1);
        }

        var image = new ImageData(width, height, PixelFormat.Rgb, pixels);
        return new QuantizationResult(image, palette, labels);
    }
}
