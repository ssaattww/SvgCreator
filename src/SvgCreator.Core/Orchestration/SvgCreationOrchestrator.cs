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

        var context = new PipelineContext(options);
        var stageCount = _stages.Count;
        var debugContext = new DebugExecutionContext(_clock(), options.CliOptionSnapshot);
        var debugEnabled = options.EnableDebug;

        for (var index = 0; index < stageCount; index++)
        {
            var stage = _stages[index];
            var stageNumber = index + 1;

            ReportProgress(stage, PipelineStageStatus.Started, stageNumber, stageCount);
            await stage.ExecuteAsync(context, _dependencies, cancellationToken).ConfigureAwait(false);
            ReportProgress(stage, PipelineStageStatus.Completed, stageNumber, stageCount);

            if (debugEnabled && stage.DebugStageName is not null)
            {
                var snapshot = stage.CreateDebugSnapshot(context);
                if (snapshot is not null)
                {
                    await _debugSink.WriteSnapshotAsync(stage.DebugStageName, snapshot, debugContext, cancellationToken)
                        .ConfigureAwait(false);
                }
            }
        }

        if (debugEnabled)
        {
            await _debugSink.CompleteAsync(debugContext, cancellationToken).ConfigureAwait(false);
        }

        if (context.Image is null)
        {
            throw new InvalidOperationException("The pipeline did not produce an image.");
        }

        if (context.Quantization is null)
        {
            throw new InvalidOperationException("The pipeline did not produce a quantization result.");
        }

        if (context.DepthOrder is null)
        {
            throw new InvalidOperationException("The pipeline did not produce a depth order.");
        }

        return new SvgCreationResult(context.Image, context.Quantization, context.DepthOrder);
    }

    private void ReportProgress(IPipelineStage stage, PipelineStageStatus status, int index, int total)
    {
        _progress?.Report(new PipelineStageProgress(stage.Name, stage.DisplayName, status, index, total));
    }
}
