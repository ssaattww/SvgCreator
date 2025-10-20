using System;
using System.Collections.Immutable;
using System.Diagnostics;

namespace SvgCreator.Core.Models;

/// <summary>
/// 領域に属する画素集合を表す不変のラスターマスクです。
/// </summary>
[DebuggerDisplay("{Width}x{Height}")]
public sealed class RasterMask
{
    /// <summary>
    /// <see cref="RasterMask"/> を初期化します。
    /// </summary>
    /// <param name="width">マスクの幅（ピクセル単位）。</param>
    /// <param name="height">マスクの高さ（ピクセル単位）。</param>
    /// <param name="bits">行優先で並んだブール値の配列。</param>
    /// <exception cref="ArgumentOutOfRangeException">幅または高さが 1 未満です。</exception>
    /// <exception cref="ArgumentException">ビット配列が空、または要素数が幅×高さと一致しません。</exception>
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

    /// <summary>
    /// マスクの幅（ピクセル単位）を取得します。
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// マスクの高さ（ピクセル単位）を取得します。
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// マスク値の不変配列を取得します。
    /// </summary>
    public ImmutableArray<bool> Bits { get; }

    /// <summary>
    /// 指定座標のマスク値を取得します。
    /// </summary>
    /// <param name="x">X 座標。</param>
    /// <param name="y">Y 座標。</param>
    /// <returns>対象ピクセルがマスクされている場合は <c>true</c>。</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="x"/> または <paramref name="y"/> が範囲外です。</exception>
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
