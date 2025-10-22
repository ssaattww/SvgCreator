using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using SvgCreator.Core.Models;

namespace SvgCreator.Core.Diagnostics;

/// <summary>
/// デバッグ用に保存されるレイヤー情報です。
/// </summary>
public sealed class DebugSnapshotLayer
{
    public required string Id { get; init; }

    public required RgbColor Color { get; init; }

    public required int Area { get; init; }

    public required DebugSnapshotMask Mask { get; init; }

    public required IReadOnlyList<Vector2> Boundary { get; init; }

    public required IReadOnlyList<IReadOnlyList<Vector2>> Holes { get; init; }

    /// <summary>
    /// <see cref="ShapeLayer"/> から <see cref="DebugSnapshotLayer"/> を生成します。
    /// </summary>
    /// <param name="layer">変換元のレイヤー。</param>
    /// <returns>生成されたデバッグレイヤー。</returns>
    public static DebugSnapshotLayer From(ShapeLayer layer)
    {
        return new DebugSnapshotLayer
        {
            Id = layer.Id,
            Color = layer.Color,
            Area = layer.Area,
            Mask = new DebugSnapshotMask
            {
                Width = layer.Mask.Width,
                Height = layer.Mask.Height,
                Bits = layer.Mask.Bits.ToArray()
            },
            Boundary = layer.Boundary.ToArray(),
            Holes = layer.Holes.Select(h => (IReadOnlyList<Vector2>)h.ToArray()).ToArray()
        };
    }
}
