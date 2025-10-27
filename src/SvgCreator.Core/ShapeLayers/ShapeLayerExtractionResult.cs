using System;
using System.Collections.Generic;
using SvgCreator.Core.Models;

namespace SvgCreator.Core.ShapeLayers;

/// <summary>
/// シェイプレイヤー抽出処理の結果を表します。
/// </summary>
public sealed class ShapeLayerExtractionResult
{
    /// <summary>
    /// 新しい <see cref="ShapeLayerExtractionResult"/> を初期化します。
    /// </summary>
    /// <param name="shapeLayers">抽出されたシェイプレイヤー。</param>
    /// <param name="noisyLayers">ノイズとして除外されたレイヤー。</param>
    /// <exception cref="ArgumentNullException">引数が <c>null</c> です。</exception>
    public ShapeLayerExtractionResult(IReadOnlyList<ShapeLayer> shapeLayers, IReadOnlyList<NoisyLayer> noisyLayers)
    {
        ShapeLayers = shapeLayers ?? throw new ArgumentNullException(nameof(shapeLayers));
        NoisyLayers = noisyLayers ?? throw new ArgumentNullException(nameof(noisyLayers));
    }

    /// <summary>
    /// シェイプレイヤーの一覧を取得します。
    /// </summary>
    public IReadOnlyList<ShapeLayer> ShapeLayers { get; }

    /// <summary>
    /// ノイズレイヤーの一覧を取得します。
    /// </summary>
    public IReadOnlyList<NoisyLayer> NoisyLayers { get; }
}
