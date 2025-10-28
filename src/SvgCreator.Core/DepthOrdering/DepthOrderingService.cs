using System;
using System.Collections.Generic;
using System.Linq;
using SvgCreator.Core.Models;

namespace SvgCreator.Core.DepthOrdering;

/// <summary>
/// 深度順序を計算するサービスを表します。
/// </summary>
public interface IDepthOrderingService
{
    /// <summary>
    /// 指定されたレイヤー集合に対する深度順序を計算します。
    /// </summary>
    /// <param name="layers">深度順序を求めるシェイプレイヤー。</param>
    /// <param name="options">計算に利用するオプション。</param>
    /// <returns>レイヤー ID と深度インデックスの対応。</returns>
    DepthOrder Compute(IReadOnlyList<ShapeLayer> layers, DepthOrderingOptions options);
}

/// <summary>
/// 深度順序計算時のオプションを表します。
/// </summary>
public sealed class DepthOrderingOptions
{
    /// <summary>
    /// デフォルトのデルタ値。
    /// </summary>
    public const float DefaultDelta = 0.05f;

    /// <summary>
    /// ペアのスコア差がこの値以下のときに順位を確定しないための閾値（0..1）。
    /// </summary>
    public float Delta { get; init; } = DefaultDelta;
}

/// <summary>
/// 面積と共有境界に基づいてレイヤーの深度を整序します。
/// </summary>
public sealed class DepthOrderingService : IDepthOrderingService
{
    /// <inheritdoc />
    public DepthOrder Compute(IReadOnlyList<ShapeLayer> layers, DepthOrderingOptions options)
    {
        if (layers is null)
        {
            throw new ArgumentNullException(nameof(layers));
        }

        if (layers.Count == 0)
        {
            throw new ArgumentException("At least one shape layer is required.", nameof(layers));
        }

        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        var delta = float.IsFinite(options.Delta)
            ? Math.Clamp(options.Delta, 0f, 1f)
            : DepthOrderingOptions.DefaultDelta;

        var nodes = layers.Select(layer => new LayerNode(layer)).ToArray();
        var firstMask = nodes[0].Mask;
        var maskWidth = firstMask.Width;
        var maskHeight = firstMask.Height;

        foreach (var node in nodes)
        {
            if (node.Mask.Width != maskWidth || node.Mask.Height != maskHeight)
            {
                throw new ArgumentException("All shape layers must share the same mask dimensions.", nameof(layers));
            }
        }

        var edges = BuildEdges(nodes, maskWidth, maskHeight, delta);
        var components = BuildStronglyConnectedComponents(nodes);
        var orderedComponents = TopologicalSortComponents(components, edges);

        var depthAssignments = new Dictionary<string, int>(nodes.Length, StringComparer.Ordinal);
        var nextDepth = 0;

        foreach (var component in orderedComponents)
        {
            var orderedNodes = component.Nodes
                .OrderByDescending(node => node.Area)
                .ThenBy(node => node.Id, StringComparer.Ordinal)
                .ToArray();

            foreach (var node in orderedNodes)
            {
                depthAssignments[node.Id] = nextDepth++;
            }
        }

        return new DepthOrder(depthAssignments);
    }

    private static List<DirectedEdge> BuildEdges(
        IReadOnlyList<LayerNode> nodes,
        int maskWidth,
        int maskHeight,
        float delta)
    {
        var edges = new List<DirectedEdge>();

        for (var i = 0; i < nodes.Count; i++)
        {
            for (var j = i + 1; j < nodes.Count; j++)
            {
                var left = nodes[i];
                var right = nodes[j];

                var shared = ComputeSharedBoundary(left.Mask, right.Mask, maskWidth, maskHeight);
                if (shared <= 0)
                {
                    continue;
                }

                var areaDiffRatio = ComputeAreaDifferenceRatio(left.Area, right.Area);
                if (areaDiffRatio <= delta)
                {
                    continue;
                }

                var (source, target) = left.Area >= right.Area
                    ? (left, right)
                    : (right, left);

                var edge = new DirectedEdge(source, target, areaDiffRatio, shared);
                source.Outgoing.Add(edge);
                edges.Add(edge);
            }
        }

        return edges;
    }

    private static float ComputeAreaDifferenceRatio(int areaA, int areaB)
    {
        var larger = Math.Max(areaA, areaB);
        if (larger == 0)
        {
            return 0f;
        }

        var diff = Math.Abs(areaA - areaB);
        return diff / (float)larger;
    }

    private static int ComputeSharedBoundary(RasterMask first, RasterMask second, int width, int height)
    {
        var bitsA = first.Bits;
        var bitsB = second.Bits;
        var shared = 0;

        for (var y = 0; y < height; y++)
        {
            var rowOffset = y * width;
            for (var x = 0; x < width; x++)
            {
                if (!bitsA[rowOffset + x])
                {
                    continue;
                }

                if (x > 0 && bitsB[rowOffset + x - 1])
                {
                    shared++;
                }

                if (x + 1 < width && bitsB[rowOffset + x + 1])
                {
                    shared++;
                }

                if (y > 0 && bitsB[rowOffset - width + x])
                {
                    shared++;
                }

                if (y + 1 < height && bitsB[rowOffset + width + x])
                {
                    shared++;
                }
            }
        }

        return shared;
    }

    private static IReadOnlyList<Component> BuildStronglyConnectedComponents(IReadOnlyList<LayerNode> nodes)
    {
        var index = 0;
        var stack = new Stack<LayerNode>();
        var components = new List<Component>();

        foreach (var node in nodes)
        {
            if (node.Index == -1)
            {
                StrongConnect(node, ref index, stack, components);
            }
        }

        return components;
    }

    private static void StrongConnect(
        LayerNode node,
        ref int index,
        Stack<LayerNode> stack,
        List<Component> components)
    {
        node.Index = index;
        node.LowLink = index;
        index++;

        stack.Push(node);
        node.OnStack = true;

        foreach (var edge in node.Outgoing)
        {
            var target = edge.Target;
            if (target.Index == -1)
            {
                StrongConnect(target, ref index, stack, components);
                node.LowLink = Math.Min(node.LowLink, target.LowLink);
            }
            else if (target.OnStack)
            {
                node.LowLink = Math.Min(node.LowLink, target.Index);
            }
        }

        if (node.LowLink != node.Index)
        {
            return;
        }

        var componentNodes = new List<LayerNode>();
        LayerNode current;

        do
        {
            current = stack.Pop();
            current.OnStack = false;
            componentNodes.Add(current);
        }
        while (!ReferenceEquals(current, node));

        var componentId = components.Count;
        var component = new Component(componentId, componentNodes.ToArray());
        components.Add(component);
    }

    private static IReadOnlyList<Component> TopologicalSortComponents(
        IReadOnlyList<Component> components,
        IReadOnlyList<DirectedEdge> edges)
    {
        if (components.Count == 0)
        {
            return Array.Empty<Component>();
        }

        var adjacency = components.ToDictionary(
            component => component.Id,
            component => new HashSet<int>());

        var inDegree = components.ToDictionary(
            component => component.Id,
            component => 0);

        foreach (var edge in edges)
        {
            var sourceComponent = edge.Source.Component!;
            var targetComponent = edge.Target.Component!;

            if (sourceComponent.Id == targetComponent.Id)
            {
                continue;
            }

            if (adjacency[sourceComponent.Id].Add(targetComponent.Id))
            {
                inDegree[targetComponent.Id]++;
            }
        }

        var comparer = Comparer<Component>.Create(static (left, right) =>
        {
            var areaComparison = right.TotalArea.CompareTo(left.TotalArea);
            if (areaComparison != 0)
            {
                return areaComparison;
            }

            return left.Id.CompareTo(right.Id);
        });

        var ready = new SortedSet<Component>(comparer);
        foreach (var component in components)
        {
            if (inDegree[component.Id] == 0)
            {
                ready.Add(component);
            }
        }

        var ordered = new List<Component>(components.Count);

        while (ready.Count > 0)
        {
            var component = ready.Min!;
            ready.Remove(component);
            ordered.Add(component);

            foreach (var targetId in adjacency[component.Id])
            {
                inDegree[targetId]--;
                if (inDegree[targetId] == 0)
                {
                    ready.Add(components[targetId]);
                }
            }
        }

        if (ordered.Count != components.Count)
        {
            // 異常系: 残余ノードがある場合は面積降順で付け足す。
            var remaining = components
                .Except(ordered)
                .OrderByDescending(component => component.TotalArea)
                .ThenBy(component => component.Id)
                .ToArray();

            ordered.AddRange(remaining);
        }

        return ordered;
    }

    private sealed class LayerNode
    {
        public LayerNode(ShapeLayer layer)
        {
            Layer = layer ?? throw new ArgumentNullException(nameof(layer));
            Id = layer.Id;
            Area = layer.Area;
            Mask = layer.Mask;
        }

        public string Id { get; }

        public ShapeLayer Layer { get; }

        public int Area { get; }

        public RasterMask Mask { get; }

        public List<DirectedEdge> Outgoing { get; } = new();

        public int Index { get; set; } = -1;

        public int LowLink { get; set; }

        public bool OnStack { get; set; }

        public Component? Component { get; set; }
    }

    private sealed class DirectedEdge
    {
        public DirectedEdge(LayerNode source, LayerNode target, float score, int sharedBoundary)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Target = target ?? throw new ArgumentNullException(nameof(target));
            Score = score;
            SharedBoundary = sharedBoundary;
        }

        public LayerNode Source { get; }

        public LayerNode Target { get; }

        public float Score { get; }

        public int SharedBoundary { get; }
    }

    private sealed class Component
    {
        public Component(int id, IReadOnlyList<LayerNode> nodes)
        {
            if (nodes is null || nodes.Count == 0)
            {
                throw new ArgumentException("Component requires at least one node.", nameof(nodes));
            }

            Id = id;
            Nodes = nodes;
            TotalArea = 0;

            foreach (var node in nodes)
            {
                node.Component = this;
                TotalArea += node.Area;
            }
        }

        public int Id { get; }

        public IReadOnlyList<LayerNode> Nodes { get; }

        public int TotalArea { get; }
    }
}
