using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using SvgCreator.Core.Diagnostics;
using SvgCreator.Core.Models;
using SvgCreator.Core.Orchestration;
using SvgCreator.Core.Orchestration.Stages;
using SvgCreator.Core.ShapeLayers;

namespace SvgCreator.Core.Tests.Orchestration;

public sealed class SvgCreationOrchestratorTests
{
    [Fact]
    // デバッグを有効にした場合にパイプライン進捗とスナップショットが出力されることを確認する
    public async Task ExecuteAsync_WhenDebugEnabled_EmitsPipelineSnapshotAndReportsProgress()
    {
        // CLI オプションとデバッグ設定を含む実行オプションを準備
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

        // テスト用のダミー画像と量子化結果を構築
        var image = new ImageData(1, 1, PixelFormat.Rgb, new byte[] { 32, 64, 96 });
        var quantization = new QuantizationResult(
            image,
            ImmutableArray.Create(new RgbColor(10, 20, 30)),
            ImmutableArray.Create(0));

        var imageReader = new FakeImageReader(image);
        var quantizer = new FakeQuantizer(quantization);
        var progressEvents = new List<PipelineStageProgress>();
        var progress = new ImmediateProgress<PipelineStageProgress>(progressEvents.Add);
        var debugSink = new RecordingDebugSink();
        var clock = new DateTimeOffset(2025, 10, 21, 9, 0, 0, TimeSpan.Zero);
        var shapeLayerBuilder = new FakeShapeLayerBuilder(Array.Empty<ShapeLayer>());

        // 画像読み込みと量子化の 2 ステージを登録して実行
        var orchestrator = new SvgCreationOrchestrator(
            new IPipelineStage[]
            {
                new ImageLoadingStage(),
                new QuantizationStage(),
                new ShapeLayerExtractionStage()
            },
            new PipelineDependencies(imageReader, quantizer, shapeLayerBuilder),
            debugSink,
            progress,
            () => clock);

        var result = await orchestrator.ExecuteAsync(options, CancellationToken.None);

        Assert.True(imageReader.WasInvoked);
        Assert.True(quantizer.WasInvoked);

        // すべてのステージで開始・完了イベントが通知されたことを検証
        Assert.Collection(
            progressEvents,
            e =>
            {
                Assert.Equal(PipelineStageNames.ImageLoading, e.StageName);
                Assert.Equal(PipelineStageStatus.Started, e.Status);
                Assert.Equal(1, e.StageIndex);
                Assert.Equal(3, e.TotalStages);
            },
            e =>
            {
                Assert.Equal(PipelineStageNames.ImageLoading, e.StageName);
                Assert.Equal(PipelineStageStatus.Completed, e.Status);
                Assert.Equal(1, e.StageIndex);
                Assert.Equal(3, e.TotalStages);
            },
            e =>
            {
                Assert.Equal(PipelineStageNames.Quantization, e.StageName);
                Assert.Equal(PipelineStageStatus.Started, e.Status);
                Assert.Equal(2, e.StageIndex);
                Assert.Equal(3, e.TotalStages);
            },
            e =>
            {
                Assert.Equal(PipelineStageNames.Quantization, e.StageName);
                Assert.Equal(PipelineStageStatus.Completed, e.Status);
                Assert.Equal(2, e.StageIndex);
                Assert.Equal(3, e.TotalStages);
            },
            e =>
            {
                Assert.Equal(PipelineStageNames.ShapeLayerExtraction, e.StageName);
                Assert.Equal(PipelineStageStatus.Started, e.Status);
                Assert.Equal(3, e.StageIndex);
                Assert.Equal(3, e.TotalStages);
            },
            e =>
            {
                Assert.Equal(PipelineStageNames.ShapeLayerExtraction, e.StageName);
                Assert.Equal(PipelineStageStatus.Completed, e.Status);
                Assert.Equal(3, e.StageIndex);
                Assert.Equal(3, e.TotalStages);
            });

        // 量子化ステージのスナップショットが 1 回だけ記録されていることを確認
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
    // デバッグを無効にした場合はスナップショットが作成されないことを確認する
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
        var shapeLayerBuilder = new FakeShapeLayerBuilder(Array.Empty<ShapeLayer>());

        var orchestrator = new SvgCreationOrchestrator(
            new IPipelineStage[]
            {
                new ImageLoadingStage(),
                new QuantizationStage(),
                new ShapeLayerExtractionStage()
            },
            new PipelineDependencies(imageReader, quantizer, shapeLayerBuilder),
            debugSink,
            progress: null,
            clock: () => new DateTimeOffset(2025, 10, 21, 9, 30, 0, TimeSpan.Zero));

        await orchestrator.ExecuteAsync(options, CancellationToken.None);

        Assert.Empty(debugSink.SnapshotCalls);
        Assert.False(debugSink.CompleteCalled);
    }

    private sealed class FakeShapeLayerBuilder : IShapeLayerBuilder
    {
        private readonly IReadOnlyList<ShapeLayer> _layers;

        public FakeShapeLayerBuilder(IReadOnlyList<ShapeLayer> layers)
        {
            _layers = layers;
        }

        public Task<ShapeLayerExtractionResult> BuildLayersAsync(QuantizationResult quantization, CancellationToken cancellationToken)
            => Task.FromResult(new ShapeLayerExtractionResult(_layers, Array.Empty<NoisyLayer>()));
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

    private sealed class ImmediateProgress<T> : IProgress<T>
    {
        private readonly Action<T> _callback;

        public ImmediateProgress(Action<T> callback)
        {
            _callback = callback;
        }

        public void Report(T value) => _callback(value);
    }
}
