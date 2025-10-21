using System;

namespace SvgCreator.Core.Orchestration;

/// <summary>
/// パイプラインステージの進捗状態を示します。
/// </summary>
public enum PipelineStageStatus
{
    /// <summary>
    /// ステージの処理が開始した状態です。
    /// </summary>
    Started,

    /// <summary>
    /// ステージの処理が完了した状態です。
    /// </summary>
    Completed
}

/// <summary>
/// レポートされるステージ進捗イベントです。
/// </summary>
public sealed class PipelineStageProgress
{
    /// <summary>
    /// <see cref="PipelineStageProgress"/> を初期化します。
    /// </summary>
    /// <param name="stageName">ステージ ID。</param>
    /// <param name="displayName">表示用名称。</param>
    /// <param name="status">進捗状態。</param>
    /// <param name="stageIndex">全体におけるステージ順序（1 始まり）。</param>
    /// <param name="totalStages">全ステージ数。</param>
    /// <exception cref="ArgumentException">名前が未指定、またはインデックスが無効です。</exception>
    public PipelineStageProgress(
        string stageName,
        string displayName,
        PipelineStageStatus status,
        int stageIndex,
        int totalStages)
    {
        if (string.IsNullOrWhiteSpace(stageName))
        {
            throw new ArgumentException("Stage name must be provided.", nameof(stageName));
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Display name must be provided.", nameof(displayName));
        }

        if (stageIndex <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(stageIndex), stageIndex, "Stage index must be positive.");
        }

        if (totalStages <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalStages), totalStages, "Total stages must be positive.");
        }

        StageName = stageName;
        DisplayName = displayName;
        Status = status;
        StageIndex = stageIndex;
        TotalStages = totalStages;
    }

    /// <summary>
    /// ステージ ID を取得します。
    /// </summary>
    public string StageName { get; }

    /// <summary>
    /// 表示用ステージ名を取得します。
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// 現在の進捗状態を取得します。
    /// </summary>
    public PipelineStageStatus Status { get; }

    /// <summary>
    /// 処理中ステージの順序（1 始まり）を取得します。
    /// </summary>
    public int StageIndex { get; }

    /// <summary>
    /// 全体のステージ数を取得します。
    /// </summary>
    public int TotalStages { get; }
}

