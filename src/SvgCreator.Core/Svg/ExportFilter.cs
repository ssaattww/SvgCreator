using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using SvgCreator.Core.Models;

namespace SvgCreator.Core.Svg;

/// <summary>
/// レイヤー出力対象を `--export-layer` オプションに基づいて選択します。
/// </summary>
public sealed class ExportFilter
{
    /// <summary>
    /// エクスポート対象のレイヤーをフィルタリングします。
    /// </summary>
    /// <param name="layers">Bézier 近似済みのレイヤー集合。</param>
    /// <param name="depthOrder">レイヤーの深度順序。</param>
    /// <param name="selectedLayerIds">エクスポート対象のレイヤー ID。指定が無い場合は全レイヤー。</param>
    /// <returns>深度順にソートされたエクスポート対象レイヤー。</returns>
    /// <exception cref="ArgumentNullException">必須引数が <c>null</c> の場合。</exception>
    /// <exception cref="ArgumentException">レイヤー集合が空、重複、未定義の ID を含む場合。</exception>
    public ImmutableArray<LayerExportItem> Filter(
        IReadOnlyList<LayerPathGeometry> layers,
        DepthOrder depthOrder,
        IReadOnlyCollection<string>? selectedLayerIds)
    {
        ArgumentNullException.ThrowIfNull(layers);
        ArgumentNullException.ThrowIfNull(depthOrder);

        if (layers.Count == 0)
        {
            throw new ArgumentException("At least one layer geometry must be provided.", nameof(layers));
        }

        var geometryById = new Dictionary<string, LayerPathGeometry>(layers.Count, StringComparer.Ordinal);

        foreach (var geometry in layers)
        {
            if (geometry is null)
            {
                throw new ArgumentException("Layers cannot contain null entries.", nameof(layers));
            }

            if (!geometryById.TryAdd(geometry.LayerId, geometry))
            {
                throw new ArgumentException($"Duplicate layer id '{geometry.LayerId}' encountered.", nameof(layers));
            }
        }

        List<string>? requested = null;
        if (selectedLayerIds is { Count: > 0 })
        {
            requested = new List<string>(selectedLayerIds.Count);
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var id in selectedLayerIds)
            {
                if (string.IsNullOrWhiteSpace(id))
                {
                    throw new ArgumentException("Selected layer ids must be non-empty.", nameof(selectedLayerIds));
                }

                if (!seen.Add(id))
                {
                    throw new ArgumentException($"Layer id '{id}' is specified multiple times.", nameof(selectedLayerIds));
                }

                requested.Add(id);
            }
        }

        var capacity = requested?.Count ?? geometryById.Count;
        var builder = ImmutableArray.CreateBuilder<LayerExportItem>(capacity);
        IEnumerable<string> ids = requested is not null ? requested : geometryById.Keys;

        foreach (var id in ids)
        {
            if (!geometryById.TryGetValue(id, out var geometry))
            {
                throw new ArgumentException($"Layer '{id}' is not available for export.", requested is null ? nameof(layers) : nameof(selectedLayerIds));
            }

            int depth;
            try
            {
                depth = depthOrder.GetDepth(id);
            }
            catch (KeyNotFoundException ex)
            {
                throw new ArgumentException($"Depth order does not contain layer '{id}'.", nameof(depthOrder), ex);
            }

            builder.Add(new LayerExportItem(geometry, depth));
        }

        builder.Sort(static (left, right) =>
        {
            var comparison = left.Depth.CompareTo(right.Depth);
            return comparison != 0 ? comparison : string.CompareOrdinal(left.LayerId, right.LayerId);
        });

        return builder.MoveToImmutable();
    }
}

/// <summary>
/// エクスポート対象レイヤーとその深度メタデータを表します。
/// </summary>
public sealed class LayerExportItem
{
    /// <summary>
    /// <see cref="LayerExportItem"/> を初期化します。
    /// </summary>
    /// <param name="geometry">エクスポート対象のパス幾何。</param>
    /// <param name="depth">深度インデックス。</param>
    public LayerExportItem(LayerPathGeometry geometry, int depth)
    {
        Geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
        Depth = depth;
    }

    /// <summary>
    /// レイヤー ID を取得します。
    /// </summary>
    public string LayerId => Geometry.LayerId;

    /// <summary>
    /// ベクタ化済みパス幾何を取得します。
    /// </summary>
    public LayerPathGeometry Geometry { get; }

    /// <summary>
    /// 深度インデックス（0 が最奥）を取得します。
    /// </summary>
    public int Depth { get; }
}
