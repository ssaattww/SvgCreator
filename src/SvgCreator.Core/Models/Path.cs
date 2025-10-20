using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;

namespace SvgCreator.Core.Models;

/// <summary>
/// Represents an ordered sequence of path segments forming a closed or open contour.
/// </summary>
[DebuggerDisplay("Segments = {Segments.Length}")]
public sealed class Path
{
    public Path(ImmutableArray<PathSegment> segments)
    {
        if (segments.IsDefaultOrEmpty)
        {
            throw new ArgumentException("Path must contain at least one segment.", nameof(segments));
        }

        ValidateSegments(segments);
        Segments = segments;
    }

    public Path(IEnumerable<PathSegment> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);
        var immutable = ImmutableArray.CreateRange(segments);

        if (immutable.IsDefaultOrEmpty)
        {
            throw new ArgumentException("Path must contain at least one segment.", nameof(segments));
        }

        ValidateSegments(immutable);
        Segments = immutable;
    }

    public ImmutableArray<PathSegment> Segments { get; }

    private static void ValidateSegments(ImmutableArray<PathSegment> segments)
    {
        if (segments[0].Type != PathSegmentType.Move)
        {
            throw new ArgumentException("First segment must be a Move command.", nameof(segments));
        }

        foreach (var segment in segments)
        {
            if (segment is null)
            {
                throw new ArgumentException("Segments cannot contain null entries.", nameof(segments));
            }
        }
    }
}
