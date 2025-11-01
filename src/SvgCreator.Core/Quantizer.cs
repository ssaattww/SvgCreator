using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SvgCreator.Core.Models;
using SvgCreator.Core.Orchestration;

namespace SvgCreator.Core;

/// <summary>
/// K-means ライクな手法を用いて RGB 画像を減色します。
/// </summary>
public sealed class Quantizer : IQuantizer
{
    private readonly ILogger<Quantizer> _logger;
    private readonly QuantizerSettings _settings;

    /// <summary>
    /// <see cref="Quantizer"/> を初期化します。
    /// </summary>
    /// <param name="settings">動作設定。</param>
    /// <param name="logger">診断用ロガー。</param>
    public Quantizer(QuantizerSettings? settings = null, ILogger<Quantizer>? logger = null)
    {
        _settings = settings ?? QuantizerSettings.Default;
        _logger = logger ?? NullLogger<Quantizer>.Instance;
    }

    /// <inheritdoc />
    public Task<QuantizationResult> QuantizeAsync(
        ImageData image,
        SvgCreatorRunOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();

        var pixels = image.Pixels.Span;
        var pixelCount = image.Width * image.Height;

        if (pixels.Length != pixelCount * 3)
        {
            throw new InvalidOperationException("Image pixels must be provided in RGB triplets.");
        }

        var uniqueColors = new HashSet<RgbColor>();
        for (var i = 0; i < pixelCount; i++)
        {
            var offset = i * 3;
            uniqueColors.Add(new RgbColor(pixels[offset], pixels[offset + 1], pixels[offset + 2]));
        }

        var desiredClusters = options.QuantizationClusterCount ?? _settings.DefaultClusterCount;
        var k = Math.Clamp(desiredClusters, _settings.MinimumClusterCount, _settings.MaximumClusterCount);
        k = Math.Max(1, Math.Min(k, uniqueColors.Count));

        if (uniqueColors.Count <= k)
        {
            _logger.LogDebug(
                "Quantization bypassed K-means because unique color count {Count} <= target clusters {Clusters}.",
                uniqueColors.Count,
                k);
            return Task.FromResult(CreateTrivialResult(image, uniqueColors, pixels));
        }

        var vectorPixels = new Vector3[pixelCount];
        for (var i = 0; i < pixelCount; i++)
        {
            var offset = i * 3;
            vectorPixels[i] = new Vector3(pixels[offset], pixels[offset + 1], pixels[offset + 2]);
        }

        var rng = _settings.RandomSeed.HasValue
            ? new Random(_settings.RandomSeed.Value)
            : new Random();

        var centroids = InitializeCentroids(vectorPixels, k, rng, cancellationToken);
        var assignments = new int[pixelCount];
        Array.Fill(assignments, -1);

        var counts = IterateKMeans(vectorPixels, centroids, assignments, rng, cancellationToken);

        var paletteBuilder = ImmutableArray.CreateBuilder<RgbColor>(k);
        var clusterMap = new int[centroids.Length];
        Array.Fill(clusterMap, -1);

        for (var i = 0; i < centroids.Length; i++)
        {
            if (counts[i] == 0)
            {
                continue;
            }

            var centroid = centroids[i];
            var color = new RgbColor(ToByte(centroid.X), ToByte(centroid.Y), ToByte(centroid.Z));
            clusterMap[i] = paletteBuilder.Count;
            paletteBuilder.Add(color);
        }

        if (paletteBuilder.Count == 0)
        {
            // すべて再割当てできなかった場合は安全策としてユニークカラーを返す。
            _logger.LogWarning("All quantization clusters were empty. Falling back to unique colors.");
            return Task.FromResult(CreateTrivialResult(image, uniqueColors, pixels));
        }

        var finalLabels = new int[pixelCount];
        for (var i = 0; i < pixelCount; i++)
        {
            var mapped = clusterMap[assignments[i]];
            if (mapped < 0)
            {
                // 不使用クラスタに割り当たった場合は最も近い既存パレットへ丸める。
                mapped = FindNearestPaletteIndex(vectorPixels[i], paletteBuilder);
            }

            finalLabels[i] = mapped;
        }

        var result = new QuantizationResult(
            image,
            paletteBuilder.ToImmutable(),
            ImmutableArray.CreateRange(finalLabels));

        return Task.FromResult(result);
    }

    private static QuantizationResult CreateTrivialResult(ImageData image, HashSet<RgbColor> uniqueColors, ReadOnlySpan<byte> pixels)
    {
        var paletteBuilder = ImmutableArray.CreateBuilder<RgbColor>(uniqueColors.Count);
        var map = new Dictionary<RgbColor, int>(uniqueColors.Count);
        var labels = new int[image.Width * image.Height];

        for (var i = 0; i < labels.Length; i++)
        {
            var offset = i * 3;
            var color = new RgbColor(pixels[offset], pixels[offset + 1], pixels[offset + 2]);
            if (!map.TryGetValue(color, out var paletteIndex))
            {
                paletteIndex = paletteBuilder.Count;
                map[color] = paletteIndex;
                paletteBuilder.Add(color);
            }

            labels[i] = paletteIndex;
        }

        return new QuantizationResult(
            image,
            paletteBuilder.ToImmutable(),
            ImmutableArray.CreateRange(labels));
    }

    private int[] IterateKMeans(
        IReadOnlyList<Vector3> pixels,
        Vector3[] centroids,
        int[] assignments,
        Random rng,
        CancellationToken cancellationToken)
    {
        var centroidCount = centroids.Length;
        var pixelCount = pixels.Count;

        var sums = new Vector3[centroidCount];
        var counts = new int[centroidCount];

        for (var iteration = 0; iteration < _settings.MaxIterations; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var changed = AssignPixels(pixels, centroids, assignments);

            Array.Clear(sums);
            Array.Clear(counts);

            for (var i = 0; i < pixelCount; i++)
            {
                var assignment = assignments[i];
                sums[assignment] += pixels[i];
                counts[assignment]++;
            }

            var centroidsChanged = false;

            for (var c = 0; c < centroidCount; c++)
            {
                if (counts[c] == 0)
                {
                    centroids[c] = pixels[rng.Next(pixelCount)];
                    centroidsChanged = true;
                    continue;
                }

                var newCentroid = sums[c] / counts[c];
                if (Vector3.DistanceSquared(newCentroid, centroids[c]) > _settings.ConvergenceThreshold)
                {
                    centroidsChanged = true;
                }

                centroids[c] = newCentroid;
            }

            if (!changed && !centroidsChanged)
            {
                break;
            }
        }

        Array.Clear(counts);
        for (var i = 0; i < pixelCount; i++)
        {
            counts[assignments[i]]++;
        }

        for (var c = 0; c < centroidCount; c++)
        {
            if (counts[c] == 0)
            {
                var index = rng.Next(pixelCount);
                centroids[c] = pixels[index];
                assignments[index] = c;
                counts[c] = 1;
            }
        }

        return counts;
    }

    private static byte ToByte(float value)
    {
        var clamped = Math.Clamp((int)MathF.Round(value, MidpointRounding.AwayFromZero), 0, 255);
        return (byte)clamped;
    }

    private static int FindNearestPaletteIndex(Vector3 color, ImmutableArray<RgbColor>.Builder paletteBuilder)
    {
        var bestIndex = 0;
        var bestDistance = float.PositiveInfinity;

        for (var i = 0; i < paletteBuilder.Count; i++)
        {
            var paletteColor = paletteBuilder[i];
            var paletteVector = new Vector3(paletteColor.R, paletteColor.G, paletteColor.B);
            var distance = Vector3.DistanceSquared(color, paletteVector);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private static Vector3[] InitializeCentroids(
        IReadOnlyList<Vector3> pixels,
        int k,
        Random rng,
        CancellationToken cancellationToken)
    {
        var centroids = new Vector3[k];
        var pixelCount = pixels.Count;

        centroids[0] = pixels[rng.Next(pixelCount)];
        var distances = new float[pixelCount];

        for (var centroidIndex = 1; centroidIndex < k; centroidIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var totalDistance = 0.0;
            for (var i = 0; i < pixelCount; i++)
            {
                var distance = DistanceSquaredToClosestCentroid(pixels[i], centroids, centroidIndex);
                distances[i] = distance;
                totalDistance += distance;
            }

            if (totalDistance <= 0)
            {
                centroids[centroidIndex] = pixels[rng.Next(pixelCount)];
                continue;
            }

            var threshold = rng.NextDouble() * totalDistance;
            var cumulative = 0.0;
            for (var i = 0; i < pixelCount; i++)
            {
                cumulative += distances[i];
                if (cumulative >= threshold)
                {
                    centroids[centroidIndex] = pixels[i];
                    break;
                }
            }
        }

        return centroids;
    }

    private static float DistanceSquaredToClosestCentroid(Vector3 pixel, IReadOnlyList<Vector3> centroids, int length)
    {
        var best = float.PositiveInfinity;
        for (var i = 0; i < length; i++)
        {
            var distance = Vector3.DistanceSquared(pixel, centroids[i]);
            if (distance < best)
            {
                best = distance;
            }
        }

        return best;
    }

    private static bool AssignPixels(IReadOnlyList<Vector3> pixels, Vector3[] centroids, int[] assignments)
    {
        var changed = false;
        var pixelCount = pixels.Count;

        for (var i = 0; i < pixelCount; i++)
        {
            var nearest = FindNearestCentroid(pixels[i], centroids);
            if (assignments[i] != nearest)
            {
                assignments[i] = nearest;
                changed = true;
            }
        }

        return changed;
    }

    private static int FindNearestCentroid(Vector3 pixel, Vector3[] centroids)
    {
        var bestIndex = 0;
        var bestDistance = float.PositiveInfinity;

        for (var i = 0; i < centroids.Length; i++)
        {
            var distance = Vector3.DistanceSquared(pixel, centroids[i]);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;
            }
        }

        return bestIndex;
    }
}

/// <summary>
/// 減色処理の動作設定を表します。
/// </summary>
public sealed class QuantizerSettings
{
    /// <summary>
    /// 既定設定。
    /// </summary>
    public static QuantizerSettings Default { get; } = new()
    {
        MinimumClusterCount = 4,
        MaximumClusterCount = 64,
        DefaultClusterCount = 20,
        MaxIterations = 50,
        ConvergenceThreshold = 1e-3f,
        RandomSeed = 17
    };

    /// <summary>
    /// 許容される最小クラスタ数。
    /// </summary>
    public int MinimumClusterCount { get; init; } = 2;

    /// <summary>
    /// 許容される最大クラスタ数。
    /// </summary>
    public int MaximumClusterCount { get; init; } = 64;

    /// <summary>
    /// CLI から指定がない場合に使用する既定クラスタ数。
    /// </summary>
    public int DefaultClusterCount { get; init; } = 20;

    /// <summary>
    /// K-means 更新の最大反復回数。
    /// </summary>
    public int MaxIterations { get; init; } = 50;

    /// <summary>
    /// 収束判定に利用するしきい値（座標二乗距離）。
    /// </summary>
    public float ConvergenceThreshold { get; init; } = 1e-3f;

    /// <summary>
    /// 乱数シード。<c>null</c> の場合は非決定的なシードを利用します。
    /// </summary>
    public int? RandomSeed { get; init; } = 17;
}
