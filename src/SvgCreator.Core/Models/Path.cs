using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;

namespace SvgCreator.Core.Models;

/// <summary>
/// 閉じた輪郭または開いた輪郭を構成するパスセグメント列を表します。
/// </summary>
[DebuggerDisplay("Segments = {Segments.Length}")]
public sealed class Path
{
    /// <summary>
    /// 不変配列から <see cref="Path"/> を初期化します。
    /// </summary>
    /// <param name="segments">Move で始まるパスセグメント列。</param>
    /// <exception cref="ArgumentException">セグメントが空、または先頭が Move ではありません。</exception>
    public Path(ImmutableArray<PathSegment> segments)
    {
        if (segments.IsDefaultOrEmpty)
        {
            throw new ArgumentException("Path must contain at least one segment.", nameof(segments));
        }

        ValidateSegments(segments);
        Segments = segments;
    }

    /// <summary>
    /// 列挙可能なセグメントから <see cref="Path"/> を初期化します。
    /// </summary>
    /// <param name="segments">Move で始まるパスセグメント列。</param>
    /// <exception cref="ArgumentNullException"><paramref name="segments"/> が <c>null</c> です。</exception>
    /// <exception cref="ArgumentException">セグメントが空、または先頭が Move ではありません。</exception>
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

    /// <summary>
    /// パスを構成するセグメント列を取得します。
    /// </summary>
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
