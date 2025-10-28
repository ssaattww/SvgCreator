using System.Collections.Immutable;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using SvgCreator.Core.Geometry;
using SvgCreator.Core.Models;

namespace SvgCreator.Core.Tests.Geometry;

public sealed class BezierFitterTests
{
    [Fact]
    // 矩形境界を与えた場合に閉じたパスが生成されることを確認
    public async Task FitAsync_WithRectangle_ReturnsClosedPath()
    {
        var boundary = ImmutableArray.Create(
            new Vector2(0f, 0f),
            new Vector2(10f, 0f),
            new Vector2(10f, 6f),
            new Vector2(0f, 6f),
            new Vector2(0f, 0f));

        var layer = CreateLayer("layer-rect", boundary);
        var options = new BezierFitterOptions
        {
            ErrorTolerance = 0.05f,
            CornerRadius = 0f
        };

        var fitter = new BezierFitter();
        var result = await fitter.FitAsync(new[] { layer }, options, CancellationToken.None);

        var geometry = Assert.Single(result);
        Assert.Equal(layer.Id, geometry.LayerId);

        var segments = geometry.OuterPath.Segments;
        Assert.Equal(PathSegmentType.Move, segments[0].Type);
        Assert.Equal(PathSegmentType.Close, segments[^1].Type);

        var lineSegments = segments.Where(static s => s.Type == PathSegmentType.Line).ToArray();
        Assert.Equal(4, lineSegments.Length);

        var startPoint = segments[0].Points[0];
        var lastSegmentEnd = lineSegments[^1].Points[0];
        Assert.True(Vector2.Distance(startPoint, lastSegmentEnd) <= options.ErrorTolerance + 1e-3f);
    }

    [Fact]
    // 直線上の冗長点が許容誤差内で間引かれることを確認
    public async Task FitAsync_WithRedundantColinearPoints_SimplifiesSegments()
    {
        var boundary = ImmutableArray.Create(
            new Vector2(0f, 0f),
            new Vector2(4f, 0f),
            new Vector2(8f, 0.01f),
            new Vector2(12f, 0f),
            new Vector2(12f, 5f),
            new Vector2(0f, 5f),
            new Vector2(0f, 0f));

        var layer = CreateLayer("layer-colinear", boundary);
        var options = new BezierFitterOptions
        {
            ErrorTolerance = 0.5f,
            CornerRadius = 0f
        };

        var fitter = new BezierFitter();
        var result = await fitter.FitAsync(new[] { layer }, options, CancellationToken.None);

        var geometry = Assert.Single(result);
        var lineSegments = geometry.OuterPath.Segments.Where(static s => s.Type == PathSegmentType.Line).ToArray();

        Assert.Equal(4, lineSegments.Length);
    }

    [Fact]
    // 曲率が滑らかな境界では3次ベジェが生成されることを確認
    public async Task FitAsync_WithSmoothCurve_ProducesCubicBezier()
    {
        var boundaryBuilder = ImmutableArray.CreateBuilder<Vector2>();
        const int SampleCount = 9;
        for (var i = 0; i < SampleCount; i++)
        {
            var angle = (float)(i * (System.MathF.PI / 2f) / (SampleCount - 1));
            var x = System.MathF.Cos(angle);
            var y = System.MathF.Sin(angle);
            boundaryBuilder.Add(new Vector2(x, y));
        }

        // 閉路にするために始点を再度追加
        boundaryBuilder.Add(boundaryBuilder[0]);

        var boundary = boundaryBuilder.ToImmutable();
        var layer = CreateLayer("layer-arc", boundary);
        var options = new BezierFitterOptions
        {
            ErrorTolerance = 0.01f,
            CornerRadius = 0.9f
        };

        var fitter = new BezierFitter();
        var result = await fitter.FitAsync(new[] { layer }, options, CancellationToken.None);

        var geometry = Assert.Single(result);
        var cubicSegments = geometry.OuterPath.Segments.Where(static s => s.Type == PathSegmentType.CubicBezier).ToArray();
        Assert.NotEmpty(cubicSegments);
        var cubic = cubicSegments[0];

        // 制御点が有限値であり、端点とは異なる値を取ることを確認
        var previousEnd = geometry.OuterPath.Segments[0].Points[0];
        var endPoint = cubic.Points[^1];
        foreach (var control in cubic.Points.Take(2))
        {
            Assert.False(float.IsNaN(control.X) || float.IsNaN(control.Y));
            Assert.False(float.IsInfinity(control.X) || float.IsInfinity(control.Y));
            Assert.NotEqual(previousEnd, control);
            Assert.NotEqual(endPoint, control);
        }

        Assert.Contains(boundary, point => Vector2.Distance(point, cubic.Points[^1]) <= options.ErrorTolerance + 0.1f);
    }

    private static ShapeLayer CreateLayer(string id, ImmutableArray<Vector2> boundary)
    {
        var maskWidth = 8;
        var maskHeight = 8;
        var bitsBuilder = ImmutableArray.CreateBuilder<bool>(maskWidth * maskHeight);
        bitsBuilder.Count = maskWidth * maskHeight;

        // 単純化の目的で領域中央の数ピクセルを塗りつぶす
        var filledIndices = new[] { 27, 28, 35, 36 };
        foreach (var index in filledIndices)
        {
            bitsBuilder[index] = true;
        }

        var mask = new RasterMask(maskWidth, maskHeight, bitsBuilder.MoveToImmutable());
        var area = filledIndices.Length;

        return new ShapeLayer(
            id,
            new RgbColor(120, 130, 140),
            mask,
            boundary,
            ImmutableArray<IImmutableList<Vector2>>.Empty,
            area);
    }
}
