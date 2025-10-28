using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Numerics;
using System.Xml.Linq;
using SvgCreator.Core.Models;
using SvgCreator.Core.Svg;
using Xunit;
using PathModel = SvgCreator.Core.Models.Path;

namespace SvgCreator.Core.Tests.Output;

public sealed class SvgEmitterTests
{
    private static readonly XNamespace Svg = "http://www.w3.org/2000/svg";

    // 生成された SVG のルート要素が仕様通りの寸法・メタ属性を持つことを確認する。
    [Fact]
    public void EmitDocument_RootContainsExpectedAttributes()
    {
        var image = CreateImage(128, 256);
        var geometry = CreateRectangleGeometry("layer-001", new RgbColor(10, 20, 30));
        var depthOrder = CreateDepthOrder(("layer-001", 0));

        var emitter = new SvgEmitter();
        var svg = emitter.EmitDocument(image, new[] { geometry }, depthOrder);

        var document = XDocument.Parse(svg);
        var root = document.Root ?? throw new InvalidOperationException("SVG root element is missing.");

        Assert.Equal("svg", root.Name.LocalName);
        Assert.Equal(Svg.NamespaceName, root.Name.NamespaceName);
        Assert.Equal("128", root.Attribute("width")?.Value);
        Assert.Equal("256", root.Attribute("height")?.Value);
        Assert.Equal("0 0 128 256", root.Attribute("viewBox")?.Value);
        Assert.Equal("1.1", root.Attribute("version")?.Value);
        Assert.Equal("SvgCreator", root.Attribute("data-generator")?.Value);
    }

    // レイヤーが深度順に `<g>` として並ぶことを確認し、重ね順メタデータの整合性を担保する。
    [Fact]
    public void EmitDocument_ProducesGroupsInDepthOrder()
    {
        var image = CreateImage(16, 16);
        var backGeometry = CreateRectangleGeometry("layer-back", new RgbColor(12, 13, 14));
        var frontGeometry = CreateRectangleGeometry("layer-front", new RgbColor(200, 100, 50));

        var depthOrder = CreateDepthOrder(("layer-back", 0), ("layer-front", 1));

        var emitter = new SvgEmitter();
        var svg = emitter.EmitDocument(image, new[] { frontGeometry, backGeometry }, depthOrder);

        var document = XDocument.Parse(svg);
        var root = document.Root ?? throw new InvalidOperationException("SVG root element is missing.");

        var groups = root.Elements(Svg + "g").ToArray();
        Assert.Equal(2, groups.Length);

        Assert.Equal("layer-back", groups[0].Attribute("id")?.Value);
        Assert.Equal("0", groups[0].Attribute("data-depth")?.Value);
        Assert.Equal("layer-front", groups[1].Attribute("id")?.Value);
        Assert.Equal("1", groups[1].Attribute("data-depth")?.Value);

        var backFill = groups[0].Element(Svg + "path")?.Attribute("fill")?.Value;
        var frontFill = groups[1].Element(Svg + "path")?.Attribute("fill")?.Value;

        Assert.Equal("#0c0d0e", backFill);
        Assert.Equal("#c86432", frontFill);
    }

    // パスコマンドの数値が余分な小数桁を含まずフォーマットされることを確認する。
    [Fact]
    public void EmitDocument_FormatsPathDataWithoutRedundantZeroes()
    {
        var image = CreateImage(32, 32);
        var segments = ImmutableArray.Create(
            new PathSegment(PathSegmentType.Move, new[] { new Vector2(10.5f, 20f) }),
            new PathSegment(PathSegmentType.Line, new[] { new Vector2(30.25f, 40.5f) }),
            new PathSegment(PathSegmentType.CubicBezier, new[]
            {
                new Vector2(31f, 40.25f),
                new Vector2(32.75f, 41.125f),
                new Vector2(33.125f, 42.875f)
            }),
            new PathSegment(PathSegmentType.Close, Array.Empty<Vector2>())
        );

        var geometry = new LayerPathGeometry(
            "layer-curves",
            new RgbColor(80, 90, 100),
            new PathModel(segments),
            ImmutableArray<PathModel>.Empty);

        var depthOrder = CreateDepthOrder(("layer-curves", 0));
        var emitter = new SvgEmitter();
        var svg = emitter.EmitDocument(image, new[] { geometry }, depthOrder);

        var document = XDocument.Parse(svg);
        var path = document.Root!.Element(Svg + "g")!.Element(Svg + "path")!;
        var data = path.Attribute("d")?.Value ?? string.Empty;

        Assert.Contains("M 10.5 20", data);
        Assert.Contains("L 30.25 40.5", data);
        Assert.Contains("C 31 40.25 32.75 41.125 33.125 42.875", data);
        Assert.EndsWith("Z", data, StringComparison.Ordinal);
    }

    // 穴付きレイヤーで `fill-rule="evenodd"` と複数サブパスが出力されることを確認し、ホール描画に対応する。
    [Fact]
    public void EmitDocument_IncludesHoleSubpathsWithEvenOddFillRule()
    {
        var image = CreateImage(64, 64);
        var outer = CreateRectanglePath(0f, 0f, 40f, 40f);
        var hole = CreateRectanglePath(10f, 10f, 20f, 20f);

        var geometry = new LayerPathGeometry(
            "layer-with-hole",
            new RgbColor(25, 26, 27),
            outer,
            ImmutableArray.Create(hole));

        var depthOrder = CreateDepthOrder(("layer-with-hole", 0));

        var emitter = new SvgEmitter();
        var svg = emitter.EmitDocument(image, new[] { geometry }, depthOrder);

        var document = XDocument.Parse(svg);
        var path = document.Root!.Element(Svg + "g")!.Element(Svg + "path")!;

        Assert.Equal("evenodd", path.Attribute("fill-rule")?.Value);
        var data = path.Attribute("d")?.Value ?? string.Empty;

        Assert.Contains("M 0 0", data);
        Assert.Contains("M 10 10", data);
        Assert.True(data.Count(c => c == 'M') >= 2, "Hole subpath was not emitted.");
    }

    private static ImageData CreateImage(int width, int height)
    {
        var buffer = new byte[width * height * (int)PixelFormat.Rgb];
        return new ImageData(width, height, PixelFormat.Rgb, buffer);
    }

    private static LayerPathGeometry CreateRectangleGeometry(string id, RgbColor color)
    {
        var outer = CreateRectanglePath(0f, 0f, 10f, 8f);
        return new LayerPathGeometry(id, color, outer, ImmutableArray<PathModel>.Empty);
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
