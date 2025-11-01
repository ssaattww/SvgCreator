using System;
using System.Collections.Immutable;
using System.Numerics;
using SvgCreator.Core.Models;

namespace SvgCreator.Core.Quantization;

/// <summary>
/// 複数スケールのブロック平均を生成し、マルチスケール K-means のサンプル集合を作成します。
/// </summary>
internal static class MultiScalePixelSampler
{
    /// <summary>
    /// 指定したブロックサイズ系列に基づき、各スケールでの色サンプル集合を生成します。
    /// </summary>
    /// <param name="image">入力画像。</param>
    /// <param name="blockSizes">ブロック一辺の画素数。降順に指定し、最終スケールには 1 を含めます。</param>
    /// <returns>スケールごとのサンプル配列。</returns>
    public static ImmutableArray<QuantizationSample[]> CreateSamples(ImageData image, ImmutableArray<int> blockSizes)
    {
        if (image is null)
        {
            throw new ArgumentNullException(nameof(image));
        }

        if (blockSizes.IsDefaultOrEmpty)
        {
            throw new ArgumentException("At least one block size must be provided.", nameof(blockSizes));
        }

        var builder = ImmutableArray.CreateBuilder<QuantizationSample[]>(blockSizes.Length);

        foreach (var blockSize in blockSizes)
        {
            if (blockSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(blockSizes), blockSize, "Block size must be positive.");
            }

            builder.Add(CreateLevel(image, blockSize));
        }

        return builder.ToImmutable();
    }

    private static QuantizationSample[] CreateLevel(ImageData image, int blockSize)
    {
        var width = image.Width;
        var height = image.Height;
        var pixels = image.Pixels.Span;
        var bytesPerPixel = 3; // Quantizer は RGB を前提とする

        var blocksX = (width + blockSize - 1) / blockSize;
        var blocksY = (height + blockSize - 1) / blockSize;

        var samples = new QuantizationSample[blocksX * blocksY];
        var index = 0;

        for (var by = 0; by < blocksY; by++)
        {
            var yStart = by * blockSize;
            var blockHeight = Math.Min(blockSize, height - yStart);

            for (var bx = 0; bx < blocksX; bx++)
            {
                var xStart = bx * blockSize;
                var blockWidth = Math.Min(blockSize, width - xStart);

                var weight = blockWidth * blockHeight;
                var accumulator = Vector3.Zero;

                for (var dy = 0; dy < blockHeight; dy++)
                {
                    var row = yStart + dy;
                    var rowOffset = row * width * bytesPerPixel;

                    for (var dx = 0; dx < blockWidth; dx++)
                    {
                        var col = xStart + dx;
                        var offset = rowOffset + col * bytesPerPixel;

                        accumulator.X += pixels[offset + 0];
                        accumulator.Y += pixels[offset + 1];
                        accumulator.Z += pixels[offset + 2];
                    }
                }

                var color = accumulator / weight;
                samples[index++] = new QuantizationSample(color, weight);
            }
        }

        return samples;
    }
}
