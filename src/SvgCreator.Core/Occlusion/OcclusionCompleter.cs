using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using MathNet.Numerics.LinearAlgebra;
using SvgCreator.Core.DepthOrdering;
using SvgCreator.Core.Models;

using MathNetVector = MathNet.Numerics.LinearAlgebra.Vector<double>;

namespace SvgCreator.Core.Occlusion;

/// <summary>
/// エラスティカ補完を実行するコンポーネントのインターフェースです。
/// </summary>
public interface IOcclusionCompleter
{
    /// <summary>
    /// シェイプレイヤーの境界を補完し、遮蔽で欠落した領域を復元します。
    /// </summary>
    /// <param name="layers">補完対象のレイヤー。</param>
    /// <param name="depthOrder">レイヤーの深度順。</param>
    /// <param name="options">補完アルゴリズムのパラメータ。</param>
    /// <param name="cancellationToken">キャンセル要求トークン。</param>
    /// <returns>補完後のレイヤー集合。</returns>
    Task<OcclusionCompletionResult> CompleteAsync(
        IReadOnlyList<ShapeLayer> layers,
        DepthOrder depthOrder,
        OcclusionCompletionOptions options,
        CancellationToken cancellationToken);
}

/// <summary>
/// エラスティカ近似で利用するパラメータを表します。
/// </summary>
public sealed class OcclusionCompletionOptions
{
    /// <summary>
    /// 既定の反復上限。
    /// </summary>
    public const int DefaultEulerIterationLimit = 100;

    /// <summary>
    /// 既定の近傍幅。
    /// </summary>
    public const float DefaultEpsilon = 5f;

    /// <summary>
    /// 既定の曲率正則化係数。
    /// </summary>
    public const float DefaultCurvatureWeight = 0.1f;

    /// <summary>
    /// 既定の弧長ペナルティ係数。
    /// </summary>
    public const float DefaultLengthWeight = 1.0f;

    /// <summary>
    /// 既定の近傍半径。
    /// </summary>
    public const float DefaultNeighborhoodRadius = 0.1f;

    /// <summary>
    /// 既定の閉鎖誤差許容値。
    /// </summary>
    public const float DefaultClosingTolerance = 1e-3f;

    /// <summary>
    /// Euler エラスティカの最大反復回数。
    /// </summary>
    public int EulerIterationLimit { get; init; } = DefaultEulerIterationLimit;

    /// <summary>
    /// 共有境界近傍の幅。
    /// </summary>
    public float Epsilon { get; init; } = DefaultEpsilon;

    /// <summary>
    /// 曲率正則化項の重み。
    /// </summary>
    public float CurvatureWeight { get; init; } = DefaultCurvatureWeight;

    /// <summary>
    /// 弧長項の重み。
    /// </summary>
    public float LengthWeight { get; init; } = DefaultLengthWeight;

    /// <summary>
    /// 近傍半径。
    /// </summary>
    public float NeighborhoodRadius { get; init; } = DefaultNeighborhoodRadius;

    /// <summary>
    /// 境界閉鎖時に同一点とみなす距離の許容値。
    /// </summary>
    public float ClosingTolerance { get; init; } = DefaultClosingTolerance;
}

/// <summary>
/// エラスティカ補完の結果を表します。
/// </summary>
public sealed class OcclusionCompletionResult
{
    /// <summary>
    /// 新しい <see cref="OcclusionCompletionResult"/> を初期化します。
    /// </summary>
    /// <param name="completedLayers">補完後のレイヤー集合。</param>
    /// <exception cref="ArgumentNullException"><paramref name="completedLayers"/> が <c>null</c> です。</exception>
    public OcclusionCompletionResult(IReadOnlyList<ShapeLayer> completedLayers)
    {
        CompletedLayers = completedLayers ?? throw new ArgumentNullException(nameof(completedLayers));
    }

    /// <summary>
    /// 補完後のレイヤーを取得します。
    /// </summary>
    public IReadOnlyList<ShapeLayer> CompletedLayers { get; }
}

/// <summary>
/// シェイプレイヤー境界の簡易エラスティカ補完を提供します。
/// 設計記述に沿ったエネルギー最小化の枠組みを提供しつつ、現段階では数値安定化を目的に平滑化の近似を実装します。
/// </summary>
public sealed class OcclusionCompleter : IOcclusionCompleter
{
    private const float MinimumWeight = 1e-6f;

    /// <inheritdoc />
    public Task<OcclusionCompletionResult> CompleteAsync(
        IReadOnlyList<ShapeLayer> layers,
        DepthOrder depthOrder,
        OcclusionCompletionOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(layers);
        ArgumentNullException.ThrowIfNull(depthOrder);
        ArgumentNullException.ThrowIfNull(options);

        if (layers.Count == 0)
        {
            throw new ArgumentException("At least one shape layer is required.", nameof(layers));
        }

        var completed = new List<ShapeLayer>(layers.Count);
        var builder = ImmutableArray.CreateBuilder<Vector2>();

        foreach (var layer in layers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (layer is null)
            {
                throw new ArgumentException("Layers cannot contain null entries.", nameof(layers));
            }

            // 深度順序に含まれていることを確認
            _ = depthOrder.GetDepth(layer.Id);

            var boundary = layer.Boundary;
            if (boundary.Length < 3)
            {
                throw new InvalidOperationException($"Layer '{layer.Id}' boundary is too small to complete.");
            }

            var closed = EnsureClosedBoundary(boundary, options.ClosingTolerance);
            var smoothed = SmoothBoundary(closed, Math.Clamp(options.CurvatureWeight, MinimumWeight, 1f), options.EulerIterationLimit);

            var completedLayer = new ShapeLayer(
                layer.Id,
                layer.Color,
                layer.Mask,
                smoothed,
                layer.Holes,
                layer.Area);

            completed.Add(completedLayer);
        }

        return Task.FromResult(new OcclusionCompletionResult(completed));
    }

    private static ImmutableArray<Vector2> EnsureClosedBoundary(ImmutableArray<Vector2> boundary, float tolerance)
    {
        var builder = ImmutableArray.CreateBuilder<Vector2>(boundary.Length + 1);
        builder.AddRange(boundary);

        var first = builder[0];
        var last = builder[^1];

        if (!IsApproximatelyEqual(first, last, tolerance))
        {
            builder.Add(first);
        }

        return builder.ToImmutable();
    }

    private static ImmutableArray<Vector2> SmoothBoundary(ImmutableArray<Vector2> boundary, float curvatureWeight, int iterationLimit)
    {
        if (iterationLimit <= 0 || curvatureWeight <= MinimumWeight)
        {
            return boundary;
        }

        var denseX = MathNet.Numerics.LinearAlgebra.Vector<double>.Build.Dense(boundary.Length, i => boundary[i].X);
        var denseY = MathNet.Numerics.LinearAlgebra.Vector<double>.Build.Dense(boundary.Length, i => boundary[i].Y);

        var smoothingWeight = Math.Min(curvatureWeight, 0.25f);
        var iterations = Math.Clamp(iterationLimit, 1, 8); // 数値安定性のため軽めの反復に制限

        denseX = RunLbfgsIterations(denseX, smoothingWeight, iterations);
        denseY = RunLbfgsIterations(denseY, smoothingWeight, iterations);

        var builder = ImmutableArray.CreateBuilder<Vector2>(boundary.Length);
        for (var i = 0; i < boundary.Length; i++)
        {
            builder.Add(new Vector2((float)denseX[i], (float)denseY[i]));
        }

        // 始終点は元の値を尊重して戻す（幾何拘束）
        builder[0] = boundary[0];
        builder[^1] = boundary[^1];

        return builder.MoveToImmutable();
    }

    private static MathNetVector RunLbfgsIterations(MathNetVector initial, double stepScale, int iterationLimit)
    {
        const int HistorySize = 5;

        var positions = initial.Clone();
        var sHistory = new List<MathNetVector>(HistorySize);
        var yHistory = new List<MathNetVector>(HistorySize);
        var gradient = ComputeGradient(positions);

        for (var iteration = 0; iteration < iterationLimit; iteration++)
        {
            if (gradient.L2Norm() < 1e-5)
            {
                break;
            }

            var direction = TwoLoopRecursion(gradient, sHistory, yHistory);
            if (direction.L2Norm() < 1e-6)
            {
                break;
            }

            var previousPositions = positions;
            var step = direction * stepScale;
            positions = positions - step;
            positions[0] = initial[0];
            positions[^1] = initial[^1];

            var newGradient = ComputeGradient(positions);
            var s = positions - previousPositions;
            var y = newGradient - gradient;

            var curvature = y.DotProduct(s);
            if (curvature > 1e-9)
            {
                if (sHistory.Count == HistorySize)
                {
                    sHistory.RemoveAt(0);
                    yHistory.RemoveAt(0);
                }

                sHistory.Add(s);
                yHistory.Add(y);
            }

            gradient = newGradient;
        }

        return positions;
    }

    private static MathNetVector ComputeGradient(MathNetVector positions)
    {
        var gradient = positions.Clone();
        gradient[0] = 0d;
        gradient[^1] = 0d;

        for (var i = 1; i < positions.Count - 1; i++)
        {
            gradient[i] = 2d * positions[i] - positions[i - 1] - positions[i + 1];
        }

        return gradient;
    }

    private static MathNetVector TwoLoopRecursion(MathNetVector gradient, IReadOnlyList<MathNetVector> sHistory, IReadOnlyList<MathNetVector> yHistory)
    {
        var q = gradient.Clone();
        var historyLength = sHistory.Count;

        if (historyLength == 0)
        {
            return q;
        }

        var alpha = new double[historyLength];

        for (var index = historyLength - 1; index >= 0; index--)
        {
            var s = sHistory[index];
            var y = yHistory[index];
            var rho = 1d / Math.Max(y.DotProduct(s), 1e-9);
            alpha[index] = rho * s.DotProduct(q);
            q = q - alpha[index] * y;
        }

        var lastS = sHistory[^1];
        var lastY = yHistory[^1];
        var gamma = lastS.DotProduct(lastY) / Math.Max(lastY.DotProduct(lastY), 1e-9);
        var r = q * gamma;

        for (var index = 0; index < historyLength; index++)
        {
            var s = sHistory[index];
            var y = yHistory[index];
            var rho = 1d / Math.Max(y.DotProduct(s), 1e-9);
            var beta = rho * y.DotProduct(r);
            r = r + s * (alpha[index] - beta);
        }

        return r;
    }

    private static bool IsApproximatelyEqual(Vector2 left, Vector2 right, float tolerance)
    {
        var delta = Vector2.DistanceSquared(left, right);
        return delta <= tolerance * tolerance;
    }
}
