using System;

namespace SvgCreator.Core.Models;

/// <summary>
/// Represents the supported pixel formats for <see cref="ImageData"/>.
/// </summary>
public enum PixelFormat
{
    /// <summary>
    /// Three channels (RGB) without alpha.
    /// </summary>
    Rgb = 3,

    /// <summary>
    /// Four channels (RGBA).
    /// </summary>
    Rgba = 4,

    /// <summary>
    /// Single channel grayscale.
    /// </summary>
    Grayscale = 1
}

internal static class PixelFormatExtensions
{
    public static int GetBytesPerPixel(this PixelFormat format) =>
        format switch
        {
            PixelFormat.Rgb => 3,
            PixelFormat.Rgba => 4,
            PixelFormat.Grayscale => 1,
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported pixel format.")
        };
}
