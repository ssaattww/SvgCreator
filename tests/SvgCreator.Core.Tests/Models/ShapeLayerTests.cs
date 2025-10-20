using System;
using System.Collections.Immutable;
using System.Numerics;
using SvgCreator.Core.Models;

namespace SvgCreator.Core.Tests.Models;

public sealed class ShapeLayerTests
{
    private static readonly ImmutableArray<Vector2> SampleBoundary =
        ImmutableArray.Create(new Vector2(0, 0), new Vector2(10, 0), new Vector2(0, 10));

    [Fact]
    // レイヤー ID が null または空白の場合に例外となることを確認
    public void Constructor_Throws_WhenIdIsNullOrWhitespace()
    {
        var mask = new RasterMask(1, 1, ImmutableArray.Create(true));
        var holes = ImmutableArray<IImmutableList<Vector2>>.Empty;

        Assert.Throws<ArgumentException>(() => new ShapeLayer(null!, default, mask, SampleBoundary, holes, 1));
        Assert.Throws<ArgumentException>(() => new ShapeLayer("   ", default, mask, SampleBoundary, holes, 1));
    }

    [Fact]
    // 境界点数が 3 未満のときに例外となることを確認
    public void Constructor_Throws_WhenBoundaryHasTooFewPoints()
    {
        var mask = new RasterMask(1, 1, ImmutableArray.Create(true));
        var boundary = ImmutableArray.Create(new Vector2(0, 0), new Vector2(1, 1));
        var holes = ImmutableArray<IImmutableList<Vector2>>.Empty;

        Assert.Throws<ArgumentException>(() => new ShapeLayer("layer-1", default, mask, boundary, holes, 1));
    }

    [Fact]
    // 面積が 1 未満のときに例外となることを確認
    public void Constructor_Throws_WhenAreaIsNotPositive()
    {
        var mask = new RasterMask(1, 1, ImmutableArray.Create(true));
        var holes = ImmutableArray<IImmutableList<Vector2>>.Empty;

        Assert.Throws<ArgumentOutOfRangeException>(() => new ShapeLayer("layer-1", default, mask, SampleBoundary, holes, 0));
    }

    [Fact]
    // すべてのプロパティに値が設定されることを確認
    public void Constructor_AssignsProperties()
    {
        var mask = new RasterMask(2, 2, ImmutableArray.Create(true, false, true, false));
        var holes = ImmutableArray<IImmutableList<Vector2>>.Empty;
        var layer = new ShapeLayer("layer-1", new RgbColor(10, 20, 30), mask, SampleBoundary, holes, 2);

        Assert.Equal("layer-1", layer.Id);
        Assert.Equal(new RgbColor(10, 20, 30), layer.Color);
        Assert.Equal(mask, layer.Mask);
        Assert.Equal(SampleBoundary, layer.Boundary);
        Assert.Equal(2, layer.Area);
    }
}
