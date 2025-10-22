using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Numerics;

namespace SvgCreator.Core.Models;

/// <summary>
/// SVG 出力で利用するパスセグメント種別を表します。
/// </summary>
public enum PathSegmentType
{
    /// <summary>
    /// 始点を移動する Move コマンドです。
    /// </summary>
    Move,

    /// <summary>
    /// 直線コマンドです。
    /// </summary>
    Line,

    /// <summary>
    /// 3 次ベジェ曲線コマンドです。
    /// </summary>
    CubicBezier,

    /// <summary>
    /// 2 次ベジェ曲線コマンドです。
    /// </summary>
    QuadraticBezier,

    /// <summary>
    /// パスを閉じる Close コマンドです。
    /// </summary>
    Close
}

/// <summary>
/// SVG パスの 1 セグメントと制御点情報を表します。
/// </summary>
[DebuggerDisplay("{Type} ({Points.Length} points)")]
public sealed class PathSegment
{
    /// <summary>
    /// <see cref="PathSegment"/> を初期化します。
    /// </summary>
    /// <param name="type">セグメント種別。</param>
    /// <param name="points">必要な制御点列。</param>
    /// <exception cref="ArgumentNullException"><paramref name="points"/> が <c>null</c> です。</exception>
    /// <exception cref="ArgumentException">制御点数がセグメント種別と一致しません。</exception>
    public PathSegment(PathSegmentType type, IReadOnlyList<Vector2> points)
    {
        ArgumentNullException.ThrowIfNull(points);

        ValidatePointCount(type, points.Count);

        Type = type;
        Points = ImmutableArray.CreateRange(points);
    }

    /// <summary>
    /// セグメント種別を取得します。
    /// </summary>
    public PathSegmentType Type { get; }

    /// <summary>
    /// セグメントの制御点列を取得します。
    /// </summary>
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
