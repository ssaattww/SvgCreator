using System.Collections.Generic;
using System.Collections.Immutable;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using SvgCreator.Core.DepthOrdering;
using SvgCreator.Core.Models;
using SvgCreator.Core.Occlusion;

namespace SvgCreator.Core.Tests.Occlusion;

public sealed class OcclusionCompleterTests
{
    // 境界が未閉鎖のシェイプレイヤーでも閉領域に補完されることを確認
    [Fact]
    public async Task CompleteAsync_WithOpenBoundary_AppendsClosingPoint()
    {
        var layer = CreateShapeLayer(
            "layer-0001",
            new[]
            {
                new Vector2(0f, 0f),
                new Vector2(3f, 0f),
                new Vector2(3f, 3f),
                new Vector2(0f, 3f)
            });

        var depthOrder = new DepthOrder(new Dictionary<string, int>
        {
            ["layer-0001"] = 0
        });

        var completer = new OcclusionCompleter();
        var options = new OcclusionCompletionOptions();

        var result = await completer.CompleteAsync(
            new List<ShapeLayer> { layer },
            depthOrder,
            options,
            CancellationToken.None);

        var completed = Assert.Single(result.CompletedLayers);
        Assert.Equal(layer.Id, completed.Id);
        Assert.Equal(layer.Area, completed.Area);

        var boundary = completed.Boundary;
        Assert.Equal(layer.Boundary.Length + 1, boundary.Length);
        Assert.Equal(boundary[0], boundary[^1]);
    }

    // 既に閉じている境界はそのまま維持されることを確認
    [Fact]
    public async Task CompleteAsync_WithClosedBoundary_PreservesShape()
    {
        var points = ImmutableArray.Create(
            new Vector2(0f, 0f),
            new Vector2(2f, 0f),
            new Vector2(2f, 2f),
            new Vector2(0f, 2f),
            new Vector2(0f, 0f));

        var layer = CreateShapeLayer("layer-0001", points);

        var depthOrder = new DepthOrder(new Dictionary<string, int>
        {
            ["layer-0001"] = 0
        });

        var completer = new OcclusionCompleter();
        var options = new OcclusionCompletionOptions();

        var result = await completer.CompleteAsync(
            new List<ShapeLayer> { layer },
            depthOrder,
            options,
            CancellationToken.None);

        var completed = Assert.Single(result.CompletedLayers);
        Assert.Equal(points.Length, completed.Boundary.Length);
        Assert.Equal(points[0], completed.Boundary[0]);
        Assert.Equal(points[^1], completed.Boundary[^1]);
    }

    private static ShapeLayer CreateShapeLayer(string id, IEnumerable<Vector2> boundaryPoints)
    {
        var boundary = ImmutableArray.CreateRange(boundaryPoints);
        var mask = new RasterMask(4, 4, ImmutableArray.CreateRange(new[]
        {
            true, true, true, true,
            true, false, false, true,
            true, false, false, true,
            true, true, true, true
        }));

        return new ShapeLayer(
            id,
            new RgbColor(10, 20, 30),
            mask,
            boundary,
            ImmutableArray<IImmutableList<Vector2>>.Empty,
            area: 12);
    }
}
