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
using SvgCreator.Core.Quantization;

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
            return Task.FromResult(CreateTrivialResult(image, uniqueColors));
        }

        var blockSizes = EnsureBlockSizes(_settings.MultiScaleBlockSizes);
        var samplesPerLevel = MultiScalePixelSampler.CreateSamples(image, blockSizes);

        var usableLevels = new List<QuantizationSample[]>(samplesPerLevel.Length);
        foreach (var level in samplesPerLevel)
        {
            if (level.Length >= k)
            {
                usableLevels.Add(level);
            }
        }

        var finalLevel = samplesPerLevel[^1];
        if (!usableLevels.Contains(finalLevel))
        {
            usableLevels.Add(finalLevel);
        }
        else if (!ReferenceEquals(usableLevels[^1], finalLevel))
        {
            usableLevels.Remove(finalLevel);
            usableLevels.Add(finalLevel);
        }

        if (usableLevels.Count == 0)
        {
            usableLevels.Add(finalLevel);
        }

        var rng = _settings.RandomSeed.HasValue
            ? new Random(_settings.RandomSeed.Value)
            : new Random();

        Vector3[]? centroids = null;
        int[]? assignments = null;
        double[]? counts = null;
        QuantizationSample[]? finalSamples = null;

        foreach (var level in usableLevels)
        {
            var result = RunWeightedKMeans(level, k, centroids, rng, cancellationToken);
            centroids = result.Centroids;
            assignments = result.Assignments;
            counts = result.Counts;
            finalSamples = level;
        }

        if (centroids is null || assignments is null || counts is null || finalSamples is null)
        {
            _logger.LogWarning("Quantization failed to produce centroids. Falling back to unique colors.");
            return Task.FromResult(CreateTrivialResult(image, uniqueColors));
        }

        var paletteBuilder = ImmutableArray.CreateBuilder<RgbColor>(k);
        var clusterMap = new int[centroids.Length];
        Array.Fill(clusterMap, -1);

        for (var i = 0; i < centroids.Length; i++)
        {
            if (counts[i] <= 0 || double.IsNaN(counts[i]))
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
            _logger.LogWarning("All quantization clusters were empty. Falling back to unique colors.");
            return Task.FromResult(CreateTrivialResult(image, uniqueColors));
        }

        var finalLabels = new int[finalSamples.Length];
        for (var i = 0; i < finalSamples.Length; i++)
        {
            var mapped = clusterMap[assignments[i]];
            if (mapped < 0)
            {
                mapped = FindNearestPaletteIndex(finalSamples[i].Color, paletteBuilder);
            }

            finalLabels[i] = mapped;
        }

        var result = new QuantizationResult(
            image,
            paletteBuilder.ToImmutable(),
            ImmutableArray.CreateRange(finalLabels));

        return Task.FromResult(result);
    }

    private static QuantizationResult CreateTrivialResult(ImageData image, HashSet<RgbColor> uniqueColors)
    {
        var pixels = image.Pixels.Span;

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

    private ImmutableArray<int> EnsureBlockSizes(ImmutableArray<int> configured)
    {
        var seen = new HashSet<int>();
        var values = new List<int>();

        void TryAdd(int size)
        {
            if (size <= 0)
            {
                return;
            }

            if (seen.Add(size))
            {
                values.Add(size);
            }
        }

        if (!configured.IsDefaultOrEmpty)
        {
            foreach (var size in configured)
            {
                TryAdd(size);
            }
        }

        if (values.Count == 0)
        {
            TryAdd(4);
            TryAdd(2);
            TryAdd(1);
        }
        else if (!seen.Contains(1))
        {
            TryAdd(1);
        }

        values.Sort((a, b) => b.CompareTo(a));

        return ImmutableArray.CreateRange(values);
    }

    private (Vector3[] Centroids, int[] Assignments, double[] Counts) RunWeightedKMeans(
        QuantizationSample[] samples,
        int clusterCount,
        Vector3[]? previousCentroids,
        Random rng,
        CancellationToken cancellationToken)
    {
        if (samples.Length == 0)
        {
            throw new InvalidOperationException("Cannot perform K-means without samples.");
        }

        var centroids = PrepareInitialCentroids(samples, clusterCount, previousCentroids, rng, cancellationToken);
        var assignments = new int[samples.Length];
        Array.Fill(assignments, -1);

        var sums = new Vector3[clusterCount];
        var counts = new double[clusterCount];

        for (var iteration = 0; iteration < _settings.MaxIterations; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var changed = AssignSamples(samples, centroids, assignments);

            Array.Clear(sums);
            Array.Clear(counts);

            for (var i = 0; i < samples.Length; i++)
            {
                var weight = Math.Max(1, samples[i].Weight);
                var assignment = assignments[i];
                sums[assignment] += samples[i].Color * weight;
                counts[assignment] += weight;
            }

            var centroidsChanged = false;

            for (var c = 0; c < clusterCount; c++)
            {
                if (counts[c] <= 0)
                {
                    var randomIndex = rng.Next(samples.Length);
                    centroids[c] = samples[randomIndex].Color;
                    counts[c] = 1;
                    centroidsChanged = true;
                    continue;
                }

                var newCentroid = sums[c] / (float)counts[c];
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

        for (var c = 0; c < clusterCount; c++)
        {
            if (counts[c] <= 0)
            {
                var randomIndex = rng.Next(samples.Length);
                centroids[c] = samples[randomIndex].Color;
                counts[c] = 1;
            }
        }

        return (centroids, assignments, counts);
    }

    private static Vector3[] PrepareInitialCentroids(
        QuantizationSample[] samples,
        int clusterCount,
        Vector3[]? previousCentroids,
        Random rng,
        CancellationToken cancellationToken)
    {
        if (samples.Length == 0)
        {
            throw new InvalidOperationException("Cannot initialize centroids without samples.");
        }

        if (previousCentroids is { Length: > 0 })
        {
            var centroids = new Vector3[clusterCount];
            var copyLength = Math.Min(clusterCount, previousCentroids.Length);
            Array.Copy(previousCentroids, centroids, copyLength);
            for (var i = copyLength; i < clusterCount; i++)
            {
                var index = rng.Next(samples.Length);
                centroids[i] = samples[index].Color;
            }

            return centroids;
        }

        var result = new Vector3[clusterCount];
        var sampleCount = samples.Length;
        var distances = new double[sampleCount];

        var firstIndex = SelectWeightedSampleIndex(samples, rng);
        result[0] = samples[firstIndex].Color;

        for (var centroidIndex = 1; centroidIndex < clusterCount; centroidIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            double totalDistance = 0;
            for (var i = 0; i < sampleCount; i++)
            {
                var distance = DistanceSquaredToClosestCentroid(samples[i].Color, result, centroidIndex);
                distances[i] = distance;
                totalDistance += distance * Math.Max(1, samples[i].Weight);
            }

            if (totalDistance <= 0)
            {
                var fallback = SelectWeightedSampleIndex(samples, rng);
                result[centroidIndex] = samples[fallback].Color;
                continue;
            }

            var threshold = rng.NextDouble() * totalDistance;
            double cumulative = 0;

            for (var i = 0; i < sampleCount; i++)
            {
                cumulative += distances[i] * Math.Max(1, samples[i].Weight);
                if (cumulative >= threshold)
                {
                    result[centroidIndex] = samples[i].Color;
                    break;
                }
            }
        }

        return result;
    }

    private static int SelectWeightedSampleIndex(QuantizationSample[] samples, Random rng)
    {
        double totalWeight = 0;
        for (var i = 0; i < samples.Length; i++)
        {
            totalWeight += Math.Max(1, samples[i].Weight);
        }

        var threshold = rng.NextDouble() * totalWeight;
        double cumulative = 0;

        for (var i = 0; i < samples.Length; i++)
        {
            cumulative += Math.Max(1, samples[i].Weight);
            if (cumulative >= threshold)
            {
                return i;
            }
        }

        return samples.Length - 1;
    }

    private static bool AssignSamples(QuantizationSample[] samples, Vector3[] centroids, int[] assignments)
    {
        var changed = false;

        for (var i = 0; i < samples.Length; i++)
        {
            var nearest = FindNearestCentroid(samples[i].Color, centroids);
            if (assignments[i] != nearest)
            {
                assignments[i] = nearest;
                changed = true;
            }
        }

        return changed;
    }

    private static double DistanceSquaredToClosestCentroid(Vector3 sample, Vector3[] centroids, int length)
    {
        var best = double.PositiveInfinity;

        for (var i = 0; i < length; i++)
        {
            var distance = Vector3.DistanceSquared(sample, centroids[i]);
            if (distance < best)
            {
                best = distance;
            }
        }

        return best;
    }

    private static int FindNearestCentroid(Vector3 sample, Vector3[] centroids)
    {
        var bestIndex = 0;
        var bestDistance = float.PositiveInfinity;

        for (var i = 0; i < centroids.Length; i++)
        {
            var distance = Vector3.DistanceSquared(sample, centroids[i]);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;
            }
        }

        return bestIndex;
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

    private static byte ToByte(float value)
    {
        var clamped = Math.Clamp((int)MathF.Round(value, MidpointRounding.AwayFromZero), 0, 255);
        return (byte)clamped;
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
        RandomSeed = 17,
        MultiScaleBlockSizes = ImmutableArray.Create(4, 2, 1)
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

    /// <summary>
    /// マルチスケール量子化で使用するブロックサイズの系列。
    /// </summary>
    public ImmutableArray<int> MultiScaleBlockSizes { get; init; } = ImmutableArray.Create(4, 2, 1);
}
