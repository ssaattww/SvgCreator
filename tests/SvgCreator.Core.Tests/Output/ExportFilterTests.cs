using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Numerics;
using SvgCreator.Core.Models;
using SvgCreator.Core.Svg;
using Xunit;
using PathModel = SvgCreator.Core.Models.Path;

namespace SvgCreator.Core.Tests.Output;

public sealed class ExportFilterTests
{
    // Filter_NoSelection_ReturnsAllLayersInDepthOrder の挙動を検証します。
    [Fact]
    public void Filter_NoSelection_ReturnsAllLayersInDepthOrder()
    {
        var depthOrder = CreateDepthOrder(("layer-back", 0), ("layer-front", 1), ("layer-top", 2));
        var geometries = ImmutableArray.Create(
            CreateLayer("layer-front"),
            CreateLayer("layer-top"),
            CreateLayer("layer-back"));

        var filter = new ExportFilter();
        var result = filter.Filter(geometries, depthOrder, null);

        Assert.Equal(new[] { "layer-back", "layer-front", "layer-top" }, result.Select(r => r.LayerId));
        Assert.Equal(new[] { 0, 1, 2 }, result.Select(r => r.Depth));
        Assert.True(result.All(item => geometries.Contains(item.Geometry)));
    }

    // Filter_WithSelection_ReturnsRequestedLayersSortedByDepth の挙動を検証します。
    [Fact]
    public void Filter_WithSelection_ReturnsRequestedLayersSortedByDepth()
    {
        var depthOrder = CreateDepthOrder(("layer-back", 0), ("layer-front", 1), ("layer-top", 2));
        var geometries = ImmutableArray.Create(
            CreateLayer("layer-front"),
            CreateLayer("layer-top"),
            CreateLayer("layer-back"));

        var filter = new ExportFilter();
        var selection = new[] { "layer-top", "layer-back" };
        var result = filter.Filter(geometries, depthOrder, selection);

        Assert.Equal(new[] { "layer-back", "layer-top" }, result.Select(r => r.LayerId));
        Assert.Equal(new[] { 0, 2 }, result.Select(r => r.Depth));
    }

    // Filter_WithUnknownLayer_Throws の挙動を検証します。
    [Fact]
    public void Filter_WithUnknownLayer_Throws()
    {
        var depthOrder = CreateDepthOrder(("layer-back", 0), ("layer-front", 1));
        var geometries = ImmutableArray.Create(CreateLayer("layer-back"), CreateLayer("layer-front"));

        var filter = new ExportFilter();

        Assert.Throws<ArgumentException>(() => filter.Filter(geometries, depthOrder, new[] { "layer-missing" }));
    }

    private static LayerPathGeometry CreateLayer(string layerId)
    {
        var outer = CreateRectanglePath(0f, 0f, 4f, 2f);
        return new LayerPathGeometry(layerId, new RgbColor(10, 20, 30), outer, ImmutableArray<PathModel>.Empty);
    }

    private static PathModel CreateRectanglePath(float x, float y, float width, float height)
    {
        var segments = ImmutableArray.Create(
            new PathSegment(PathSegmentType.Move, new[] { new Vector2(x, y) }),
            new PathSegment(PathSegmentType.Line, new[] { new Vector2(x + width, y) }),
            new PathSegment(PathSegmentType.Line, new[] { new Vector2(x + width, y + height) }),
            new PathSegment(PathSegmentType.Line, new[] { new Vector2(x, y + height) }),
            new PathSegment(PathSegmentType.Close, Array.Empty<Vector2>())
        );

        return new PathModel(segments);
    }

    private static DepthOrder CreateDepthOrder(params (string LayerId, int Depth)[] entries)
    {
        var map = new Dictionary<string, int>(entries.Length, StringComparer.Ordinal);
        foreach (var (layerId, depth) in entries)
        {
            map[layerId] = depth;
        }

        return new DepthOrder(map);
    }
}
