using System;
using System.Threading;
using System.Threading.Tasks;
using SvgCreator.Core.Diagnostics;
using SvgCreator.Core.Occlusion;

namespace SvgCreator.Core.Orchestration.Stages;

/// <summary>
/// エラスティカ補完を実行するパイプラインステージです。
/// </summary>
public sealed class OcclusionCompletionStage : IPipelineStage
{
    /// <inheritdoc />
    public string Name => PipelineStageNames.OcclusionCompletion;

    /// <inheritdoc />
    public string DisplayName => "Occlusion Completion";

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

        if (context.ShapeLayers.Count == 0)
        {
            throw new InvalidOperationException("Occlusion completion requires extracted shape layers.");
        }

        if (context.DepthOrder is null)
        {
            throw new InvalidOperationException("Occlusion completion requires a resolved depth order.");
        }

        var options = BuildOptions(context.Options);

        var result = await dependencies.OcclusionCompleter
            .CompleteAsync(context.ShapeLayers, context.DepthOrder, options, cancellationToken)
            .ConfigureAwait(false);

        context.SetCompletedLayers(result.CompletedLayers);
    }

    /// <inheritdoc />
    public DebugSnapshot? CreateDebugSnapshot(PipelineContext context) => null;

    private static OcclusionCompletionOptions BuildOptions(SvgCreatorRunOptions options)
    {
        var completionOptions = new OcclusionCompletionOptions
        {
            EulerIterationLimit = options.OcclusionIterationLimit ?? OcclusionCompletionOptions.DefaultEulerIterationLimit,
            Epsilon = options.OcclusionEpsilon ?? OcclusionCompletionOptions.DefaultEpsilon,
            CurvatureWeight = options.OcclusionCurvatureWeight ?? OcclusionCompletionOptions.DefaultCurvatureWeight,
            LengthWeight = options.OcclusionLengthWeight ?? OcclusionCompletionOptions.DefaultLengthWeight,
            NeighborhoodRadius = options.OcclusionNeighborhoodRadius ?? OcclusionCompletionOptions.DefaultNeighborhoodRadius,
            ClosingTolerance = options.OcclusionClosingTolerance ?? OcclusionCompletionOptions.DefaultClosingTolerance
        };

        return completionOptions;
    }
}
