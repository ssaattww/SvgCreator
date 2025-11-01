using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SvgCreator.Core;
using SvgCreator.Core.Models;
using SvgCreator.Core.Quantization;
using SvgCreator.Core.Orchestration;
using Xunit;

namespace SvgCreator.Core.Tests.Quantization;

public sealed class MultiScalePixelSamplerTests
{
    [Fact]
    public void CreateSamples_ComputesWeightedAveragesPerBlock()
    {
        var pixels = new byte[]
        {
            // Row 0
            10, 20, 30,
            40, 50, 60,
            70, 80, 90,
            // Row 1
            20, 30, 40,
            50, 60, 70,
            80, 90, 100
        };

        var image = new ImageData(3, 2, PixelFormat.Rgb, pixels);

        var samples = MultiScalePixelSampler.CreateSamples(image, ImmutableArray.Create(2, 1));

        Assert.Equal(2, samples.Count);

        var coarse = samples[0];
        Assert.Equal(2, coarse.Length);

        Assert.Equal(4, coarse[0].Weight);
        AssertVector(coarse[0].Color, 30f, 40f, 50f);

        Assert.Equal(2, coarse[1].Weight);
        AssertVector(coarse[1].Color, 75f, 85f, 95f);

        var fine = samples[1];
        Assert.Equal(image.Width * image.Height, fine.Length);
        Assert.All(fine, sample => Assert.Equal(1, sample.Weight));
    }

    [Fact]
    public async Task QuantizeAsync_WithMultiScaleSeedsRetainsIntermediateCentroid()
    {
        var pixels = new byte[4 * 4 * 3];

        void WritePixel(int x, int y, byte r, byte g, byte b)
        {
            var index = (y * 4 + x) * 3;
            pixels[index + 0] = r;
            pixels[index + 1] = g;
            pixels[index + 2] = b;
        }

        for (var y = 0; y < 2; y++)
        {
            WritePixel(0, y, 0, 0, 0);
            WritePixel(1, y, 200, 0, 0);
            WritePixel(2, y, 200, 0, 0);
            WritePixel(3, y, 200, 0, 0);
        }

        for (var y = 2; y < 4; y++)
        {
            WritePixel(0, y, 0, 200, 0);
            WritePixel(1, y, 0, 0, 200);
            WritePixel(2, y, 0, 0, 200);
            WritePixel(3, y, 0, 0, 200);
        }

        var image = new ImageData(4, 4, PixelFormat.Rgb, pixels);
        var options = new SvgCreatorRunOptions("input.png", "out")
        {
            QuantizationClusterCount = 2
        };

        var settings = new QuantizerSettings
        {
            MinimumClusterCount = 1,
            MaximumClusterCount = 8,
            DefaultClusterCount = 2,
            MaxIterations = 1,
            RandomSeed = 7,
            MultiScaleBlockSizes = ImmutableArray.Create(2, 1)
        };

        var quantizer = new Quantizer(settings);
        var result = await quantizer.QuantizeAsync(image, options, CancellationToken.None);

        Assert.Equal(2, result.Palette.Length);

        var intermediateRed = result.Palette.Single(color => color.G == 0 && color.B == 0);
        Assert.InRange(intermediateRed.R, 90, 110);
    }

    private static void AssertVector(System.Numerics.Vector3 actual, float expectedX, float expectedY, float expectedZ)
    {
        const float tolerance = 1e-3f;
        Assert.InRange(actual.X, expectedX - tolerance, expectedX + tolerance);
        Assert.InRange(actual.Y, expectedY - tolerance, expectedY + tolerance);
        Assert.InRange(actual.Z, expectedZ - tolerance, expectedZ + tolerance);
    }
}
