using System;
using System.Linq;
using System.Text.Json;
using SvgCreator.Core.Diagnostics;

namespace SvgCreator.Core.Tests.Diagnostics;

public sealed class DebugMetadataBuilderTests
{
    // ファイルエントリを追加するとメタデータに反映されることを確認
    [Fact]
    public void AddFile_AddsEntry()
    {
        var builder = new DebugMetadataBuilder("1.0");
        builder.SetCreatedAt(new DateTimeOffset(2025, 10, 21, 7, 30, 0, TimeSpan.Zero));
        builder.SetCliOption("threads", "8");
        builder.AddFile(role: "pipeline", relativePath: "pipeline.json", contentType: "application/json");
        builder.AddFile(role: "layer", relativePath: "stages/layer-001.json", contentType: "application/json", stage: "layer-001");

        var metadata = builder.Build();

        Assert.Equal("1.0", metadata.Version);
        Assert.Equal(2, metadata.Files.Count);
        Assert.Equal("pipeline.json", metadata.Files[0].RelativePath);
        Assert.Equal("stages/layer-001.json", metadata.Files[1].RelativePath);
        Assert.Equal("layer-001", metadata.Files[1].Stage);
        Assert.Equal("8", metadata.CliOptions["threads"]);
    }

    // JSON 出力に主要フィールドが含まれることを確認
    [Fact]
    public void ToJson_ProducesExpectedStructure()
    {
        var builder = new DebugMetadataBuilder("1.0");
        builder.SetCreatedAt(new DateTimeOffset(2025, 10, 21, 7, 45, 0, TimeSpan.Zero));
        builder.AddFile("pipeline", "pipeline.json", "application/json");

        var json = builder.Build().ToJson();

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("1.0", root.GetProperty("version").GetString());
        Assert.True(root.TryGetProperty("createdAt", out _));
        Assert.True(root.GetProperty("files").EnumerateArray().Any());
    }
}
