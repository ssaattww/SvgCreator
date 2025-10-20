using System;
using System.Numerics;
using SvgCreator.Core.Models;

namespace SvgCreator.Core.Tests.Models;

public sealed class PathSegmentTests
{
    [Theory]
    [InlineData(PathSegmentType.Move, 0)]
    [InlineData(PathSegmentType.Move, 2)]
    [InlineData(PathSegmentType.Line, 0)]
    [InlineData(PathSegmentType.CubicBezier, 2)]
    [InlineData(PathSegmentType.QuadraticBezier, 1)]
    [InlineData(PathSegmentType.Close, 1)]
    public void Constructor_Throws_WhenPointCountInvalid(PathSegmentType type, int pointCount)
    {
        var points = new Vector2[pointCount];
        Assert.Throws<ArgumentException>(() => new PathSegment(type, points));
    }

    [Theory]
    [InlineData(PathSegmentType.Move, 1)]
    [InlineData(PathSegmentType.Line, 1)]
    [InlineData(PathSegmentType.CubicBezier, 3)]
    [InlineData(PathSegmentType.QuadraticBezier, 2)]
    [InlineData(PathSegmentType.Close, 0)]
    public void Constructor_AllowsValidPointCounts(PathSegmentType type, int pointCount)
    {
        var points = new Vector2[pointCount];
        var segment = new PathSegment(type, points);

        Assert.Equal(type, segment.Type);
        Assert.Equal(pointCount, segment.Points.Length);
    }

    [Fact]
    public void Constructor_CopiesPoints()
    {
        var points = new[] { new Vector2(1, 2) };
        var segment = new PathSegment(PathSegmentType.Move, points);

        points[0] = Vector2.Zero;

        Assert.Equal(new Vector2(1, 2), segment.Points[0]);
    }
}
