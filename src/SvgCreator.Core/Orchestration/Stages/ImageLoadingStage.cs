using System;
using System.Threading;
using System.Threading.Tasks;
using SvgCreator.Core.Diagnostics;

namespace SvgCreator.Core.Orchestration.Stages;

/// <summary>
/// 画像読込ステージ。
/// </summary>
public sealed class ImageLoadingStage : IPipelineStage
{
    /// <inheritdoc />
    public string Name => PipelineStageNames.ImageLoading;

    /// <inheritdoc />
    public string DisplayName => "Image Loading";

    /// <inheritdoc />
    public string? DebugStageName => null;

    /// <inheritdoc />
    public async Task ExecuteAsync(PipelineContext context, PipelineDependencies dependencies, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(dependencies);

        var image = await dependencies.ImageReader.ReadAsync(context.Options, cancellationToken).ConfigureAwait(false);
        context.SetImage(image);
    }

    /// <inheritdoc />
    public DebugSnapshot? CreateDebugSnapshot(PipelineContext context) => null;
}
