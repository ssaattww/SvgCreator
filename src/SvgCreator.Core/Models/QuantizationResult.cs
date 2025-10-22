using System;
using System.Collections.Immutable;
using System.Diagnostics;

namespace SvgCreator.Core.Models;

/// <summary>
/// 減色処理の結果（入力画像・パレット・ラベルマップ）を保持します。
/// </summary>
[DebuggerDisplay("Palette={Palette.Length} Colors")]
public sealed class QuantizationResult
{
    /// <summary>
    /// <see cref="QuantizationResult"/> を初期化します。
    /// </summary>
    /// <param name="image">元画像。</param>
    /// <param name="palette">量子化後のカラーパレット。</param>
    /// <param name="labelIndices">各画素のパレット参照インデックス。</param>
    /// <exception cref="ArgumentNullException"><paramref name="image"/> が <c>null</c> です。</exception>
    /// <exception cref="ArgumentException">パレットが空、またはラベル長がピクセル数と一致しません。</exception>
    /// <exception cref="ArgumentOutOfRangeException">ラベル値がパレット範囲外です。</exception>
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

    /// <summary>
    /// 元画像を取得します。
    /// </summary>
    public ImageData Image { get; }

    /// <summary>
    /// 使用したカラーパレットを取得します。
    /// </summary>
    public ImmutableArray<RgbColor> Palette { get; }

    /// <summary>
    /// 各画素に対応するパレットインデックス（行優先）を取得します。
    /// </summary>
    public ImmutableArray<int> LabelIndices { get; }
}
