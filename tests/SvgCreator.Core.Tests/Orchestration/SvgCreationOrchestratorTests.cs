using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using SvgCreator.Core.Diagnostics;
using SvgCreator.Core.Models;
using SvgCreator.Core.Orchestration;
using SvgCreator.Core.Orchestration.Stages;

namespace SvgCreator.Core.Tests.Orchestration;

public sealed class SvgCreationOrchestratorTests
{
    [Fact]
    public async Task ExecuteAsync_WhenDebugEnabled_EmitsPipelineSnapshotAndReportsProgress()
    {
        var options = new SvgCreatorRunOptions(
            imagePath: "input/sample.png",
            outputDirectory: "out",
            cliOptionSnapshot: new Dictionary<string, string>
            {
                ["image"] = "input/sample.png",
                ["out"] = "out"
            })
        {
            EnableDebug = true,
            DebugDirectory = "out/debug"
        };

        var image = new ImageData(1, 1, PixelFormat.Rgb, new byte[] { 32, 64, 96 });
        var quantization = new QuantizationResult(
            image,
            ImmutableArray.Create(new RgbColor(10, 20, 30)),
            ImmutableArray.Create(0));

        var imageReader = new FakeImageReader(image);
        var quantizer = new FakeQuantizer(quantization);
        var progressEvents = new List<PipelineStageProgress>();
        var progress = new Progress<PipelineStageProgress>(progressEvents.Add);
        var debugSink = new RecordingDebugSink();
        var clock = new DateTimeOffset(2025, 10, 21, 9, 0, 0, TimeSpan.Zero);

        var orchestrator = new SvgCreationOrchestrator(
            new IPipelineStage[]
            {
                new ImageLoadingStage(),
                new QuantizationStage()
            },
            new PipelineDependencies(imageReader, quantizer),
            debugSink,
            progress,
            () => clock);

        var result = await orchestrator.ExecuteAsync(options, CancellationToken.None);

        Assert.True(imageReader.WasInvoked);
        Assert.True(quantizer.WasInvoked);

        Assert.Collection(
            progressEvents,
            e =>
            {
                Assert.Equal(PipelineStageNames.ImageLoading, e.StageName);
                Assert.Equal(PipelineStageStatus.Started, e.Status);
                Assert.Equal(1, e.StageIndex);
                Assert.Equal(2, e.TotalStages);
            },
            e =>
            {
                Assert.Equal(PipelineStageNames.ImageLoading, e.StageName);
                Assert.Equal(PipelineStageStatus.Completed, e.Status);
                Assert.Equal(1, e.StageIndex);
                Assert.Equal(2, e.TotalStages);
            },
            e =>
            {
                Assert.Equal(PipelineStageNames.Quantization, e.StageName);
                Assert.Equal(PipelineStageStatus.Started, e.Status);
                Assert.Equal(2, e.StageIndex);
                Assert.Equal(2, e.TotalStages);
            },
            e =>
            {
                Assert.Equal(PipelineStageNames.Quantization, e.StageName);
                Assert.Equal(PipelineStageStatus.Completed, e.Status);
                Assert.Equal(2, e.StageIndex);
                Assert.Equal(2, e.TotalStages);
            });

        var snapshotCall = Assert.Single(debugSink.SnapshotCalls);
        Assert.Equal(QuantizationStage.DebugStageName, snapshotCall.StageName);
        Assert.Equal(clock, snapshotCall.Context.CreatedAt);
        Assert.Equal("input/sample.png", snapshotCall.Context.CliOptions["image"]);
        Assert.Equal(1, snapshotCall.Snapshot.Palette.Count);
        Assert.Equal(1, snapshotCall.Snapshot.Image.Width);
        Assert.True(debugSink.CompleteCalled);

        Assert.NotNull(result);
        Assert.Same(quantization, result.Quantization);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDebugDisabled_SkipsDebugSinkCalls()
    {
        var options = new SvgCreatorRunOptions(
            imagePath: "input/sample.png",
            outputDirectory: "out",
            cliOptionSnapshot: new Dictionary<string, string>());

        var image = new ImageData(1, 1, PixelFormat.Rgb, new byte[] { 1, 2, 3 });
        var quantization = new QuantizationResult(
            image,
            ImmutableArray.Create(new RgbColor(0, 0, 0)),
            ImmutableArray.Create(0));

        var imageReader = new FakeImageReader(image);
        var quantizer = new FakeQuantizer(quantization);
        var debugSink = new RecordingDebugSink();

        var orchestrator = new SvgCreationOrchestrator(
            new IPipelineStage[]
            {
                new ImageLoadingStage(),
                new QuantizationStage()
            },
            new PipelineDependencies(imageReader, quantizer),
            debugSink,
            progress: null,
            clock: () => new DateTimeOffset(2025, 10, 21, 9, 30, 0, TimeSpan.Zero));

        await orchestrator.ExecuteAsync(options, CancellationToken.None);

        Assert.Empty(debugSink.SnapshotCalls);
        Assert.False(debugSink.CompleteCalled);
    }

    private sealed class FakeImageReader : IImageReader
    {
        private readonly ImageData _image;

        public FakeImageReader(ImageData image)
        {
            _image = image;
        }

        public bool WasInvoked { get; private set; }

        public Task<ImageData> ReadAsync(SvgCreatorRunOptions options, CancellationToken cancellationToken)
        {
            WasInvoked = true;
            return Task.FromResult(_image);
        }
    }

    private sealed class FakeQuantizer : IQuantizer
    {
        private readonly QuantizationResult _result;

        public FakeQuantizer(QuantizationResult result)
        {
            _result = result;
        }

        public bool WasInvoked { get; private set; }

        public Task<QuantizationResult> QuantizeAsync(
            ImageData image,
            SvgCreatorRunOptions options,
            CancellationToken cancellationToken)
        {
            WasInvoked = true;
            return Task.FromResult(_result);
        }
    }

    private sealed class RecordingDebugSink : IDebugSink
    {
        public List<(string StageName, DebugSnapshot Snapshot, DebugExecutionContext Context)> SnapshotCalls { get; } = new();

        public bool CompleteCalled { get; private set; }

        public Task WriteSnapshotAsync(string stageName, DebugSnapshot snapshot, DebugExecutionContext context, CancellationToken cancellationToken = default)
        {
            SnapshotCalls.Add((stageName, snapshot, context));
            return Task.CompletedTask;
        }

        public Task CompleteAsync(DebugExecutionContext context, CancellationToken cancellationToken = default)
        {
            CompleteCalled = true;
            return Task.CompletedTask;
        }
    }
}

