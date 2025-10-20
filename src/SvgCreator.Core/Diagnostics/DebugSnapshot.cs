using System;
using System.Collections.Generic;
using System.Linq;
using SvgCreator.Core.Models;

namespace SvgCreator.Core.Diagnostics;

/// <summary>
/// デバッグ用に保存するパイプラインのスナップショットを表します。
/// </summary>
public sealed class DebugSnapshot
{
    /// <summary>
    /// スナップショットの現在バージョン。
    /// </summary>
    public const string CurrentVersion = "1.0";

    /// <summary>
    /// JSON 形式に埋め込むバージョン文字列。
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    /// 入力画像に関する情報。
    /// </summary>
    public required DebugSnapshotImage Image { get; init; }

    /// <summary>
    /// 減色後のカラーパレット。
    /// </summary>
    public required IReadOnlyList<RgbColor> Palette { get; init; }

    /// <summary>
    /// 生成されたレイヤー群。
    /// </summary>
    public required IReadOnlyList<DebugSnapshotLayer> Layers { get; init; }

    /// <summary>
    /// <see cref="QuantizationResult"/> とレイヤー集合からスナップショットを構築します。
    /// </summary>
    /// <param name="result">減色結果。</param>
    /// <param name="layers">生成済みレイヤー。</param>
    /// <returns>構築済みスナップショット。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> または <paramref name="layers"/> が <c>null</c>。</exception>
    public static DebugSnapshot From(QuantizationResult result, IEnumerable<ShapeLayer> layers)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(layers);

        var layerList = layers.ToList();

        return new DebugSnapshot
        {
            Version = CurrentVersion,
            Image = new DebugSnapshotImage
            {
                Width = result.Image.Width,
                Height = result.Image.Height,
                Format = result.Image.Format,
                Pixels = result.Image.Pixels.ToArray()
            },
            Palette = result.Palette.ToArray(),
            Layers = layerList.Select(DebugSnapshotLayer.From).ToArray()
        };
    }
}
