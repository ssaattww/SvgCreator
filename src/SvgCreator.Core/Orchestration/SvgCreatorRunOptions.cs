using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace SvgCreator.Core.Orchestration;

/// <summary>
/// SvgCreator パイプラインの実行オプションを表します。
/// </summary>
public sealed class SvgCreatorRunOptions
{
    /// <summary>
    /// 新しい <see cref="SvgCreatorRunOptions"/> を初期化します。
    /// </summary>
    /// <param name="imagePath">入力画像のパス。</param>
    /// <param name="outputDirectory">出力ディレクトリ。</param>
    /// <param name="cliOptionSnapshot">CLI オプションのスナップショット。</param>
    /// <exception cref="ArgumentException">必須引数が空白の場合に発生します。</exception>
    public SvgCreatorRunOptions(
        string imagePath,
        string outputDirectory,
        IReadOnlyDictionary<string, string>? cliOptionSnapshot = null)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            throw new ArgumentException("Image path must be specified.", nameof(imagePath));
        }

        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new ArgumentException("Output directory must be specified.", nameof(outputDirectory));
        }

        ImagePath = imagePath;
        OutputDirectory = outputDirectory;
        CliOptionSnapshot = cliOptionSnapshot is null
            ? ImmutableDictionary<string, string>.Empty
            : cliOptionSnapshot.ToImmutableDictionary(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 入力画像のパスを取得します。
    /// </summary>
    public string ImagePath { get; }

    /// <summary>
    /// 出力ディレクトリを取得します。
    /// </summary>
    public string OutputDirectory { get; }

    /// <summary>
    /// 量子化で使用するターゲットクラスタ数を取得または設定します。
    /// </summary>
    public int? QuantizationClusterCount { get; init; }

    /// <summary>
    /// デバッグモードを有効にするかどうかを取得または設定します。
    /// </summary>
    public bool EnableDebug { get; init; }

    /// <summary>
    /// デバッグ出力先ディレクトリを取得または設定します。
    /// </summary>
    public string? DebugDirectory { get; init; }

    /// <summary>
    /// デバッグ対象ステージの一覧を取得または設定します。
    /// </summary>
    public IReadOnlyCollection<string> DebugStages { get; init; } = Array.Empty<string>();

    /// <summary>
    /// デバッグ時に一時ファイルを保持するかどうかを取得または設定します。
    /// </summary>
    public bool DebugKeepTemporaryFiles { get; init; } = true;

    /// <summary>
    /// CLI オプションのスナップショットを取得します。
    /// </summary>
    public IReadOnlyDictionary<string, string> CliOptionSnapshot { get; }
}
