using System;
using System.Collections.Immutable;
using System.Diagnostics;

namespace SvgCreator.Core.Models;

/// <summary>
/// Represents the output of the color quantization stage.
/// </summary>
[DebuggerDisplay("Palette={Palette.Length} Colors")]
public sealed class QuantizationResult
{
    public QuantizationResult(
        ImageData image,
        ImmutableArray<RgbColor> palette,
        ImmutableArray<int> labelIndices)
    {
        ArgumentNullException.ThrowIfNull(image);

        if (palette.IsDefaultOrEmpty)
        {
            throw new ArgumentException("Palette must contain at least one color.", nameof(palette));
        }

        if (labelIndices.IsDefault || labelIndices.Length != image.Width * image.Height)
        {
            throw new ArgumentException("Label indices length must match the number of pixels.", nameof(labelIndices));
        }

        for (var i = 0; i < labelIndices.Length; i++)
        {
            var index = labelIndices[i];
            if ((uint)index >= (uint)palette.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(labelIndices), index, "Label index must refer to an existing palette entry.");
            }
        }

        Image = image;
        Palette = palette;
        LabelIndices = labelIndices;
    }

    public ImageData Image { get; }

    public ImmutableArray<RgbColor> Palette { get; }

    /// <summary>
    /// Gets an array of palette indices per pixel in row-major order.
    /// </summary>
    public ImmutableArray<int> LabelIndices { get; }
}
