using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Numerics;

namespace SvgCreator.Core.Models;

/// <summary>
/// Path segment types supported within SVG output.
/// </summary>
public enum PathSegmentType
{
    Move,
    Line,
    CubicBezier,
    QuadraticBezier,
    Close
}

/// <summary>
/// Represents a single SVG path segment with strongly typed geometry.
/// </summary>
[DebuggerDisplay("{Type} ({Points.Length} points)")]
public sealed class PathSegment
{
    public PathSegment(PathSegmentType type, IReadOnlyList<Vector2> points)
    {
        ArgumentNullException.ThrowIfNull(points);

        ValidatePointCount(type, points.Count);

        Type = type;
        Points = ImmutableArray.CreateRange(points);
    }

    public PathSegmentType Type { get; }

    public ImmutableArray<Vector2> Points { get; }

    private static void ValidatePointCount(PathSegmentType type, int count)
    {
        var isValid = type switch
        {
            PathSegmentType.Move or PathSegmentType.Line => count == 1,
            PathSegmentType.CubicBezier => count == 3,
            PathSegmentType.QuadraticBezier => count == 2,
            PathSegmentType.Close => count == 0,
            _ => false
        };

        if (!isValid)
        {
            throw new ArgumentException($"Invalid point count {count} for segment type {type}.", nameof(count));
        }
    }
}
