using System;
using SvgCreator.Core.Models;

namespace SvgCreator.Core.Orchestration;

/// <summary>
/// パイプライン実行後に得られる主要な成果物を表します。
/// </summary>
public sealed class SvgCreationResult
{
    /// <summary>
    /// <see cref="SvgCreationResult"/> を初期化します。
    /// </summary>
    /// <param name="image">読み込んだ画像。</param>
    /// <param name="quantization">減色結果。</param>
    /// <exception cref="ArgumentNullException">必要な成果が <c>null</c> です。</exception>
    public SvgCreationResult(ImageData image, QuantizationResult quantization)
    {
        Image = image ?? throw new ArgumentNullException(nameof(image));
        Quantization = quantization ?? throw new ArgumentNullException(nameof(quantization));
    }

    /// <summary>
    /// 入力画像を取得します。
    /// </summary>
    public ImageData Image { get; }

    /// <summary>
    /// 減色結果を取得します。
    /// </summary>
    public QuantizationResult Quantization { get; }
}

