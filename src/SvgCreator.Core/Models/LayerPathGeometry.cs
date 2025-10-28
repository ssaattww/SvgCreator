using System;
using System.Collections.Immutable;

namespace SvgCreator.Core.Models;

/// <summary>
/// シェイプレイヤーに対応するベクタ化後のパス集合を表します。
/// </summary>
public sealed class LayerPathGeometry
{
    /// <summary>
    /// <see cref="LayerPathGeometry"/> を初期化します。
    /// </summary>
    /// <param name="layerId">対象レイヤー ID。</param>
    /// <param name="color">レイヤーの代表色。</param>
    /// <param name="outerPath">外周輪郭を表すパス。</param>
    /// <param name="holePaths">穴領域を表すパスの不変配列。</param>
    /// <exception cref="ArgumentException"><paramref name="layerId"/> が空白、または <paramref name="outerPath"/> が <c>null</c> です。</exception>
    public LayerPathGeometry(string layerId, RgbColor color, Path outerPath, ImmutableArray<Path> holePaths)
    {
        if (string.IsNullOrWhiteSpace(layerId))
        {
            throw new ArgumentException("Layer id must be non-empty.", nameof(layerId));
        }

        OuterPath = outerPath ?? throw new ArgumentException("Outer path cannot be null.", nameof(outerPath));
        HolePaths = holePaths.IsDefault ? ImmutableArray<Path>.Empty : holePaths;
        LayerId = layerId;
        Color = color;
    }

    /// <summary>
    /// レイヤー ID を取得します。
    /// </summary>
    public string LayerId { get; }

    /// <summary>
    /// レイヤーの代表色を取得します。
    /// </summary>
    public RgbColor Color { get; }

    /// <summary>
    /// 外周輪郭パスを取得します。
    /// </summary>
    public Path OuterPath { get; }

    /// <summary>
    /// 穴領域パスの不変配列を取得します。
    /// </summary>
    public ImmutableArray<Path> HolePaths { get; }
}
