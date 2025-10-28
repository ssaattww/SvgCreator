using System;
using System.Threading;
using System.Threading.Tasks;
using SvgCreator.Core.DepthOrdering;
using SvgCreator.Core.Diagnostics;

namespace SvgCreator.Core.Orchestration.Stages;

/// <summary>
/// シェイプレイヤーの深度順序を決定するステージです。
/// </summary>
public sealed class DepthOrderingStage : IPipelineStage
{
    /// <inheritdoc />
    public string Name => PipelineStageNames.DepthOrdering;

    /// <inheritdoc />
    public string DisplayName => "Depth Ordering";

    /// <inheritdoc />
    public string? DebugStageName => null;

    /// <inheritdoc />
    public Task ExecuteAsync(PipelineContext context, PipelineDependencies dependencies, CancellationToken cancellationToken)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (dependencies is null)
        {
            throw new ArgumentNullException(nameof(dependencies));
        }

        if (context.ShapeLayers.Count == 0)
        {
            throw new InvalidOperationException("Depth ordering requires extracted shape layers.");
        }

        var depthDelta = context.Options.DepthOrderingDelta ?? DepthOrderingOptions.DefaultDelta;
        var options = new DepthOrderingOptions
        {
            Delta = depthDelta
        };

        var depthOrder = dependencies.DepthOrdering.Compute(context.ShapeLayers, options);
        context.SetDepthOrder(depthOrder);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public DebugSnapshot? CreateDebugSnapshot(PipelineContext context) => null;
}
