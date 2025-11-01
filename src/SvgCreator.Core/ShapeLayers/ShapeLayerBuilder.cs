using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using SvgCreator.Core.Models;
using SvgCreator.Core.Orchestration;

namespace SvgCreator.Core.ShapeLayers;

/// <summary>
/// 量子化済み画像からシェイプレイヤーを抽出します。
/// </summary>
public sealed class ShapeLayerBuilder : IShapeLayerBuilder
{
    private readonly ShapeLayerBuilderOptions _options;

    /// <summary>
    /// 新しい <see cref="ShapeLayerBuilder"/> を初期化します。
    /// </summary>
    /// <param name="options">抽出時のオプション。</param>
    public ShapeLayerBuilder(ShapeLayerBuilderOptions? options = null)
    {
        _options = options ?? ShapeLayerBuilderOptions.Default;
        _options.Validate();
    }

    /// <inheritdoc />
    public Task<ShapeLayerExtractionResult> BuildLayersAsync(QuantizationResult quantization, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(quantization);

        var width = quantization.Image.Width;
        var height = quantization.Image.Height;
        var labels = quantization.LabelIndices;
        var palette = quantization.Palette;

        var totalPixels = labels.Length;
        var visited = new bool[totalPixels];
        var shapeLayers = new List<ShapeLayer>();
        var noisyLayers = new List<NoisyLayer>();

        var queue = new Queue<int>();
        var componentPixels = new List<int>();
        var shapeId = 1;
        var noiseId = 1;

        for (var index = 0; index < totalPixels; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (visited[index])
            {
                continue;
            }

            componentPixels.Clear();
            queue.Clear();

            var label = labels[index];
            queue.Enqueue(index);
            visited[index] = true;

            while (queue.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var current = queue.Dequeue();
                componentPixels.Add(current);

                var x = current % width;
                var y = current / width;

                EnqueueIfMatches(x - 1, y, label);
                EnqueueIfMatches(x + 1, y, label);
                EnqueueIfMatches(x, y - 1, label);
                EnqueueIfMatches(x, y + 1, label);
            }

            if (componentPixels.Count == 0)
            {
                continue;
            }

            var maskBits = new bool[totalPixels];
            foreach (var pixelIndex in componentPixels)
            {
                maskBits[pixelIndex] = true;
            }

            var mask = new RasterMask(width, height, ImmutableArray.CreateRange(maskBits));
            var contours = ExtractContours(componentPixels, width, height);
            var boundary = contours.Boundary;
            var perimeter = ComputePerimeter(boundary);

            var area = componentPixels.Count;
            var color = palette[label];

            if (IsNoisy(area, perimeter))
            {
                var noisyLayer = new NoisyLayer(
                    id: $"noise-{noiseId:0000}",
                    color: color,
                    mask: mask,
                    boundary: boundary,
                    area: area);

                noisyLayers.Add(noisyLayer);
                noiseId++;
            }
            else
            {
                var holes = contours.Holes
                    .Select(h => (IImmutableList<Vector2>)h)
                    .ToImmutableArray();

                var shapeLayer = new ShapeLayer(
                    id: $"layer-{shapeId:0000}",
                    color: color,
                    mask: mask,
                    boundary: boundary,
                    holes: holes,
                    area: area);

                shapeLayers.Add(shapeLayer);
                shapeId++;
            }
        }

        if (_options.MaxPrimaryLayerCount > 0 && shapeLayers.Count > _options.MaxPrimaryLayerCount)
        {
            var keepSet = shapeLayers
                .OrderByDescending(layer => layer.Area)
                .ThenBy(layer => layer.Id, StringComparer.Ordinal)
                .Take(_options.MaxPrimaryLayerCount)
                .Select(layer => layer.Id)
                .ToHashSet(StringComparer.Ordinal);

            var demotedLayers = shapeLayers
                .Where(layer => !keepSet.Contains(layer.Id))
                .OrderBy(layer => layer.Area)
                .ThenBy(layer => layer.Id, StringComparer.Ordinal)
                .ToList();

            shapeLayers.RemoveAll(layer => !keepSet.Contains(layer.Id));

            foreach (var demoted in demotedLayers)
            {
                var noiseLayer = new NoisyLayer(
                    id: $"noise-{noiseId:0000}",
                    color: demoted.Color,
                    mask: demoted.Mask,
                    boundary: demoted.Boundary,
                    area: demoted.Area);

                noiseId++;
                noisyLayers.Add(noiseLayer);
            }
        }

        return Task.FromResult(new ShapeLayerExtractionResult(shapeLayers.ToArray(), noisyLayers.ToArray()));

        void EnqueueIfMatches(int nx, int ny, int targetLabel)
        {
            if ((uint)nx >= (uint)width || (uint)ny >= (uint)height)
            {
                return;
            }

            var neighborIndex = ny * width + nx;
            if (!visited[neighborIndex] && labels[neighborIndex] == targetLabel)
            {
                visited[neighborIndex] = true;
                queue.Enqueue(neighborIndex);
            }
        }
    }

    private bool IsNoisy(int area, float perimeter)
    {
        if (_options.NoisyComponentMinimumPixelCount > 0 && area < _options.NoisyComponentMinimumPixelCount)
        {
            return true;
        }

        if (_options.NoisyComponentMinimumPerimeter > 0 && perimeter < _options.NoisyComponentMinimumPerimeter)
        {
            return true;
        }

        return false;
    }

    private static ContourSet ExtractContours(IReadOnlyList<int> componentPixels, int width, int height)
    {
        if (componentPixels.Count == 0)
        {
            throw new ArgumentException("Component must contain at least one pixel.", nameof(componentPixels));
        }

        var pixelSet = new HashSet<int>(componentPixels);
        var edgesByStart = new Dictionary<(int X, int Y), List<DirectedEdge>>();
        var allEdges = new List<DirectedEdge>();

        foreach (var pixelIndex in componentPixels)
        {
            var px = pixelIndex % width;
            var py = pixelIndex / width;

            if (!HasPixel(px, py - 1))
            {
                AddEdge(edgesByStart, allEdges, (px, py), (px + 1, py));
            }

            if (!HasPixel(px + 1, py))
            {
                AddEdge(edgesByStart, allEdges, (px + 1, py), (px + 1, py + 1));
            }

            if (!HasPixel(px, py + 1))
            {
                AddEdge(edgesByStart, allEdges, (px + 1, py + 1), (px, py + 1));
            }

            if (!HasPixel(px - 1, py))
            {
                AddEdge(edgesByStart, allEdges, (px, py + 1), (px, py));
            }
        }

        if (allEdges.Count == 0)
        {
            throw new InvalidOperationException("Component boundary could not be constructed.");
        }

        var orderedEdges = allEdges
            .OrderBy(edge => edge.From.Y)
            .ThenBy(edge => edge.From.X)
            .ThenBy(edge => edge.Direction)
            .ToArray();

        var visited = new HashSet<DirectedEdge>();
        List<Vector2>? outerBoundary = null;
        float outerArea = 0;
        var holes = new List<ImmutableArray<Vector2>>();

        foreach (var startEdge in orderedEdges)
        {
            if (!visited.Add(startEdge))
            {
                continue;
            }

            var loop = TraceLoop(startEdge, edgesByStart, visited);
            RemoveCollinearVertices(loop);

            if (loop.Count < 3)
            {
                throw new InvalidOperationException("Boundary loop must contain at least three vertices.");
            }

            var signedArea = ComputeSignedArea(loop);

            if (signedArea > 0)
            {
                if (outerBoundary is null || signedArea > outerArea)
                {
                    outerBoundary = loop;
                    outerArea = signedArea;
                }
            }
            else
            {
                loop.Reverse();
                holes.Add(ImmutableArray.CreateRange(loop));
            }
        }

        if (outerBoundary is null)
        {
            throw new InvalidOperationException("No outer boundary detected for component.");
        }

        var boundary = ImmutableArray.CreateRange(outerBoundary);
        return new ContourSet(boundary, holes.ToImmutableArray());

        bool HasPixel(int x, int y)
        {
            if ((uint)x >= (uint)width || (uint)y >= (uint)height)
            {
                return false;
            }

            return pixelSet.Contains(y * width + x);
        }
    }

    private static List<Vector2> TraceLoop(
        DirectedEdge startEdge,
        IReadOnlyDictionary<(int X, int Y), List<DirectedEdge>> edgesByStart,
        HashSet<DirectedEdge> visited)
    {
        var loop = new List<Vector2>();
        var startVertex = startEdge.From;
        var currentVertex = startEdge.To;
        var previousDirection = startEdge.Direction;

        loop.Add(new Vector2(startVertex.X, startVertex.Y));
        if (currentVertex != startVertex)
        {
            loop.Add(new Vector2(currentVertex.X, currentVertex.Y));
        }

        while (!currentVertex.Equals(startVertex))
        {
            var nextEdge = SelectNextEdge(currentVertex, previousDirection, edgesByStart, visited);
            visited.Add(nextEdge);

            currentVertex = nextEdge.To;
            if (!currentVertex.Equals(startVertex))
            {
                loop.Add(new Vector2(currentVertex.X, currentVertex.Y));
            }

            previousDirection = nextEdge.Direction;
        }

        return loop;
    }

    private static DirectedEdge SelectNextEdge(
        (int X, int Y) current,
        Direction previousDirection,
        IReadOnlyDictionary<(int X, int Y), List<DirectedEdge>> edgesByStart,
        HashSet<DirectedEdge> visited)
    {
        if (!edgesByStart.TryGetValue(current, out var outgoing) || outgoing.Count == 0)
        {
            throw new InvalidOperationException("Boundary traversal encountered a dangling vertex.");
        }

        ReadOnlySpan<Direction> preference = stackalloc Direction[]
        {
            TurnLeft(previousDirection),
            previousDirection,
            TurnRight(previousDirection),
            TurnBack(previousDirection)
        };

        foreach (var candidate in preference)
        {
            foreach (var edge in outgoing)
            {
                if (edge.Direction != candidate || visited.Contains(edge))
                {
                    continue;
                }

                return edge;
            }
        }

        throw new InvalidOperationException("Failed to locate next boundary edge.");
    }

    private static void AddEdge(
        Dictionary<(int X, int Y), List<DirectedEdge>> edgesByStart,
        List<DirectedEdge> allEdges,
        (int X, int Y) from,
        (int X, int Y) to)
    {
        var direction = GetDirection(from, to);
        var edge = new DirectedEdge(from, to, direction);

        if (!edgesByStart.TryGetValue(from, out var list))
        {
            list = new List<DirectedEdge>();
            edgesByStart[from] = list;
        }

        list.Add(edge);
        edgesByStart.TryAdd(to, new List<DirectedEdge>());
        allEdges.Add(edge);
    }

    private static float ComputePerimeter(ImmutableArray<Vector2> boundary)
    {
        var perimeter = 0f;

        for (var i = 0; i < boundary.Length; i++)
        {
            var current = boundary[i];
            var next = boundary[(i + 1) % boundary.Length];
            perimeter += Vector2.Distance(current, next);
        }

        return perimeter;
    }

    private static float ComputeSignedArea(IReadOnlyList<Vector2> polygon)
    {
        var area = 0f;

        for (var i = 0; i < polygon.Count; i++)
        {
            var current = polygon[i];
            var next = polygon[(i + 1) % polygon.Count];
            area += (current.X * next.Y) - (next.X * current.Y);
        }

        return area * 0.5f;
    }

    private static Direction GetDirection((int X, int Y) from, (int X, int Y) to)
    {
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;

        return (dx, dy) switch
        {
            (1, 0) => Direction.East,
            (0, 1) => Direction.South,
            (-1, 0) => Direction.West,
            (0, -1) => Direction.North,
            _ => throw new InvalidOperationException("Unexpected edge direction while tracing boundary.")
        };
    }

    private static Direction TurnLeft(Direction direction) => (Direction)(((int)direction + 3) & 3);

    private static Direction TurnRight(Direction direction) => (Direction)(((int)direction + 1) & 3);

    private static Direction TurnBack(Direction direction) => (Direction)(((int)direction + 2) & 3);

    private static void RemoveCollinearVertices(List<Vector2> boundary)
    {
        if (boundary.Count <= 3)
        {
            return;
        }

        var count = boundary.Count;
        var result = new List<Vector2>(count);

        for (var i = 0; i < count; i++)
        {
            var prev = boundary[(i - 1 + count) % count];
            var current = boundary[i];
            var next = boundary[(i + 1) % count];

            var v1 = current - prev;
            var v2 = next - current;

            var cross = (v1.X * v2.Y) - (v1.Y * v2.X);
            var dot = (v1.X * v2.X) + (v1.Y * v2.Y);

            if (Math.Abs(cross) <= 1e-6f && dot > 0)
            {
                continue;
            }

            if (result.Count == 0 || result[^1] != current)
            {
                result.Add(current);
            }
        }

        if (result.Count >= 3)
        {
            boundary.Clear();
            boundary.AddRange(result);
        }
    }

    private readonly record struct DirectedEdge((int X, int Y) From, (int X, int Y) To, Direction Direction);

    private readonly record struct ContourSet(ImmutableArray<Vector2> Boundary, ImmutableArray<ImmutableArray<Vector2>> Holes);

    private enum Direction
    {
        East = 0,
        South = 1,
        West = 2,
        North = 3
    }
}
