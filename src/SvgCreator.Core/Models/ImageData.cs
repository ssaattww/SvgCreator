using System;
using System.Diagnostics;

namespace SvgCreator.Core.Models;

/// <summary>
/// Immutable representation of raster image data used throughout the pipeline.
/// </summary>
[DebuggerDisplay("{Width}x{Height} {Format}")]
public sealed class ImageData
{
    private readonly byte[] _buffer;

    public ImageData(int width, int height, PixelFormat format, byte[] pixels)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, "Width must be positive.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), height, "Height must be positive.");
        }

        ArgumentNullException.ThrowIfNull(pixels);

        var expectedLength = checked(width * height * format.GetBytesPerPixel());

        if (pixels.Length != expectedLength)
        {
            throw new ArgumentException($"Pixel buffer length {pixels.Length} does not match expected length {expectedLength}.", nameof(pixels));
        }

        Width = width;
        Height = height;
        Format = format;

        _buffer = new byte[pixels.Length];
        Array.Copy(pixels, _buffer, pixels.Length);
        Pixels = _buffer.AsMemory();
    }

    public int Width { get; }

    public int Height { get; }

    public PixelFormat Format { get; }

    /// <summary>
    /// Gets a read-only view over the pixel buffer.
    /// </summary>
    public ReadOnlyMemory<byte> Pixels { get; }

}
