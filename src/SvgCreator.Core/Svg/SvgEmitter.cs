using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Xml.Linq;
using SvgCreator.Core.Models;
using PathModel = SvgCreator.Core.Models.Path;

namespace SvgCreator.Core.Svg;

/// <summary>
/// レイヤーのパス幾何から SVG マークアップを生成します。
/// </summary>
public sealed class SvgEmitter
{
    private static readonly XNamespace SvgNamespace = "http://www.w3.org/2000/svg";

    /// <summary>
    /// SVG ドキュメントを生成します。
    /// </summary>
    /// <param name="image">出力 SVG の寸法基準となる画像情報。</param>
    /// <param name="layers">描画対象レイヤーのベクタ化結果。</param>
    /// <param name="depthOrder">レイヤーの深度順。</param>
    /// <param name="options">出力書式設定。</param>
    /// <returns>SVG マークアップ。</returns>
    /// <exception cref="ArgumentNullException">必須引数が <c>null</c> の場合。</exception>
    /// <exception cref="ArgumentException">レイヤー集合が空、または重複 ID を含む場合。</exception>
    public string EmitDocument(
        ImageData image,
        IReadOnlyList<LayerPathGeometry> layers,
        DepthOrder depthOrder,
        SvgEmitterOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(layers);
        ArgumentNullException.ThrowIfNull(depthOrder);

        if (layers.Count == 0)
        {
            throw new ArgumentException("At least one layer geometry must be provided.", nameof(layers));
        }

        var normalizedOptions = NormalizeOptions(options);
        var orderedLayers = OrderLayers(layers, depthOrder);

        var width = image.Width.ToString(CultureInfo.InvariantCulture);
        var height = image.Height.ToString(CultureInfo.InvariantCulture);

        var root = new XElement(
            SvgNamespace + "svg",
            new XAttribute("width", width),
            new XAttribute("height", height),
            new XAttribute("viewBox", $"0 0 {width} {height}"),
            new XAttribute("version", "1.1"));

        if (!string.IsNullOrEmpty(normalizedOptions.GeneratorName))
        {
            root.SetAttributeValue("data-generator", normalizedOptions.GeneratorName);
        }

        foreach (var (geometry, depth) in orderedLayers)
        {
            var group = new XElement(
                SvgNamespace + "g",
                new XAttribute("id", geometry.LayerId),
                new XAttribute("data-depth", depth.ToString(CultureInfo.InvariantCulture)));

            var pathData = BuildPathData(geometry, normalizedOptions);
            var pathElement = new XElement(
                SvgNamespace + "path",
                new XAttribute("fill", FormatColor(geometry.Color)),
                new XAttribute("d", pathData));

            if (!geometry.HolePaths.IsDefaultOrEmpty)
            {
                pathElement.SetAttributeValue("fill-rule", "evenodd");
            }

            group.Add(pathElement);
            root.Add(group);
        }

        var document = new XDocument(new XDeclaration("1.0", "utf-8", "yes"), root);
        using var writer = new System.IO.StringWriter(CultureInfo.InvariantCulture);
        document.Save(writer, SaveOptions.DisableFormatting);
        return writer.ToString();
    }

    private static SvgEmitterOptions NormalizeOptions(SvgEmitterOptions? options)
    {
        var source = options ?? SvgEmitterOptions.Default;
        var decimals = source.MaxDecimalPlaces < 0 ? SvgEmitterOptions.Default.MaxDecimalPlaces : source.MaxDecimalPlaces;
        decimals = Math.Min(decimals, SvgEmitterOptions.MaximumSupportedDecimalPlaces);

        var generator = string.IsNullOrWhiteSpace(source.GeneratorName)
            ? SvgEmitterOptions.Default.GeneratorName
            : source.GeneratorName.Trim();

        return new SvgEmitterOptions
        {
            MaxDecimalPlaces = decimals,
            GeneratorName = generator
        };
    }

    private static IReadOnlyList<(LayerPathGeometry Geometry, int Depth)> OrderLayers(
        IReadOnlyList<LayerPathGeometry> layers,
        DepthOrder depthOrder)
    {
        var ordered = new List<(LayerPathGeometry Geometry, int Depth)>(layers.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var geometry in layers)
        {
            if (geometry is null)
            {
                throw new ArgumentException("Layers cannot contain null entries.", nameof(layers));
            }

            if (!seen.Add(geometry.LayerId))
            {
                throw new ArgumentException($"Duplicate layer id '{geometry.LayerId}' detected.", nameof(layers));
            }

            int depth;
            try
            {
                depth = depthOrder.GetDepth(geometry.LayerId);
            }
            catch (KeyNotFoundException ex)
            {
                throw new ArgumentException($"Depth order does not contain layer '{geometry.LayerId}'.", nameof(depthOrder), ex);
            }

            ordered.Add((geometry, depth));
        }

        ordered.Sort(static (left, right) =>
        {
            var comparison = left.Depth.CompareTo(right.Depth);
            return comparison != 0 ? comparison : string.CompareOrdinal(left.Geometry.LayerId, right.Geometry.LayerId);
        });

        return ordered;
    }

    private static string BuildPathData(LayerPathGeometry geometry, SvgEmitterOptions options)
    {
        var builder = new StringBuilder();
        AppendPath(builder, geometry.OuterPath, options);

        if (!geometry.HolePaths.IsDefaultOrEmpty)
        {
            foreach (var hole in geometry.HolePaths)
            {
                if (hole is null)
                {
                    throw new ArgumentException("Hole paths cannot contain null entries.", nameof(geometry));
                }

                AppendPath(builder, hole, options);
            }
        }

        return builder.ToString();
    }

    private static void AppendPath(StringBuilder builder, PathModel path, SvgEmitterOptions options)
    {
        if (path is null)
        {
            throw new ArgumentException("Path cannot be null.", nameof(path));
        }

        foreach (var segment in path.Segments)
        {
            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append(BuildSegment(segment, options));
        }
    }

    private static string BuildSegment(PathSegment segment, SvgEmitterOptions options)
    {
        return segment.Type switch
        {
            PathSegmentType.Move => BuildCommand('M', segment.Points, options),
            PathSegmentType.Line => BuildCommand('L', segment.Points, options),
            PathSegmentType.CubicBezier => BuildCommand('C', segment.Points, options),
            PathSegmentType.QuadraticBezier => BuildCommand('Q', segment.Points, options),
            PathSegmentType.Close => "Z",
            _ => throw new NotSupportedException($"Unsupported segment type '{segment.Type}'.")
        };
    }

    private static string BuildCommand(char command, ImmutableArray<Vector2> points, SvgEmitterOptions options)
    {
        var builder = new StringBuilder();
        builder.Append(command);

        foreach (var point in points)
        {
            builder.Append(' ');
            builder.Append(FormatNumber(point.X, options));
            builder.Append(' ');
            builder.Append(FormatNumber(point.Y, options));
        }

        return builder.ToString();
    }

    private static string FormatNumber(float value, SvgEmitterOptions options)
    {
        var adjusted = Math.Abs(value) < 1e-6f ? 0f : value;
        var format = options.MaxDecimalPlaces == 0
            ? "0"
            : $"0.{new string('#', options.MaxDecimalPlaces)}";

        return adjusted.ToString(format, CultureInfo.InvariantCulture);
    }

    private static string FormatColor(RgbColor color) => $"#{color.R:x2}{color.G:x2}{color.B:x2}";
}

/// <summary>
/// SVG 出力時の書式設定です。
/// </summary>
public sealed class SvgEmitterOptions
{
    internal const int MaximumSupportedDecimalPlaces = 6;

    /// <summary>
    /// 既定の書式設定を取得します。
    /// </summary>
    public static SvgEmitterOptions Default { get; } = new();

    /// <summary>
    /// 座標出力時に保持する最大小数桁数を取得または設定します。
    /// </summary>
    public int MaxDecimalPlaces { get; init; } = 3;

    /// <summary>
    /// 生成元メタデータとして <c>data-generator</c> 属性に出力する文字列を取得または設定します。
    /// </summary>
    public string GeneratorName { get; init; } = "SvgCreator";
}
