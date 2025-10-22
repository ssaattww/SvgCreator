using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SvgCreator.Core.Diagnostics;

/// <summary>
/// デバッグスナップショットをファイルへ書き出すシンクです。
/// </summary>
public sealed class FileDebugSink : IDebugSink
{
    private const string JsonContentType = "application/json";

    private readonly DebugDirectoryLayout _layout;
    private readonly DebugSnapshotSerializer _serializer;
    private readonly DebugMetadataBuilder _metadataBuilder;
    private readonly HashSet<string>? _stageFilter;
    private readonly bool _keepTemporaryFiles;
    private bool _metadataInitialized;

    public FileDebugSink(
        DebugDirectoryLayout layout,
        DebugSnapshotSerializer serializer,
        DebugMetadataBuilder metadataBuilder,
        IEnumerable<string>? enabledStages = null,
        bool keepTemporaryFiles = true)
    {
        _layout = layout ?? throw new ArgumentNullException(nameof(layout));
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _metadataBuilder = metadataBuilder ?? throw new ArgumentNullException(nameof(metadataBuilder));
        _keepTemporaryFiles = keepTemporaryFiles;

        if (enabledStages is not null)
        {
            _stageFilter = enabledStages
                .Select(DebugDirectoryLayout.SanitizeStageName)
                .Where(static n => !string.IsNullOrEmpty(n))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
    }

    public async Task WriteSnapshotAsync(string stageName, DebugSnapshot snapshot, DebugExecutionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(stageName);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(context);

        InitializeMetadata(context);

        if (!IsStageEnabled(stageName))
        {
            return;
        }

        var descriptor = ResolveDescriptor(stageName);
        Directory.CreateDirectory(Path.GetDirectoryName(descriptor.Path)!);

        await using (var stream = new FileStream(descriptor.Path, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await _serializer.SerializeAsync(snapshot, stream, cancellationToken).ConfigureAwait(false);
        }

        var relativePath = Path.GetRelativePath(_layout.BaseDirectory, descriptor.Path);
        _metadataBuilder.AddFile(descriptor.Role, relativePath, JsonContentType, descriptor.Stage);
    }

    public async Task CompleteAsync(DebugExecutionContext context, CancellationToken cancellationToken = default)
    {
        InitializeMetadata(context);
        Directory.CreateDirectory(_layout.BaseDirectory);

        var metadataJson = _metadataBuilder.Build().ToJson();
        await File.WriteAllTextAsync(_layout.MetadataPath, metadataJson, cancellationToken).ConfigureAwait(false);

        if (!_keepTemporaryFiles)
        {
            // 現状テンポラリファイルは生成していないため将来拡張に備えたプレースホルダ。
        }
    }

    private void InitializeMetadata(DebugExecutionContext context)
    {
        if (_metadataInitialized)
        {
            return;
        }

        _metadataBuilder.SetCreatedAt(context.CreatedAt);
        foreach (var option in context.CliOptions)
        {
            _metadataBuilder.SetCliOption(option.Key, option.Value);
        }

        _metadataInitialized = true;
    }

    private bool IsStageEnabled(string stageName)
    {
        if (_stageFilter is null || _stageFilter.Count == 0)
        {
            return true;
        }

        var sanitized = DebugDirectoryLayout.SanitizeStageName(stageName);
        // パイプラインやレイヤ概要は常に出力
        if (IsSpecialStage(stageName))
        {
            return true;
        }

        return _stageFilter.Contains(sanitized);
    }

    private static bool IsSpecialStage(string stageName)
    {
        return stageName.Equals("pipeline", StringComparison.OrdinalIgnoreCase)
            || stageName.Equals("layers", StringComparison.OrdinalIgnoreCase);
    }

    private (string Path, string Role, string? Stage) ResolveDescriptor(string stageName)
    {
        if (stageName.Equals("pipeline", StringComparison.OrdinalIgnoreCase))
        {
            return (_layout.PipelineSnapshotPath, "pipeline", null);
        }

        if (stageName.Equals("layers", StringComparison.OrdinalIgnoreCase))
        {
            return (_layout.LayerSummaryPath, "layer-summary", null);
        }

        var sanitized = DebugDirectoryLayout.SanitizeStageName(stageName);
        var path = _layout.GetStageSnapshotPath(stageName);
        return (path, "stage", sanitized);
    }
}
