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
    /// <inheritdoc />
    public Task<IReadOnlyList<ShapeLayer>> BuildLayersAsync(QuantizationResult quantization, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(quantization);

        var width = quantization.Image.Width;
        var height = quantization.Image.Height;
        var labels = quantization.LabelIndices;
        var palette = quantization.Palette;

        var totalPixels = labels.Length;
        var visited = new bool[totalPixels];
        var components = new List<ShapeLayer>();

        // BFS で量子化ラベルごとに連結成分を抽出するためのワークスペース。
        var queue = new Queue<int>();
        var tempPixels = new List<int>();
        var id = 1;

        for (var index = 0; index < totalPixels; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (visited[index])
            {
                continue;
            }

            tempPixels.Clear();
            queue.Clear();

            var label = labels[index];
            queue.Enqueue(index);
            visited[index] = true;

            var minX = int.MaxValue;
            var minY = int.MaxValue;
            var maxX = int.MinValue;
            var maxY = int.MinValue;

            while (queue.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var current = queue.Dequeue();
                tempPixels.Add(current);

                var x = current % width;
                var y = current / width;

                if (x < minX) minX = x;
                if (y < minY) minY = y;
                if (x > maxX) maxX = x;
                if (y > maxY) maxY = y;

                EnqueueIfMatches(x - 1, y);
                EnqueueIfMatches(x + 1, y);
                EnqueueIfMatches(x, y - 1);
                EnqueueIfMatches(x, y + 1);
            }

            if (tempPixels.Count == 0)
            {
                continue;
            }

            // 連結成分の画素を boolean マスクに反映する。
            var maskBits = new bool[totalPixels];
            foreach (var pixelIndex in tempPixels)
            {
                maskBits[pixelIndex] = true;
            }

            var mask = new RasterMask(width, height, ImmutableArray.CreateRange(maskBits));

            // 画素集合から境界輪郭（CCW）を抽出する。
            var boundary = TraceBoundary(tempPixels, width, height);

            var color = palette[label];
            var shapeLayer = new ShapeLayer(
                id: $"layer-{id:0000}",
                color: color,
                mask: mask,
                boundary: boundary,
                holes: ImmutableArray<IImmutableList<Vector2>>.Empty,
                area: tempPixels.Count);

            components.Add(shapeLayer);
            id++;

            void EnqueueIfMatches(int nx, int ny)
            {
                if (nx < 0 || ny < 0 || nx >= width || ny >= height)
                {
                    return;
                }

                var neighborIndex = ny * width + nx;
                if (!visited[neighborIndex] && labels[neighborIndex] == label)
                {
                    visited[neighborIndex] = true;
                    queue.Enqueue(neighborIndex);
                }
            }
        }

        return Task.FromResult<IReadOnlyList<ShapeLayer>>(components.ToArray());
    }

    private static ImmutableArray<Vector2> TraceBoundary(IReadOnlyList<int> componentPixels, int width, int height)
    {
        if (componentPixels.Count == 0)
        {
            throw new ArgumentException("Component must contain at least one pixel.", nameof(componentPixels));
        }

        var pixelSet = new HashSet<int>(componentPixels);
        var edges = new Dictionary<(int X, int Y), List<DirectedEdge>>();

        foreach (var pixelIndex in componentPixels)
        {
            var px = pixelIndex % width;
            var py = pixelIndex / width;

            if (!HasPixel(px, py - 1))
            {
                AddEdge(edges, (px, py), (px + 1, py));
            }

            if (!HasPixel(px + 1, py))
            {
                AddEdge(edges, (px + 1, py), (px + 1, py + 1));
            }

            if (!HasPixel(px, py + 1))
            {
                AddEdge(edges, (px + 1, py + 1), (px, py + 1));
            }

            if (!HasPixel(px - 1, py))
            {
                AddEdge(edges, (px, py + 1), (px, py));
            }
        }

        if (edges.Count == 0)
        {
            throw new InvalidOperationException("Component boundary could not be constructed.");
        }

        var start = edges.Keys
            .OrderBy(v => v.Y)
            .ThenBy(v => v.X)
            .First();

        var totalEdges = edges.Values.Sum(list => list.Count);
        if (totalEdges == 0)
        {
            throw new InvalidOperationException("Component boundary is empty.");
        }

        var boundary = new List<Vector2>(capacity: Math.Max(4, totalEdges));
        var visited = new HashSet<VisitedEdge>();

        boundary.Add(new Vector2(start.X, start.Y));

        if (!TrySelectNextEdge(edges, visited, start, previousDirection: null, isFirstStep: true, out var current, out var currentDirection))
        {
            throw new InvalidOperationException("Failed to locate initial boundary edge.");
        }

        if (current != start)
        {
            boundary.Add(new Vector2(current.X, current.Y));
        }

        var steps = 1;

        while (current != start)
        {
            if (steps > totalEdges)
            {
                throw new InvalidOperationException("Boundary tracing did not close after visiting all edges.");
            }

            if (!TrySelectNextEdge(edges, visited, current, currentDirection, isFirstStep: false, out var next, out var nextDirection))
            {
                throw new InvalidOperationException("Failed to advance boundary tracing.");
            }

            current = next;
            currentDirection = nextDirection;
            if (current != start)
            {
                boundary.Add(new Vector2(current.X, current.Y));
            }

            steps++;
        }

        RemoveCollinearVertices(boundary);

        if (boundary.Count < 3)
        {
            throw new InvalidOperationException("Boundary must include at least three vertices.");
        }

        return ImmutableArray.CreateRange(boundary);

        bool HasPixel(int x, int y)
        {
            if (x < 0 || y < 0 || x >= width || y >= height)
            {
                return false;
            }

            return pixelSet.Contains(y * width + x);
        }
    }

    private static void AddEdge(Dictionary<(int X, int Y), List<DirectedEdge>> edges, (int X, int Y) from, (int X, int Y) to)
    {
        var direction = GetDirection(from, to);
        if (!edges.TryGetValue(from, out var list))
        {
            list = new List<DirectedEdge>();
            edges[from] = list;
        }

        list.Add(new DirectedEdge(from, to, direction));
        edges.TryAdd(to, new List<DirectedEdge>());
    }

    private static bool TrySelectNextEdge(
        IReadOnlyDictionary<(int X, int Y), List<DirectedEdge>> edges,
        HashSet<VisitedEdge> visited,
        (int X, int Y) current,
        Direction? previousDirection,
        bool isFirstStep,
        out (int X, int Y) next,
        out Direction direction)
    {
        next = default;
        direction = default;

        if (!edges.TryGetValue(current, out var outgoing) || outgoing.Count == 0)
        {
            return false;
        }

        ReadOnlySpan<Direction> preference = isFirstStep
            ? stackalloc Direction[] { Direction.East, Direction.South, Direction.West, Direction.North }
            : stackalloc Direction[]
            {
                TurnLeft(previousDirection!.Value),
                previousDirection.Value,
                TurnRight(previousDirection.Value),
                TurnBack(previousDirection.Value)
            };

        foreach (var candidate in preference)
        {
            foreach (var edge in outgoing)
            {
                if (edge.Direction != candidate)
                {
                    continue;
                }

                if (!visited.Add(new VisitedEdge(current, candidate)))
                {
                    continue;
                }

                next = edge.To;
                direction = edge.Direction;
                return true;
            }
        }

        return false;
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

    private readonly record struct VisitedEdge((int X, int Y) From, Direction Direction);

    private enum Direction
    {
        East = 0,
        South = 1,
        West = 2,
        North = 3
    }
}
