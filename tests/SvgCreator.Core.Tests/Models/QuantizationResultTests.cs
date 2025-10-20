using System;
using System.Collections.Immutable;
using SvgCreator.Core.Models;

namespace SvgCreator.Core.Tests.Models;

public sealed class QuantizationResultTests
{
    [Fact]
    public void Constructor_Throws_WhenPaletteEmpty()
    {
        var image = CreateImage();
        var labels = ImmutableArray.Create(0);

        Assert.Throws<ArgumentException>(() => new QuantizationResult(image, ImmutableArray<RgbColor>.Empty, labels));
    }

    [Fact]
    public void Constructor_Throws_WhenLabelsLengthDoesNotMatchImagePixels()
    {
        var image = CreateImage();
        var palette = ImmutableArray.Create(new RgbColor(1, 2, 3));
        var labels = ImmutableArray.Create(0, 0); // length mismatch

        Assert.Throws<ArgumentException>(() => new QuantizationResult(image, palette, labels));
    }

    [Fact]
    public void Constructor_Throws_WhenLabelReferencesInvalidPaletteIndex()
    {
        var image = CreateImage();
        var palette = ImmutableArray.Create(new RgbColor(1, 2, 3));
        var labels = ImmutableArray.Create(1);

        Assert.Throws<ArgumentOutOfRangeException>(() => new QuantizationResult(image, palette, labels));
    }

    [Fact]
    public void Constructor_AssignsProperties()
    {
        var image = CreateImage();
        var palette = ImmutableArray.Create(new RgbColor(1, 2, 3));
        var labels = ImmutableArray.Create(0);

        var result = new QuantizationResult(image, palette, labels);

        Assert.Equal(image, result.Image);
        Assert.Equal(palette, result.Palette);
        Assert.Equal(labels, result.LabelIndices);
    }

    private static ImageData CreateImage()
    {
        var pixels = new byte[] { 0, 0, 0 };
        return new ImageData(1, 1, PixelFormat.Rgb, pixels);
    }
}
