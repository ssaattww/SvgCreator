using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Xml.Linq;
using SvgCreator.Core.Models;
using SvgCreator.Core.Svg;
using Xunit;
using PathModel = SvgCreator.Core.Models.Path;

namespace SvgCreator.Core.Tests.Output;

public sealed class SizeLimiterTests
{
    private static readonly XNamespace Svg = "http://www.w3.org/2000/svg";

    // EmitWithLimit_ReturnsSvgWithinThreshold の挙動を検証します。
    [Fact]
    public void EmitWithLimit_ReturnsSvgWithinThreshold()
    {
        var image = CreateImage(32, 32);
        var geometry = CreateGeometry("layer-basic");
        var depthOrder = CreateDepthOrder(("layer-basic", 0));
        var exportLayer = new ExportFilter().Filter(new[] { geometry }, depthOrder, null).Single();

        var limiter = new SizeLimiter(new SvgEmitter());
        var document = limiter.EmitWithLimit(image, depthOrder, exportLayer, maxBytes: 2048);

        var byteCount = Encoding.UTF8.GetByteCount(document.SvgContent);
        Assert.True(byteCount <= 2048, "SVG content exceeds the configured limit.");
        Assert.Equal(3, document.AppliedDecimalPlaces);
    }

    // EmitWithLimit_ReducesPrecisionToSatisfyLimit の挙動を検証します。
    [Fact]
    public void EmitWithLimit_ReducesPrecisionToSatisfyLimit()
    {
        var image = CreateImage(32, 32);
        var geometry = CreateGeometry("layer-precision");
        var depthOrder = CreateDepthOrder(("layer-precision", 1));
        var exportLayer = new ExportFilter().Filter(new[] { geometry }, depthOrder, null).Single();

        var emitter = new SvgEmitter();
        var svg1 = emitter.EmitDocument(image, new[] { geometry }, depthOrder, new SvgEmitterOptions { MaxDecimalPlaces = 1 });

        var limit = Encoding.UTF8.GetByteCount(svg1) - 1; // Force the limiter to fall back to 0 桁。

        var limiter = new SizeLimiter(emitter);
        var document = limiter.EmitWithLimit(image, depthOrder, exportLayer, limit);

        Assert.True(Encoding.UTF8.GetByteCount(document.SvgContent) <= limit);
        Assert.Equal(0, document.AppliedDecimalPlaces);

        var parsed = XDocument.Parse(document.SvgContent);
        var pathData = parsed.Root!.Element(Svg + "g")!.Element(Svg + "path")!.Attribute("d")!.Value;
        Assert.DoesNotContain(".", pathData);
    }

    // EmitWithLimit_WhenCannotReduceFurther_Throws の挙動を検証します。
    [Fact]
    public void EmitWithLimit_WhenCannotReduceFurther_Throws()
    {
        var image = CreateImage(32, 32);
        var geometry = CreateGeometry("layer-tight");
        var depthOrder = CreateDepthOrder(("layer-tight", 2));
        var exportLayer = new ExportFilter().Filter(new[] { geometry }, depthOrder, null).Single();

        var emitter = new SvgEmitter();
        var minimalSvg = emitter.EmitDocument(image, new[] { geometry }, depthOrder, new SvgEmitterOptions { MaxDecimalPlaces = 0 });
        var minimalBytes = Encoding.UTF8.GetByteCount(minimalSvg);

        var limiter = new SizeLimiter(emitter);

        Assert.Throws<InvalidOperationException>(() =>
            limiter.EmitWithLimit(image, depthOrder, exportLayer, minimalBytes - 1));
    }

    private static ImageData CreateImage(int width, int height)
    {
        var buffer = new byte[width * height * (int)PixelFormat.Rgb];
        return new ImageData(width, height, PixelFormat.Rgb, buffer);
    }

    private static LayerPathGeometry CreateGeometry(string layerId)
    {
        var outer = CreatePolygonPath();
        return new LayerPathGeometry(layerId, new RgbColor(220, 120, 60), outer, ImmutableArray<PathModel>.Empty);
    }

    private static PathModel CreatePolygonPath()
    {
        var segments = ImmutableArray.Create(
            new PathSegment(PathSegmentType.Move, new[] { new Vector2(12.3456f, 7.8912f) }),
            new PathSegment(PathSegmentType.Line, new[] { new Vector2(25.5678f, 7.4321f) }),
            new PathSegment(PathSegmentType.Line, new[] { new Vector2(25.5678f, 21.9876f) }),
            new PathSegment(PathSegmentType.CubicBezier, new[]
            {
                new Vector2(20.1234f, 25.6789f),
                new Vector2(15.4321f, 26.7891f),
                new Vector2(12.3456f, 21.9876f)
            }),
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
