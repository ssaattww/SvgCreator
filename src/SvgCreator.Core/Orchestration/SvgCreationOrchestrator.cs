using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SvgCreator.Core.Diagnostics;

namespace SvgCreator.Core.Orchestration;

/// <summary>
/// SvgCreator パイプラインの実行を調整します。
/// </summary>
public sealed class SvgCreationOrchestrator
{
    private readonly IReadOnlyList<IPipelineStage> _stages;
    private readonly PipelineDependencies _dependencies;
    private readonly IDebugSink _debugSink;
    private readonly IProgress<PipelineStageProgress>? _progress;
    private readonly Func<DateTimeOffset> _clock;

    public SvgCreationOrchestrator(
        IEnumerable<IPipelineStage> stages,
        PipelineDependencies dependencies,
        IDebugSink debugSink,
        IProgress<PipelineStageProgress>? progress = null,
        Func<DateTimeOffset>? clock = null)
    {
        if (stages is null)
        {
            throw new ArgumentNullException(nameof(stages));
        }

        var stageList = stages.ToList();
        if (stageList.Count == 0)
        {
            throw new ArgumentException("At least one pipeline stage must be provided.", nameof(stages));
        }

        _stages = stageList;
        _dependencies = dependencies ?? throw new ArgumentNullException(nameof(dependencies));
        _debugSink = debugSink ?? throw new ArgumentNullException(nameof(debugSink));
        _progress = progress;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// パイプラインを実行します。
    /// </summary>
    /// <param name="options">実行オプション。</param>
    /// <param name="cancellationToken">キャンセル要求トークン。</param>
    /// <returns>生成された結果。</returns>
    public async Task<SvgCreationResult> ExecuteAsync(SvgCreatorRunOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        try
        {
            var context = new PipelineContext(options);
            var stageCount = _stages.Count;
            var debugContext = new DebugExecutionContext(_clock(), options.CliOptionSnapshot);
            var debugEnabled = options.EnableDebug;

            for (var index = 0; index < stageCount; index++)
            {
                var stage = _stages[index];
                var stageNumber = index + 1;

                ReportProgress(stage, PipelineStageStatus.Started, stageNumber, stageCount);

                try
                {
                    await stage.ExecuteAsync(context, _dependencies, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception stageError) when (!IsCancellation(stageError))
                {
                    throw SvgCreatorErrorTranslator.Translate(stageError, stage.Name);
                }

                ReportProgress(stage, PipelineStageStatus.Completed, stageNumber, stageCount);

                if (debugEnabled && stage.DebugStageName is not null)
                {
                    var snapshot = stage.CreateDebugSnapshot(context);
                    if (snapshot is not null)
                    {
                        try
                        {
                            await _debugSink.WriteSnapshotAsync(stage.DebugStageName, snapshot, debugContext, cancellationToken)
                                .ConfigureAwait(false);
                        }
                        catch (Exception debugError) when (!IsCancellation(debugError))
                        {
                            throw SvgCreatorErrorTranslator.Translate(debugError, stage.DebugStageName, isDebugOperation: true);
                        }
                    }
                }
            }

            if (debugEnabled)
            {
                try
                {
                    await _debugSink.CompleteAsync(debugContext, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception debugError) when (!IsCancellation(debugError))
                {
                    throw SvgCreatorErrorTranslator.Translate(debugError, "debug-complete", isDebugOperation: true);
                }
            }

            if (context.Image is null)
            {
                throw SvgCreatorException.FromCode(
                    SvgCreatorErrorCode.UnexpectedPipelineFailure,
                    "The pipeline did not produce an image.");
            }

            if (context.Quantization is null)
            {
                throw SvgCreatorException.FromCode(
                    SvgCreatorErrorCode.UnexpectedPipelineFailure,
                    "The pipeline did not produce a quantization result.");
            }

            if (context.DepthOrder is null)
            {
                throw SvgCreatorException.FromCode(
                    SvgCreatorErrorCode.UnexpectedPipelineFailure,
                    "The pipeline did not produce a depth order.");
            }

            if (context.CompletedLayers.Count == 0)
            {
                throw SvgCreatorException.FromCode(
                    SvgCreatorErrorCode.UnexpectedPipelineFailure,
                    "The pipeline did not produce completed shape layers.");
            }

            return new SvgCreationResult(context.Image, context.Quantization, context.DepthOrder);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (SvgCreatorException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw SvgCreatorErrorTranslator.Translate(ex, "pipeline");
        }
    }

    private static bool IsCancellation(Exception exception)
        => exception is OperationCanceledException or TaskCanceledException;

    private void ReportProgress(IPipelineStage stage, PipelineStageStatus status, int index, int total)
    {
        _progress?.Report(new PipelineStageProgress(stage.Name, stage.DisplayName, status, index, total));
    }
}
