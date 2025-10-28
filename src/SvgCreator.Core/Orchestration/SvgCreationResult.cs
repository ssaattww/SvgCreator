using System;
using SvgCreator.Core.DepthOrdering;
using SvgCreator.Core.Models;

namespace SvgCreator.Core.Orchestration;

/// <summary>
/// パイプライン実行の結果を表します。
/// </summary>
public sealed class SvgCreationResult
{
    /// <summary>
    /// 新しい <see cref="SvgCreationResult"/> を初期化します。
    /// </summary>
    /// <param name="image">処理済みの入力画像。</param>
    /// <param name="quantization">量子化結果。</param>
    /// <param name="depthOrder">レイヤーの深度順序。</param>
    /// <exception cref="ArgumentNullException">必須引数が null の場合に送出されます。</exception>
    public SvgCreationResult(ImageData image, QuantizationResult quantization, DepthOrder depthOrder)
    {
        Image = image ?? throw new ArgumentNullException(nameof(image));
        Quantization = quantization ?? throw new ArgumentNullException(nameof(quantization));
        DepthOrder = depthOrder ?? throw new ArgumentNullException(nameof(depthOrder));
    }

    /// <summary>
    /// 入力画像を取得します。
    /// </summary>
    public ImageData Image { get; }

    /// <summary>
    /// 量子化結果を取得します。
    /// </summary>
    public QuantizationResult Quantization { get; }

    /// <summary>
    /// レイヤーの深度順序を取得します。
    /// </summary>
    public DepthOrder DepthOrder { get; }
}
