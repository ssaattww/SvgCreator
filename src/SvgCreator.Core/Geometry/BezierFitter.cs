using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using SvgCreator.Core.Models;
using PathModel = SvgCreator.Core.Models.Path;

namespace SvgCreator.Core.Geometry;

/// <summary>
/// ポリライン境界から Bézier パスを生成するコンポーネントのインターフェースです。
/// </summary>
public interface IBezierFitter
{
    /// <summary>
    /// レイヤー境界を Bézier パスへ近似します。
    /// </summary>
    /// <param name="layers">対象レイヤー集合。</param>
    /// <param name="options">近似時の設定。</param>
    /// <param name="cancellationToken">キャンセル要求トークン。</param>
    /// <returns>各レイヤーに対応するパス集合。</returns>
    Task<IReadOnlyList<LayerPathGeometry>> FitAsync(
        IReadOnlyList<ShapeLayer> layers,
        BezierFitterOptions? options,
        CancellationToken cancellationToken);
}

/// <summary>
/// Bézier 近似処理のパラメータを表します。
/// </summary>
public sealed class BezierFitterOptions
{
    /// <summary>
    /// 既定の誤差許容値。
    /// </summary>
    public const float DefaultErrorTolerance = 0.75f;

    /// <summary>
    /// 既定のコーナー平滑化半径（0..1）。
    /// </summary>
    public const float DefaultCornerRadius = 0.8f;

    /// <summary>
    /// パス単純化で許容する最大距離（ピクセル単位）。
    /// </summary>
    public float ErrorTolerance { get; init; } = DefaultErrorTolerance;

    /// <summary>
    /// コーナー平滑化に用いる正規化半径（0..1）。
    /// </summary>
    public float CornerRadius { get; init; } = DefaultCornerRadius;

    /// <summary>
    /// 既定値を返します。
    /// </summary>
    public static BezierFitterOptions Default => new();
}

/// <summary>
/// ポリライン境界を Bézier パスへと変換します。
/// </summary>
public sealed class BezierFitter : IBezierFitter
{
    private const float MinimumTolerance = 1e-3f;
    private const float MinimumSegmentLengthSquared = 1e-6f;

    /// <inheritdoc />
    public Task<IReadOnlyList<LayerPathGeometry>> FitAsync(
        IReadOnlyList<ShapeLayer> layers,
        BezierFitterOptions? options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(layers);

        if (layers.Count == 0)
        {
            throw new ArgumentException("At least one layer is required for Bézier fitting.", nameof(layers));
        }

        var normalized = NormalizeOptions(options ?? BezierFitterOptions.Default);
        var results = new List<LayerPathGeometry>(layers.Count);

        foreach (var layer in layers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (layer is null)
            {
                throw new ArgumentException("Layers cannot contain null entries.", nameof(layers));
            }

            var outerPath = BuildPath(layer.Boundary, normalized, cancellationToken);
            var holePaths = layer.Holes
                .Select(hole => BuildPath(ImmutableArray.CreateRange(hole), normalized, cancellationToken))
                .ToImmutableArray();

            var geometry = new LayerPathGeometry(layer.Id, layer.Color, outerPath, holePaths);
            results.Add(geometry);
        }

        return Task.FromResult((IReadOnlyList<LayerPathGeometry>)results);
    }

    private static BezierFitterOptions NormalizeOptions(BezierFitterOptions options)
    {
        var tolerance = float.IsNaN(options.ErrorTolerance) || options.ErrorTolerance <= 0f
            ? BezierFitterOptions.DefaultErrorTolerance
            : options.ErrorTolerance;

        var radius = float.IsNaN(options.CornerRadius)
            ? BezierFitterOptions.DefaultCornerRadius
            : options.CornerRadius;

        tolerance = MathF.Max(tolerance, MinimumTolerance);
        radius = Clamp(radius, 0f, 1f);

        return new BezierFitterOptions
        {
            ErrorTolerance = tolerance,
            CornerRadius = radius
        };
    }

    private static PathModel BuildPath(
        ImmutableArray<Vector2> boundary,
        BezierFitterOptions options,
        CancellationToken cancellationToken)
    {
        if (boundary.IsDefaultOrEmpty)
        {
            throw new ArgumentException("Boundary must contain coordinates.", nameof(boundary));
        }

        var simplified = SimplifyBoundary(boundary, options.ErrorTolerance);
        var segments = BuildSegments(simplified, options, cancellationToken);
        return new PathModel(segments);
    }

    private static ImmutableArray<Vector2> SimplifyBoundary(ImmutableArray<Vector2> boundary, float tolerance)
    {
        var toleranceValue = MathF.Max(tolerance, MinimumTolerance);
        var toleranceSquared = toleranceValue * toleranceValue;

        var unique = new List<Vector2>(boundary.Length);
        foreach (var point in boundary)
        {
            if (unique.Count == 0 || Vector2.DistanceSquared(unique[^1], point) > toleranceSquared * 0.25f)
            {
                unique.Add(point);
            }
        }

        if (unique.Count < 3)
        {
            throw new InvalidOperationException("Boundary must contain at least three distinct points.");
        }

        if (Vector2.DistanceSquared(unique[0], unique[^1]) <= toleranceSquared)
        {
            unique.RemoveAt(unique.Count - 1);
        }

        if (unique.Count < 3)
        {
            throw new InvalidOperationException("Boundary must contain at least three distinct points.");
        }

        var working = new List<Vector2>(unique);
        var iterationLimit = working.Count * 2;

        while (working.Count > 3 && iterationLimit-- > 0)
        {
            var removed = false;
            for (var i = 0; i < working.Count; i++)
            {
                var prev = working[(i - 1 + working.Count) % working.Count];
                var current = working[i];
                var next = working[(i + 1) % working.Count];

                if (IsRedundant(prev, current, next, toleranceValue))
                {
                    working.RemoveAt(i);
                    removed = true;
                    break;
                }
            }

            if (!removed)
            {
                break;
            }
        }

        if (working.Count < 3)
        {
            throw new InvalidOperationException("Simplified boundary lost polygon integrity.");
        }

        var builder = ImmutableArray.CreateBuilder<Vector2>(working.Count + 1);
        builder.AddRange(working);
        builder.Add(working[0]);
        return builder.ToImmutable();
    }

    private static bool IsRedundant(Vector2 prev, Vector2 current, Vector2 next, float tolerance)
    {
        var baseVector = next - prev;
        var baseLength = baseVector.Length();
        if (baseLength <= MinimumTolerance)
        {
            return true;
        }

        var area = MathF.Abs((current.X - prev.X) * (next.Y - current.Y) - (current.Y - prev.Y) * (next.X - current.X));
        var height = area / baseLength;
        return height <= tolerance;
    }

    private static ImmutableArray<PathSegment> BuildSegments(
        ImmutableArray<Vector2> closedPoints,
        BezierFitterOptions options,
        CancellationToken cancellationToken)
    {
        var segmentBuilder = ImmutableArray.CreateBuilder<PathSegment>();
        var move = new PathSegment(PathSegmentType.Move, new[] { closedPoints[0] });
        segmentBuilder.Add(move);

        var uniqueCount = closedPoints.Length - 1;
        var smoothing = options.CornerRadius;
        var toleranceSquared = options.ErrorTolerance * options.ErrorTolerance;

        for (var i = 0; i < uniqueCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var start = closedPoints[i];
            var end = closedPoints[i + 1];

            if (Vector2.DistanceSquared(start, end) <= toleranceSquared * 0.25f)
            {
                continue;
            }

            var prev = i == 0 ? closedPoints[uniqueCount - 1] : closedPoints[i - 1];
            var nextIndex = i + 2;
            if (nextIndex >= closedPoints.Length)
            {
                nextIndex -= uniqueCount;
            }

            var next = closedPoints[nextIndex];
            var shouldCurve = smoothing > 0f && FormsCorner(prev, start, end, options.ErrorTolerance);

            if (shouldCurve)
            {
                var controlPoints = ComputeCubicControls(prev, start, end, next, smoothing);
                if (controlPoints.HasValue)
                {
                    var (control1, control2) = controlPoints.Value;
                    var cubic = new PathSegment(PathSegmentType.CubicBezier, new[] { control1, control2, end });
                    segmentBuilder.Add(cubic);
                    continue;
                }
            }

            segmentBuilder.Add(new PathSegment(PathSegmentType.Line, new[] { end }));
        }

        segmentBuilder.Add(new PathSegment(PathSegmentType.Close, Array.Empty<Vector2>()));
        return segmentBuilder.ToImmutable();
    }

    private static bool FormsCorner(Vector2 prev, Vector2 start, Vector2 end, float tolerance)
    {
        var v1 = start - prev;
        var v2 = end - start;

        if (v1.LengthSquared() <= MinimumSegmentLengthSquared || v2.LengthSquared() <= MinimumSegmentLengthSquared)
        {
            return false;
        }

        v1 = Vector2.Normalize(v1);
        v2 = Vector2.Normalize(v2);

        var dot = Vector2.Dot(v1, v2);
        dot = Clamp(dot, -1f, 1f);

        var straightThreshold = 1f - MathF.Min(0.5f, tolerance * 0.1f);
        return dot < straightThreshold;
    }

    private static (Vector2 Control1, Vector2 Control2)? ComputeCubicControls(
        Vector2 prev,
        Vector2 start,
        Vector2 end,
        Vector2 next,
        float smoothing)
    {
        var segment = end - start;
        if (segment.LengthSquared() <= MinimumSegmentLengthSquared)
        {
            return null;
        }

        var forward = end - prev;
        var backward = next - start;

        if (forward.LengthSquared() <= MinimumSegmentLengthSquared || backward.LengthSquared() <= MinimumSegmentLengthSquared)
        {
            return null;
        }

        forward = Vector2.Normalize(forward);
        backward = Vector2.Normalize(backward);

        var length = segment.Length();
        var controlDistance = MathF.Min(length * 0.5f * smoothing, length * 0.75f);

        if (controlDistance <= 0f)
        {
            return null;
        }

        var control1 = start + forward * controlDistance;
        var control2 = end - backward * controlDistance;

        return (control1, control2);
    }

    private static float Clamp(float value, float min, float max)
    {
        if (value < min)
        {
            return min;
        }

        if (value > max)
        {
            return max;
        }

        return value;
    }
}
