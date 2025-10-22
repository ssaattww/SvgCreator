using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SvgCreator.Core.Diagnostics;
using SvgCreator.Core.Models;

namespace SvgCreator.Core.Orchestration;

/// <summary>
/// パイプライン実行中の共有状態を保持します。
/// </summary>
public sealed class PipelineContext
{
    public PipelineContext(SvgCreatorRunOptions options)
    {
        Options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// 実行に利用するオプションを取得します。
    /// </summary>
    public SvgCreatorRunOptions Options { get; }

    /// <summary>
    /// 入力画像を取得します。
    /// </summary>
    public ImageData? Image { get; private set; }

    /// <summary>
    /// 量子化結果を取得します。
    /// </summary>
    public QuantizationResult? Quantization { get; private set; }

    /// <summary>
    /// 生成済みのシェイプレイヤーを取得します。
    /// </summary>
    public IReadOnlyList<ShapeLayer> ShapeLayers { get; private set; } = Array.Empty<ShapeLayer>();

    public void SetImage(ImageData image)
    {
        Image = image ?? throw new ArgumentNullException(nameof(image));
    }

    public void SetQuantization(QuantizationResult quantization)
    {
        Quantization = quantization ?? throw new ArgumentNullException(nameof(quantization));
    }

    public void SetShapeLayers(IReadOnlyList<ShapeLayer> layers)
    {
        ShapeLayers = layers ?? throw new ArgumentNullException(nameof(layers));
    }
}

/// <summary>
/// ステージが利用する依存サービスをまとめて提供します。
/// </summary>
public sealed class PipelineDependencies
{
    public PipelineDependencies(IImageReader imageReader, IQuantizer quantizer)
    {
        ImageReader = imageReader ?? throw new ArgumentNullException(nameof(imageReader));
        Quantizer = quantizer ?? throw new ArgumentNullException(nameof(quantizer));
    }

    /// <summary>
    /// 画像読み込みコンポーネントを取得します。
    /// </summary>
    public IImageReader ImageReader { get; }

    /// <summary>
    /// 量子化コンポーネントを取得します。
    /// </summary>
    public IQuantizer Quantizer { get; }
}

/// <summary>
/// パイプラインを構成するステージの共通インターフェースです。
/// </summary>
public interface IPipelineStage
{
    /// <summary>
    /// ステージ識別子。
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 表示名。
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// デバッグスナップショット出力時に利用するステージ名。不要な場合は null。
    /// </summary>
    string? DebugStageName { get; }

    /// <summary>
    /// ステージ処理を非同期で実行します。
    /// </summary>
    Task ExecuteAsync(PipelineContext context, PipelineDependencies dependencies, CancellationToken cancellationToken);

    /// <summary>
    /// デバッグスナップショットを生成します。不要な場合は null。
    /// </summary>
    DebugSnapshot? CreateDebugSnapshot(PipelineContext context);
}

/// <summary>
/// 入力画像を読み込むコンポーネントです。
/// </summary>
public interface IImageReader
{
    Task<ImageData> ReadAsync(SvgCreatorRunOptions options, CancellationToken cancellationToken);
}

/// <summary>
/// 量子化を行うコンポーネントです。
/// </summary>
public interface IQuantizer
{
    Task<QuantizationResult> QuantizeAsync(ImageData image, SvgCreatorRunOptions options, CancellationToken cancellationToken);
}
