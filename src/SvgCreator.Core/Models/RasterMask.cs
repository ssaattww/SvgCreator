using System;
using System.Collections.Immutable;
using System.Diagnostics;

namespace SvgCreator.Core.Models;

/// <summary>
/// Immutable raster mask representing a set of pixels belonging to a shape.
/// </summary>
[DebuggerDisplay("{Width}x{Height}")]
public sealed class RasterMask
{
    public RasterMask(int width, int height, ImmutableArray<bool> bits)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, "Width must be positive.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), height, "Height must be positive.");
        }

        if (bits.IsDefaultOrEmpty)
        {
            throw new ArgumentException("Bit mask cannot be empty.", nameof(bits));
        }

        if (bits.Length != width * height)
        {
            throw new ArgumentException("Bit mask length does not match mask dimensions.", nameof(bits));
        }

        Width = width;
        Height = height;
        Bits = bits;
    }

    public int Width { get; }

    public int Height { get; }

    public ImmutableArray<bool> Bits { get; }

    public bool this[int x, int y]
    {
        get
        {
            if ((uint)x >= (uint)Width)
            {
                throw new ArgumentOutOfRangeException(nameof(x));
            }

            if ((uint)y >= (uint)Height)
            {
                throw new ArgumentOutOfRangeException(nameof(y));
            }

            var index = y * Width + x;
            return Bits[index];
        }
    }
}
