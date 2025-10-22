using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace SvgCreator.Core.Models;

/// <summary>
/// レイヤーの前後関係（深度順）を 0（最奥）からの整数で表す全順序です。
/// </summary>
public sealed class DepthOrder
{
    /// <summary>
    /// <see cref="DepthOrder"/> を初期化します。
    /// </summary>
    /// <param name="depthByLayer">レイヤー ID と深度インデックスの対応表。</param>
    /// <exception cref="ArgumentNullException"><paramref name="depthByLayer"/> が <c>null</c> です。</exception>
    /// <exception cref="ArgumentException">レイヤーが 1 つも無い、ID が空白、または深度値が重複しています。</exception>
    /// <exception cref="ArgumentOutOfRangeException">深度値が負です。</exception>
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

    /// <summary>
    /// レイヤー ID と深度インデックスの不変ディクショナリを取得します。
    /// </summary>
    public ImmutableDictionary<string, int> DepthByLayer { get; }

    /// <summary>
    /// 2 つのレイヤーの深度を比較します。
    /// </summary>
    /// <param name="leftLayerId">比較対象 1。</param>
    /// <param name="rightLayerId">比較対象 2。</param>
    /// <returns><paramref name="leftLayerId"/> がより奥なら負、手前なら正、同一なら 0。</returns>
    /// <exception cref="KeyNotFoundException">指定したレイヤー ID が存在しません。</exception>
    public int Compare(string leftLayerId, string rightLayerId)
    {
        var left = GetDepth(leftLayerId);
        var right = GetDepth(rightLayerId);
        return left.CompareTo(right);
    }

    /// <summary>
    /// レイヤーの深度インデックスを取得します。
    /// </summary>
    /// <param name="layerId">対象レイヤー ID。</param>
    /// <returns>0 以上の深度インデックス。</returns>
    /// <exception cref="KeyNotFoundException">指定したレイヤー ID が存在しません。</exception>
    public int GetDepth(string layerId)
    {
        if (!DepthByLayer.TryGetValue(layerId, out var depth))
        {
            throw new KeyNotFoundException($"Layer '{layerId}' does not exist in the depth order.");
        }

        return depth;
    }
}
