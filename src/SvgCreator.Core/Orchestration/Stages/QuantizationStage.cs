using System;
using System.Threading;
using System.Threading.Tasks;
using SvgCreator.Core.Diagnostics;
using SvgCreator.Core.Models;

namespace SvgCreator.Core.Orchestration.Stages;

/// <summary>
/// 減色ステージ。
/// </summary>
public sealed class QuantizationStage : IPipelineStage
{
    /// <summary>
    /// デバッグ出力で使用するステージ名。
    /// </summary>
    public const string DebugStageName = "pipeline";

    /// <inheritdoc />
    string IPipelineStage.DebugStageName => DebugStageName;

    /// <inheritdoc />
    public string Name => PipelineStageNames.Quantization;

    /// <inheritdoc />
    public string DisplayName => "Quantization";

    /// <inheritdoc />
    public async Task ExecuteAsync(PipelineContext context, PipelineDependencies dependencies, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(dependencies);

        var image = context.Image ?? throw new InvalidOperationException("Quantization requires an input image.");
        var result = await dependencies.Quantizer.QuantizeAsync(image, context.Options, cancellationToken).ConfigureAwait(false);
        context.SetQuantization(result);

        SvgCreator.Core.BinarizedImageWriter.Write(result, context.Options);
    }

    /// <inheritdoc />
    public DebugSnapshot? CreateDebugSnapshot(PipelineContext context)
    {
        if (context.Quantization is null)
        {
            return null;
        }

        return DebugSnapshot.From(context.Quantization, context.ShapeLayers);
    }
}
