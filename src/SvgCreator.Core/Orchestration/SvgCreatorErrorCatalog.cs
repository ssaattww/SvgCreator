using System.Collections.Generic;
using System.Linq;

namespace SvgCreator.Core.Orchestration;

/// <summary>
/// SVG Creator 固有のエラーコードを定義します。
/// </summary>
public enum SvgCreatorErrorCode
{
    /// <summary>
    /// 入力画像のパスが存在しません。
    /// </summary>
    InputFileNotFound,

    /// <summary>
    /// 対応していない画像形式が指定されました。
    /// </summary>
    InputUnsupportedFormat,

    /// <summary>
    /// 画像データをデコードできませんでした。
    /// </summary>
    ImageDecodeFailed,

    /// <summary>
    /// 連結成分を抽出できず、レイヤーが生成されませんでした。
    /// </summary>
    SegmentationProducedNoLayers,

    /// <summary>
    /// 深度整序グラフに閉路が検出されました。
    /// </summary>
    DepthOrderingCyclicDependency,

    /// <summary>
    /// 遮蔽補完ソルバーが収束しませんでした。
    /// </summary>
    OcclusionSolverDidNotConverge,

    /// <summary>
    /// レイヤー出力が 15KB 制限を超過しました。
    /// </summary>
    LayerSizeLimitExceeded,

    /// <summary>
    /// SVG または関連ファイルの書き出しに失敗しました。
    /// </summary>
    OutputWriteFailed,

    /// <summary>
    /// デバッグスナップショットの書き込みに失敗しました。
    /// </summary>
    DebugWriteFailed,

    /// <summary>
    /// 既存のデバッグスナップショットとバージョンが不一致です。
    /// </summary>
    DebugSnapshotVersionMismatch,

    /// <summary>
    /// 想定外のパイプラインエラーが発生しました。
    /// </summary>
    UnexpectedPipelineFailure
}

/// <summary>
/// エラーが属するカテゴリを表します。
/// </summary>
public enum SvgCreatorErrorCategory
{
    /// <summary>
    /// 入力ファイルや読み込みに関する問題。
    /// </summary>
    Input,

    /// <summary>
    /// コンフィギュレーション／パラメータ起因の問題。
    /// </summary>
    Configuration,

    /// <summary>
    /// セグメンテーション処理に関する問題。
    /// </summary>
    Segmentation,

    /// <summary>
    /// 深度整序処理に関する問題。
    /// </summary>
    DepthOrdering,

    /// <summary>
    /// 遮蔽補完処理に関する問題。
    /// </summary>
    Occlusion,

    /// <summary>
    /// SVG などの出力処理に関する問題。
    /// </summary>
    Output,

    /// <summary>
    /// デバッグ出力処理に関する問題。
    /// </summary>
    Debug,

    /// <summary>
    /// 想定外の汎用的な問題。
    /// </summary>
    Unexpected
}

/// <summary>
/// エラーの分類情報を保持します。
/// </summary>
/// <param name="Code">エラーコード。</param>
/// <param name="Category">エラーカテゴリ。</param>
/// <param name="Summary">簡潔な概要。</param>
/// <param name="RecommendedAction">推奨される対処方針。</param>
public sealed record SvgCreatorErrorDescriptor(
    SvgCreatorErrorCode Code,
    SvgCreatorErrorCategory Category,
    string Summary,
    string RecommendedAction);

/// <summary>
/// エラー分類のカタログを提供します。
/// </summary>
public static class SvgCreatorErrorCatalog
{
    private static readonly IReadOnlyDictionary<SvgCreatorErrorCode, SvgCreatorErrorDescriptor> Catalog =
        BuildCatalog();

    /// <summary>
    /// 既知のエラー記述子の一覧です。
    /// </summary>
    public static IReadOnlyList<SvgCreatorErrorDescriptor> AllDescriptors { get; } =
        Catalog.Values.ToArray();

    /// <summary>
    /// 指定したエラーコードに対応する記述子を取得します。
    /// </summary>
    /// <param name="code">エラーコード。</param>
    /// <returns>記述子。</returns>
    /// <exception cref="ArgumentOutOfRangeException">未知のエラーコードが指定された場合。</exception>
    public static SvgCreatorErrorDescriptor GetDescriptor(SvgCreatorErrorCode code)
    {
        if (!Catalog.TryGetValue(code, out var descriptor))
        {
            throw new ArgumentOutOfRangeException(nameof(code), code, "Unknown error code.");
        }

        return descriptor;
    }

    private static IReadOnlyDictionary<SvgCreatorErrorCode, SvgCreatorErrorDescriptor> BuildCatalog()
    {
        var descriptors = new List<SvgCreatorErrorDescriptor>
        {
            new(
                SvgCreatorErrorCode.InputFileNotFound,
                SvgCreatorErrorCategory.Input,
                "The input image file could not be found.",
                "Verify the --image path is correct and points to an accessible file."),
            new(
                SvgCreatorErrorCode.InputUnsupportedFormat,
                SvgCreatorErrorCategory.Input,
                "The input image format is not supported.",
                "Convert the image to PNG or JPEG, or provide a supported format."),
            new(
                SvgCreatorErrorCode.ImageDecodeFailed,
                SvgCreatorErrorCategory.Input,
                "The image could not be decoded.",
                "Ensure the file is not corrupted and is a valid PNG or JPEG."),
            new(
                SvgCreatorErrorCode.SegmentationProducedNoLayers,
                SvgCreatorErrorCategory.Segmentation,
                "Segmentation produced no usable shape layers.",
                "Adjust quantization K or noisy-layer thresholds and retry."),
            new(
                SvgCreatorErrorCode.DepthOrderingCyclicDependency,
                SvgCreatorErrorCategory.DepthOrdering,
                "A cyclic dependency was detected while resolving depth order.",
                "Review the segmentation result or tweak --delta to break the cycle."),
            new(
                SvgCreatorErrorCode.OcclusionSolverDidNotConverge,
                SvgCreatorErrorCategory.Occlusion,
                "Occlusion completion failed to converge within the iteration limit.",
                "Relax occlusion parameters (e.g., --eps, --a, --b) or simplify the input."),
            new(
                SvgCreatorErrorCode.LayerSizeLimitExceeded,
                SvgCreatorErrorCategory.Output,
                "A layer exceeded the 15KB SVG size limit.",
                "Increase simplification tolerances or export fewer layers at once."),
            new(
                SvgCreatorErrorCode.OutputWriteFailed,
                SvgCreatorErrorCategory.Output,
                "Writing the SVG output failed.",
                "Confirm the output directory exists and you have write permissions."),
            new(
                SvgCreatorErrorCode.DebugWriteFailed,
                SvgCreatorErrorCategory.Debug,
                "Debug snapshot files could not be written.",
                "Verify the debug directory is writable or disable --debug."),
            new(
                SvgCreatorErrorCode.DebugSnapshotVersionMismatch,
                SvgCreatorErrorCategory.Debug,
                "Existing debug snapshots were created with an incompatible schema version.",
                "Clear the debug directory or regenerate snapshots with the current build."),
            new(
                SvgCreatorErrorCode.UnexpectedPipelineFailure,
                SvgCreatorErrorCategory.Unexpected,
                "An unexpected error interrupted the pipeline.",
                "Review the logs with --verbose enabled and report the issue."),
        };

        var map = new Dictionary<SvgCreatorErrorCode, SvgCreatorErrorDescriptor>(descriptors.Count);
        foreach (var descriptor in descriptors)
        {
            map[descriptor.Code] = descriptor;
        }

        return map;
    }
}
