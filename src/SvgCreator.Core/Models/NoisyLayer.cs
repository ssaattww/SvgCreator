using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Numerics;

namespace SvgCreator.Core.Models;

/// <summary>
/// 微小成分として除外されたレイヤー情報を表します。
/// </summary>
[DebuggerDisplay("{Id} Area={Area}")]
public sealed class NoisyLayer
{
    /// <summary>
    /// <see cref="NoisyLayer"/> を初期化します。
    /// </summary>
    /// <param name="id">ノイズレイヤー ID。</param>
    /// <param name="color">代表色。</param>
    /// <param name="mask">ビットマスク。</param>
    /// <param name="boundary">外周輪郭（反時計回り）。</param>
    /// <param name="area">画素数。</param>
    /// <exception cref="ArgumentException">ID が空白、または境界点数が 3 未満です。</exception>
    /// <exception cref="ArgumentNullException"><paramref name="mask"/> が <c>null</c> です。</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="area"/> が 1 未満です。</exception>
    public NoisyLayer(
        string id,
        RgbColor color,
        RasterMask mask,
        ImmutableArray<Vector2> boundary,
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
        Area = area;
    }

    /// <summary>
    /// ノイズレイヤー ID を取得します。
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// 代表色を取得します。
    /// </summary>
    public RgbColor Color { get; }

    /// <summary>
    /// 画素マスクを取得します。
    /// </summary>
    public RasterMask Mask { get; }

    /// <summary>
    /// 外周輪郭を取得します。
    /// </summary>
    public ImmutableArray<Vector2> Boundary { get; }

    /// <summary>
    /// 画素数を取得します。
    /// </summary>
    public int Area { get; }
}
