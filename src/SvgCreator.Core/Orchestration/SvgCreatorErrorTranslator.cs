using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SvgCreator.Core.Orchestration;

/// <summary>
/// 例外を <see cref="SvgCreatorException"/> に変換するユーティリティです。
/// </summary>
internal static class SvgCreatorErrorTranslator
{
    /// <summary>
    /// 指定した例外を <see cref="SvgCreatorException"/> に変換します。
    /// </summary>
    /// <param name="exception">捕捉した例外。</param>
    /// <param name="source">エラーが発生したステージまたはコンテキスト。</param>
    /// <param name="isDebugOperation">デバッグ出力処理中のエラーかどうか。</param>
    /// <returns>対応する <see cref="SvgCreatorException"/>。</returns>
    public static SvgCreatorException Translate(Exception exception, string source, bool isDebugOperation = false)
    {
        var root = Unwrap(exception);

        if (root is SvgCreatorException alreadyClassified)
        {
            return alreadyClassified;
        }

        var details = root.Message;

        if (root is FileNotFoundException)
        {
            return SvgCreatorException.FromCode(SvgCreatorErrorCode.InputFileNotFound, details, root);
        }

        if (root is NotSupportedException)
        {
            return SvgCreatorException.FromCode(SvgCreatorErrorCode.InputUnsupportedFormat, details, root);
        }

        if (root is InvalidDataException)
        {
            var code = isDebugOperation
                ? SvgCreatorErrorCode.DebugSnapshotVersionMismatch
                : SvgCreatorErrorCode.ImageDecodeFailed;
            return SvgCreatorException.FromCode(code, details, root);
        }

        if (root is IOException)
        {
            var code = isDebugOperation
                ? SvgCreatorErrorCode.DebugWriteFailed
                : SvgCreatorErrorCode.OutputWriteFailed;
            return SvgCreatorException.FromCode(code, details, root);
        }

        var fallback = ResolveFallbackCode(source, isDebugOperation);
        return SvgCreatorException.FromCode(fallback, details, root);
    }

    private static Exception Unwrap(Exception exception)
    {
        if (exception is AggregateException aggregate)
        {
            var flattened = aggregate.Flatten();
            var first = flattened.InnerExceptions.FirstOrDefault();
            return first is null ? aggregate : Unwrap(first);
        }

        return exception;
    }

    private static SvgCreatorErrorCode ResolveFallbackCode(string source, bool isDebugOperation)
    {
        if (isDebugOperation)
        {
            return SvgCreatorErrorCode.DebugWriteFailed;
        }

        return source switch
        {
            PipelineStageNames.ImageLoading => SvgCreatorErrorCode.ImageDecodeFailed,
            PipelineStageNames.ShapeLayerExtraction => SvgCreatorErrorCode.SegmentationProducedNoLayers,
            PipelineStageNames.DepthOrdering => SvgCreatorErrorCode.DepthOrderingCyclicDependency,
            PipelineStageNames.OcclusionCompletion => SvgCreatorErrorCode.OcclusionSolverDidNotConverge,
            _ => SvgCreatorErrorCode.UnexpectedPipelineFailure
        };
    }
}
