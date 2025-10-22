using System;
using System.Diagnostics;

namespace SvgCreator.Core.Models;

/// <summary>
/// パイプライン全体で利用するラスタ画像データを不変オブジェクトとして保持します。
/// </summary>
[DebuggerDisplay("{Width}x{Height} {Format}")]
public sealed class ImageData
{
    private readonly byte[] _buffer;

    /// <summary>
    /// <see cref="ImageData"/> を初期化します。
    /// </summary>
    /// <param name="width">画像の幅（ピクセル単位）。</param>
    /// <param name="height">画像の高さ（ピクセル単位）。</param>
    /// <param name="format">ピクセル形式。</param>
    /// <param name="pixels">行優先で格納されたピクセルバッファ。</param>
    /// <exception cref="ArgumentOutOfRangeException">幅または高さが 1 未満です。</exception>
    /// <exception cref="ArgumentNullException"><paramref name="pixels"/> が <c>null</c> です。</exception>
    /// <exception cref="ArgumentException">バッファ長が期待値と一致しません。</exception>
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

    /// <summary>
    /// 画像の幅（ピクセル単位）を取得します。
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// 画像の高さ（ピクセル単位）を取得します。
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// ピクセル形式を取得します。
    /// </summary>
    public PixelFormat Format { get; }

    /// <summary>
    /// ピクセルバッファへの読み取り専用ビューを取得します。
    /// </summary>
    public ReadOnlyMemory<byte> Pixels { get; }

}
