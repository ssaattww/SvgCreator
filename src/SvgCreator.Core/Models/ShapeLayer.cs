using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Numerics;

namespace SvgCreator.Core.Models;

/// <summary>
/// Represents a single convex-ish region extracted from the raster image.
/// </summary>
[DebuggerDisplay("{Id} Area={Area}")]
public sealed class ShapeLayer
{
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

    public string Id { get; }

    public RgbColor Color { get; }

    public RasterMask Mask { get; }

    public ImmutableArray<Vector2> Boundary { get; }

    public ImmutableArray<IImmutableList<Vector2>> Holes { get; }

    public int Area { get; }
}
