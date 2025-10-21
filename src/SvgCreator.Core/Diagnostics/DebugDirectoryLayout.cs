using System;
using System.IO;
using System.Linq;

namespace SvgCreator.Core.Diagnostics;

/// <summary>
/// デバッグスナップショットのファイル配置を管理します。
/// </summary>
public sealed class DebugDirectoryLayout
{
    private const string StagesDirectoryName = "stages";

    public DebugDirectoryLayout(string baseDirectory)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseDirectory);
        BaseDirectory = baseDirectory;
    }

    /// <summary>
    /// 基準ディレクトリ（例: out/debug）。
    /// </summary>
    public string BaseDirectory { get; }

    /// <summary>
    /// メタデータファイルのパス。
    /// </summary>
    public string MetadataPath => Path.Combine(BaseDirectory, "metadata.json");

    /// <summary>
    /// パイプライン全体のスナップショットパス。
    /// </summary>
    public string PipelineSnapshotPath => Path.Combine(BaseDirectory, "pipeline.json");

    /// <summary>
    /// レイヤー概要のスナップショットパス。
    /// </summary>
    public string LayerSummaryPath => Path.Combine(BaseDirectory, "layers.json");

    /// <summary>
    /// 特定ステージのスナップショットファイルパスを取得します。
    /// </summary>
    public string GetStageSnapshotPath(string stageName)
    {
        var sanitized = SanitizeStageName(stageName);
        return Path.Combine(BaseDirectory, StagesDirectoryName, $"{sanitized}.json");
    }

    /// <summary>
    /// 特定ステージのアセット（PNG 等）格納ディレクトリパスを取得します。
    /// </summary>
    public string GetStageAssetsDirectory(string stageName)
    {
        var sanitized = SanitizeStageName(stageName);
        return Path.Combine(BaseDirectory, StagesDirectoryName, sanitized, "assets");
    }

    private static string SanitizeStageName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "stage";
        }

        var chars = name.Trim()
            .ToLowerInvariant()
            .Select(static c =>
            {
                if (char.IsLetterOrDigit(c))
                {
                    return c;
                }

                return '-';
            })
            .ToArray();

        var sanitized = new string(chars);
        while (sanitized.Contains("--", StringComparison.Ordinal))
        {
            sanitized = sanitized.Replace("--", "-", StringComparison.Ordinal);
        }

        sanitized = sanitized.Trim('-');
        return sanitized.Length == 0 ? "stage" : sanitized;
    }
}
