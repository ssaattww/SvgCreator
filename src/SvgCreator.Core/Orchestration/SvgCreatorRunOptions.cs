using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace SvgCreator.Core.Orchestration;

/// <summary>
/// SvgCreator パイプラインの実行に必要なオプションを表します。
/// </summary>
public sealed class SvgCreatorRunOptions
{
    /// <summary>
    /// <see cref="SvgCreatorRunOptions"/> を初期化します。
    /// </summary>
    /// <param name="imagePath">入力画像のパス。</param>
    /// <param name="outputDirectory">出力ディレクトリ。</param>
    /// <param name="cliOptionSnapshot">CLI オプションの写し。</param>
    /// <exception cref="ArgumentException">必須パラメータが空白です。</exception>
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
    /// 入力画像ファイルのパスを取得します。
    /// </summary>
    public string ImagePath { get; }

    /// <summary>
    /// 出力ディレクトリを取得します。
    /// </summary>
    public string OutputDirectory { get; }

    /// <summary>
    /// 減色処理で目標とするクラスタ数（K 値）を取得または設定します。<c>null</c> の場合は実装既定値を使用します。
    /// </summary>
    public int? QuantizationClusterCount { get; init; }

    /// <summary>
    /// デバッグ出力を有効にするかどうかを取得または設定します。
    /// </summary>
    public bool EnableDebug { get; init; }

    /// <summary>
    /// デバッグ出力先ディレクトリ（相対または絶対）を取得または設定します。
    /// </summary>
    public string? DebugDirectory { get; init; }

    /// <summary>
    /// デバッグ対象ステージの一覧を取得または設定します。
    /// </summary>
    public IReadOnlyCollection<string> DebugStages { get; init; } = Array.Empty<string>();

    /// <summary>
    /// デバッグ処理完了後にテンポラリファイルを保持するかどうかを取得または設定します。
    /// </summary>
    public bool DebugKeepTemporaryFiles { get; init; } = true;

    /// <summary>
    /// CLI オプションの写しを取得します。
    /// </summary>
    public IReadOnlyDictionary<string, string> CliOptionSnapshot { get; }
}
