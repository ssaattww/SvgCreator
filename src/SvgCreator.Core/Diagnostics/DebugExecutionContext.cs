using System;
using System.Collections.Generic;

namespace SvgCreator.Core.Diagnostics;

/// <summary>
/// デバッグスナップショット書き出し時に必要な実行コンテキスト情報を保持します。
/// </summary>
public sealed class DebugExecutionContext
{
    public DebugExecutionContext(
        DateTimeOffset createdAt,
        IReadOnlyDictionary<string, string> cliOptions)
    {
        CreatedAt = createdAt;
        CliOptions = cliOptions ?? throw new ArgumentNullException(nameof(cliOptions));
    }

    /// <summary>
    /// スナップショット生成日時。
    /// </summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// 実行時に指定された主要 CLI オプションの写し。
    /// </summary>
    public IReadOnlyDictionary<string, string> CliOptions { get; }
}
