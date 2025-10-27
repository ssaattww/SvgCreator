using System;
using System.Threading;
using System.Threading.Tasks;
using SvgCreator.Core.Diagnostics;

namespace SvgCreator.Core.Orchestration.Stages;

/// <summary>
/// 量子化結果からシェイプレイヤーを抽出します。
/// </summary>
public sealed class ShapeLayerExtractionStage : IPipelineStage
{
    /// <inheritdoc />
    public string Name => PipelineStageNames.ShapeLayerExtraction;

    /// <inheritdoc />
    public string DisplayName => "Shape Layer Extraction";

    /// <inheritdoc />
    public string? DebugStageName => null;

    /// <inheritdoc />
    public async Task ExecuteAsync(PipelineContext context, PipelineDependencies dependencies, CancellationToken cancellationToken)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (dependencies is null)
        {
            throw new ArgumentNullException(nameof(dependencies));
        }

        if (context.Quantization is null)
        {
            throw new InvalidOperationException("Quantization result is required before extracting shape layers.");
        }

        var extractionResult = await dependencies.ShapeLayerBuilder
            .BuildLayersAsync(context.Quantization, cancellationToken)
            .ConfigureAwait(false);

        context.SetShapeLayerExtractionResult(extractionResult);
    }

    /// <inheritdoc />
    public DebugSnapshot? CreateDebugSnapshot(PipelineContext context) => null;
}
