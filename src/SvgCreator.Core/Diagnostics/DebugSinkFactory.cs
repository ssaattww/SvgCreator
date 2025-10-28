using System;
using System.Collections.Generic;
using System.IO;
using SvgCreator.Core.Models;
using SvgCreator.Core.Orchestration;
using SystemPath = System.IO.Path;

namespace SvgCreator.Core.Diagnostics;

/// <summary>
/// 実行オプションに基づいて適切なデバッグシンクを生成します。
/// </summary>
public static class DebugSinkFactory
{
    /// <summary>
    /// 実行オプションからデバッグシンクを生成します。
    /// </summary>
    /// <param name="options">実行オプション。</param>
    /// <returns>有効時は <see cref="FileDebugSink"/>、それ以外は <see cref="NullDebugSink"/>。</returns>
    /// <exception cref="ArgumentNullException">オプションが <c>null</c> の場合。</exception>
    public static IDebugSink Create(SvgCreatorRunOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!options.EnableDebug)
        {
            return new NullDebugSink();
        }

        var baseDirectory = ResolveDebugDirectory(options);
        var stages = options.DebugStages is { Count: > 0 }
            ? options.DebugStages
            : Array.Empty<string>();

        var layout = new DebugDirectoryLayout(baseDirectory);
        var serializer = new DebugSnapshotSerializer();
        var metadataBuilder = new DebugMetadataBuilder(DebugSnapshot.CurrentVersion);

        return new FileDebugSink(
            layout,
            serializer,
            metadataBuilder,
            enabledStages: stages,
            keepTemporaryFiles: options.DebugKeepTemporaryFiles);
    }

    internal static string ResolveDebugDirectory(SvgCreatorRunOptions options)
    {
        var directory = options.DebugDirectory;
        string resolved;

        if (string.IsNullOrWhiteSpace(directory))
        {
            resolved = SystemPath.Combine(options.OutputDirectory, "debug");
        }
        else if (SystemPath.IsPathRooted(directory))
        {
            resolved = directory;
        }
        else
        {
            resolved = SystemPath.Combine(options.OutputDirectory, directory);
        }

        return SystemPath.GetFullPath(resolved);
    }
}
