using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using SvgCreator.Core.Diagnostics;
using SvgCreator.Core.Models;
using SvgCreator.Core.Orchestration;
using Xunit;
using SystemPath = System.IO.Path;

namespace SvgCreator.Core.Tests.Diagnostics;

public sealed class DebugSinkFactoryTests : IAsyncLifetime
{
    private readonly string _tempRoot = SystemPath.Combine(SystemPath.GetTempPath(), Guid.NewGuid().ToString("N"));

    public Task InitializeAsync()
    {
        Directory.CreateDirectory(_tempRoot);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }

        return Task.CompletedTask;
    }

    // --debug 未指定の場合に Null シンクが選択されることを確認
    [Fact]
    public void Create_WhenDebugDisabled_ReturnsNullSink()
    {
        var options = new SvgCreatorRunOptions("input.png", _tempRoot)
        {
            EnableDebug = false
        };

        var sink = DebugSinkFactory.Create(options);

        Assert.IsType<NullDebugSink>(sink);
    }

    // 相対パスの --debug-dir とステージフィルタが適用されることを確認
    [Fact]
    public async Task Create_WithDebugOptions_ProducesFileSinkUsingResolvedDirectory()
    {
        var outputDirectory = SystemPath.Combine(_tempRoot, "out");
        Directory.CreateDirectory(outputDirectory);

        var options = new SvgCreatorRunOptions("input.png", outputDirectory)
        {
            EnableDebug = true,
            DebugDirectory = "diagnostics",
            DebugStages = new[] { "DepthOrdering" }
        };

        var sink = DebugSinkFactory.Create(options);
        var fileSink = Assert.IsType<FileDebugSink>(sink);

        var debugDirectory = SystemPath.Combine(outputDirectory, "diagnostics");
        var layout = new DebugDirectoryLayout(debugDirectory);
        var context = new DebugExecutionContext(DateTimeOffset.UtcNow, new Dictionary<string, string>());
        var snapshot = CreateDummySnapshot();

        await fileSink.WriteSnapshotAsync("DepthOrdering", snapshot, context);
        await fileSink.WriteSnapshotAsync("Quantizer", snapshot, context);
        await fileSink.CompleteAsync(context);

        Assert.True(File.Exists(layout.GetStageSnapshotPath("DepthOrdering")));
        Assert.False(File.Exists(layout.GetStageSnapshotPath("Quantizer")));
        Assert.Equal(SystemPath.Combine(outputDirectory, "diagnostics"), debugDirectory);
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

        return new DebugSnapshot
        {
            Version = DebugSnapshot.CurrentVersion,
            Image = image,
            Palette = Array.Empty<RgbColor>(),
            Layers = Array.Empty<DebugSnapshotLayer>()
        };
    }
}
