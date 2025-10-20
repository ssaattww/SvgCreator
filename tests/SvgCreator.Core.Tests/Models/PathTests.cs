using System;
using System.Collections.Immutable;
using System.Numerics;
using SvgCreator.Core.Models;
using PathModel = SvgCreator.Core.Models.Path;

namespace SvgCreator.Core.Tests.Models;

public sealed class PathTests
{
    [Fact]
    // セグメントが空のパスは生成できないことを確認
    public void Constructor_Throws_WhenSegmentsEmpty()
    {
        Assert.Throws<ArgumentException>(() => new PathModel(ImmutableArray<PathSegment>.Empty));
    }

    [Fact]
    // 先頭が Move 以外のパスは生成できないことを確認
    public void Constructor_Throws_WhenFirstSegmentIsNotMove()
    {
        var segment = new PathSegment(PathSegmentType.Line, new[] { Vector2.Zero });
        Assert.Throws<ArgumentException>(() => new PathModel(ImmutableArray.Create(segment)));
    }

    [Fact]
    // 有効なセグメント列でプロパティが設定されることを確認
    public void Constructor_AssignsSegments()
    {
        var move = new PathSegment(PathSegmentType.Move, new[] { new Vector2(0, 0) });
        var line = new PathSegment(PathSegmentType.Line, new[] { new Vector2(1, 1) });

        var path = new PathModel(ImmutableArray.Create(move, line));

        Assert.Equal(2, path.Segments.Length);
        Assert.Equal(move, path.Segments[0]);
        Assert.Equal(line, path.Segments[1]);
    }
}
