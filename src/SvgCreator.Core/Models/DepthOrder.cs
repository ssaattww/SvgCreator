using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace SvgCreator.Core.Models;

/// <summary>
/// Represents a total ordering of layers by depth (0 = farthest).
/// </summary>
public sealed class DepthOrder
{
    public DepthOrder(IDictionary<string, int> depthByLayer)
    {
        ArgumentNullException.ThrowIfNull(depthByLayer);

        if (depthByLayer.Count == 0)
        {
            throw new ArgumentException("Depth order requires at least one layer.", nameof(depthByLayer));
        }

        var builder = ImmutableDictionary.CreateBuilder<string, int>(StringComparer.Ordinal);
        var depths = new HashSet<int>();

        foreach (var (layerId, depth) in depthByLayer)
        {
            if (string.IsNullOrWhiteSpace(layerId))
            {
                throw new ArgumentException("Layer id must be non-empty.", nameof(depthByLayer));
            }

            if (depth < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(depthByLayer), depth, "Depth index must be non-negative.");
            }

            if (!depths.Add(depth))
            {
                throw new ArgumentException("Depth indices must be unique.", nameof(depthByLayer));
            }

            builder[layerId] = depth;
        }

        DepthByLayer = builder.ToImmutable();
    }

    public ImmutableDictionary<string, int> DepthByLayer { get; }

    public int Compare(string leftLayerId, string rightLayerId)
    {
        var left = GetDepth(leftLayerId);
        var right = GetDepth(rightLayerId);
        return left.CompareTo(right);
    }

    public int GetDepth(string layerId)
    {
        if (!DepthByLayer.TryGetValue(layerId, out var depth))
        {
            throw new KeyNotFoundException($"Layer '{layerId}' does not exist in the depth order.");
        }

        return depth;
    }
}
