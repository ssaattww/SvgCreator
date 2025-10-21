using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using SvgCreator.Core.Diagnostics;
using SvgCreator.Core.Models;
using SystemPath = System.IO.Path;

namespace SvgCreator.Core.Tests.Diagnostics;

public sealed class FileDebugSinkTests : IAsyncLifetime
{
    private readonly string _tempDir = SystemPath.Combine(SystemPath.GetTempPath(), Guid.NewGuid().ToString("N"));

    public Task InitializeAsync()
    {
        Directory.CreateDirectory(_tempDir);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
        return Task.CompletedTask;
    }

    [Fact]
    // パイプラインおよびステージスナップショットが出力されることを確認
    public async Task WriteSnapshotAsync_WritesFilesAndMetadata()
    {
        var layout = new DebugDirectoryLayout(_tempDir);
        var serializer = new DebugSnapshotSerializer();
        var metadataBuilder = new DebugMetadataBuilder(DebugSnapshot.CurrentVersion);

        var sink = new FileDebugSink(layout, serializer, metadataBuilder, enabledStages: new[] { "Quantizer" });

        var context = new DebugExecutionContext(
            DateTimeOffset.UtcNow,
            new Dictionary<string, string> { ["threads"] = "8" });

        var snapshot = CreateDummySnapshot();

        await sink.WriteSnapshotAsync("pipeline", snapshot, context);
        await sink.WriteSnapshotAsync("Quantizer", snapshot, context);
        await sink.WriteSnapshotAsync("DepthOrdering", snapshot, context); // フィルタにより無視
        await sink.CompleteAsync(context);

        Assert.True(File.Exists(layout.PipelineSnapshotPath));
        Assert.True(File.Exists(layout.GetStageSnapshotPath("Quantizer")));
        Assert.False(File.Exists(layout.GetStageSnapshotPath("DepthOrdering")));

        var metadataJson = await File.ReadAllTextAsync(layout.MetadataPath);
        using var doc = JsonDocument.Parse(metadataJson);
        var files = doc.RootElement.GetProperty("files");
        Assert.Equal(2, files.GetArrayLength());
    }

    private static DebugSnapshot CreateDummySnapshot()
    {
        var image = new DebugSnapshotImage
        {
            Width = 1,
            Height = 1,
            Format = PixelFormat.Rgb,
            Pixels = new byte[] { 0, 0, 0 }
        };

        var layer = new DebugSnapshotLayer
        {
            Id = "layer-1",
            Color = new RgbColor(1, 2, 3),
            Area = 1,
            Mask = new DebugSnapshotMask
            {
                Width = 1,
                Height = 1,
                Bits = new[] { true }
            },
            Boundary = new[] { new System.Numerics.Vector2(0, 0), new(1, 0), new(0, 1) },
            Holes = Array.Empty<IReadOnlyList<System.Numerics.Vector2>>()
        };

        return new DebugSnapshot
        {
            Version = DebugSnapshot.CurrentVersion,
            Image = image,
            Palette = new[] { new RgbColor(1, 2, 3) },
            Layers = new[] { layer }
        };
    }
}
