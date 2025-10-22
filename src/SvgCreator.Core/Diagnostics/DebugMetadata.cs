using System;
using System.Collections.Generic;
using System.Text.Json;

namespace SvgCreator.Core.Diagnostics;

/// <summary>
/// デバッグスナップショットに付随するメタデータです。
/// </summary>
public sealed class DebugMetadata
{
    public DebugMetadata(
        string version,
        DateTimeOffset createdAt,
        IReadOnlyDictionary<string, string> cliOptions,
        IReadOnlyList<DebugMetadataFile> files)
    {
        ArgumentException.ThrowIfNullOrEmpty(version);
        Version = version;
        CreatedAt = createdAt;
        CliOptions = cliOptions ?? throw new ArgumentNullException(nameof(cliOptions));
        Files = files ?? throw new ArgumentNullException(nameof(files));
    }

    /// <summary>
    /// メタデータのフォーマットバージョン。
    /// </summary>
    public string Version { get; }

    /// <summary>
    /// スナップショットが生成された日時。
    /// </summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// 実行時の CLI オプション抜粋。
    /// </summary>
    public IReadOnlyDictionary<string, string> CliOptions { get; }

    /// <summary>
    /// 出力されたファイル一覧。
    /// </summary>
    public IReadOnlyList<DebugMetadataFile> Files { get; }

    /// <summary>
    /// JSON 文字列にシリアライズします。
    /// </summary>
    public string ToJson()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
        return JsonSerializer.Serialize(this, options);
    }
}

/// <summary>
/// デバッグ出力に含まれるファイルのメタ情報です。
/// </summary>
public sealed class DebugMetadataFile
{
    public DebugMetadataFile(
        string role,
        string relativePath,
        string contentType,
        string? stage)
    {
        ArgumentException.ThrowIfNullOrEmpty(role);
        ArgumentException.ThrowIfNullOrEmpty(relativePath);
        ArgumentException.ThrowIfNullOrEmpty(contentType);

        Role = role;
        RelativePath = relativePath;
        ContentType = contentType;
        Stage = stage;
    }

    public string Role { get; }

    public string RelativePath { get; }

    public string ContentType { get; }

    public string? Stage { get; }
}
