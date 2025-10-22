using System;
using System.Collections.Generic;

namespace SvgCreator.Core.Diagnostics;

/// <summary>
/// <see cref="DebugMetadata"/> を構築するビルダーです。
/// </summary>
public sealed class DebugMetadataBuilder
{
    private readonly string _version;
    private DateTimeOffset _createdAt = DateTimeOffset.UtcNow;
    private readonly Dictionary<string, string> _cliOptions = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<DebugMetadataFile> _files = new();

    public DebugMetadataBuilder(string version)
    {
        ArgumentException.ThrowIfNullOrEmpty(version);
        _version = version;
    }

    public void SetCreatedAt(DateTimeOffset createdAt)
    {
        _createdAt = createdAt;
    }

    public void SetCliOption(string key, string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        _cliOptions[key] = value;
    }

    public void AddFile(string role, string relativePath, string contentType, string? stage = null)
    {
        var file = new DebugMetadataFile(role, relativePath, contentType, stage);
        _files.Add(file);
    }

    public DebugMetadata Build()
    {
        return new DebugMetadata(
            _version,
            _createdAt,
            new Dictionary<string, string>(_cliOptions),
            _files.ToArray());
    }
}
