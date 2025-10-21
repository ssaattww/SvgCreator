using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SvgCreator.Core.Diagnostics;
using SvgCreator.Core.Models;

namespace SvgCreator.Core.Orchestration;

/// <summary>
/// パイプライン内で共有されるステートを保持します。
/// </summary>
public sealed class PipelineContext
{
    public PipelineContext(SvgCreatorRunOptions options)
    {
        Options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// 実行時オプションを取得します。
    /// </summary>
    public SvgCreatorRunOptions Options { get; }

    /// <summary>
    /// 読み込んだ画像を取得します。
    /// </summary>
    public ImageData? Image { get; private set; }

    /// <summary>
    /// 減色結果を取得します。
    /// </summary>
    public QuantizationResult? Quantization { get; private set; }

    /// <summary>
    /// 生成されたレイヤー集合を取得します。
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
/// パイプラインステージに提供される依存関係の集合です。
/// </summary>
public sealed class PipelineDependencies
{
    public PipelineDependencies(IImageReader imageReader, IQuantizer quantizer)
    {
        ImageReader = imageReader ?? throw new ArgumentNullException(nameof(imageReader));
        Quantizer = quantizer ?? throw new ArgumentNullException(nameof(quantizer));
    }

    /// <summary>
    /// 画像読込コンポーネントを取得します。
    /// </summary>
    public IImageReader ImageReader { get; }

    /// <summary>
    /// 減色コンポーネントを取得します。
    /// </summary>
    public IQuantizer Quantizer { get; }
}

/// <summary>
/// パイプライン内のステージを表すインターフェイスです。
/// </summary>
public interface IPipelineStage
{
    /// <summary>
    /// ステージ ID。
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 表示名。
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// デバッグスナップショット出力時に使用するステージ名。不要な場合は <c>null</c>。
    /// </summary>
    string? DebugStageName { get; }

    /// <summary>
    /// ステージ処理を実行します。
    /// </summary>
    Task ExecuteAsync(PipelineContext context, PipelineDependencies dependencies, CancellationToken cancellationToken);

    /// <summary>
    /// デバッグスナップショットを生成します。
    /// </summary>
    DebugSnapshot? CreateDebugSnapshot(PipelineContext context);
}

/// <summary>
/// 入力画像を読み込むコンポーネントを表します。
/// </summary>
public interface IImageReader
{
    Task<ImageData> ReadAsync(SvgCreatorRunOptions options, CancellationToken cancellationToken);
}

/// <summary>
/// 減色コンポーネントを表します。
/// </summary>
public interface IQuantizer
{
    Task<QuantizationResult> QuantizeAsync(ImageData image, SvgCreatorRunOptions options, CancellationToken cancellationToken);
}

