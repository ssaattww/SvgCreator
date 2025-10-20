using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Numerics;

namespace SvgCreator.Core.Models;

/// <summary>
/// ラスタ画像から抽出された準凸領域（レイヤー）を表します。
/// </summary>
[DebuggerDisplay("{Id} Area={Area}")]
public sealed class ShapeLayer
{
    /// <summary>
    /// <see cref="ShapeLayer"/> を初期化します。
    /// </summary>
    /// <param name="id">レイヤー ID。</param>
    /// <param name="color">レイヤーの代表色。</param>
    /// <param name="mask">ピクセルレベルのマスク。</param>
    /// <param name="boundary">外周輪郭（反時計回り想定）。</param>
    /// <param name="holes">穴領域のコレクション（0 個可）。</param>
    /// <param name="area">レイヤーの画素数。</param>
    /// <exception cref="ArgumentException">ID が空白、または境界点数が 3 未満です。</exception>
    /// <exception cref="ArgumentNullException"><paramref name="mask"/> が <c>null</c> です。</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="area"/> が 1 未満です。</exception>
    public ShapeLayer(
        string id,
        RgbColor color,
        RasterMask mask,
        ImmutableArray<Vector2> boundary,
        ImmutableArray<IImmutableList<Vector2>> holes,
        int area)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Layer id must be non-empty.", nameof(id));
        }

        ArgumentNullException.ThrowIfNull(mask);

        if (boundary.IsDefault || boundary.Length < 3)
        {
            throw new ArgumentException("Boundary must contain at least three points.", nameof(boundary));
        }

        if (area <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(area), area, "Area must be positive.");
        }

        Id = id;
        Color = color;
        Mask = mask;
        Boundary = boundary;
        Holes = holes.IsDefault ? ImmutableArray<IImmutableList<Vector2>>.Empty : holes;
        Area = area;
    }

    /// <summary>
    /// レイヤー ID を取得します。
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// レイヤーの代表色を取得します。
    /// </summary>
    public RgbColor Color { get; }

    /// <summary>
    /// レイヤーのマスクを取得します。
    /// </summary>
    public RasterMask Mask { get; }

    /// <summary>
    /// レイヤー外周の輪郭座標列を取得します。
    /// </summary>
    public ImmutableArray<Vector2> Boundary { get; }

    /// <summary>
    /// 穴領域の輪郭集合を取得します。
    /// </summary>
    public ImmutableArray<IImmutableList<Vector2>> Holes { get; }

    /// <summary>
    /// レイヤーの画素数を取得します。
    /// </summary>
    public int Area { get; }
}
