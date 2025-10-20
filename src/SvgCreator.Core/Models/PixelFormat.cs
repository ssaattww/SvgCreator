using System;

namespace SvgCreator.Core.Models;

/// <summary>
/// <see cref="ImageData"/> で使用されるピクセル形式を表します。
/// </summary>
public enum PixelFormat
{
    /// <summary>
    /// アルファ無しの RGB 3 チャネル形式です。
    /// </summary>
    Rgb = 3,

    /// <summary>
    /// アルファ付きの RGBA 4 チャネル形式です。
    /// </summary>
    Rgba = 4,

    /// <summary>
    /// 1 チャネルのグレースケール形式です。
    /// </summary>
    Grayscale = 1
}

internal static class PixelFormatExtensions
{
    /// <summary>
    /// ピクセル形式ごとの 1 画素当たりのバイト数を返します。
    /// </summary>
    /// <param name="format">対象のピクセル形式。</param>
    /// <returns>1 画素当たりのバイト数。</returns>
    /// <exception cref="ArgumentOutOfRangeException">サポート外のピクセル形式が指定されました。</exception>
    public static int GetBytesPerPixel(this PixelFormat format) =>
        format switch
        {
            PixelFormat.Rgb => 3,
            PixelFormat.Rgba => 4,
            PixelFormat.Grayscale => 1,
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported pixel format.")
        };
}
