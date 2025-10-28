using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Numerics;
using SvgCreator.Core.DepthOrdering;
using SvgCreator.Core.Diagnostics;
using SvgCreator.Core.Models;
using SvgCreator.Core.Occlusion;
using SvgCreator.Core.Orchestration;
using SvgCreator.Core.Orchestration.Stages;
using SvgCreator.Core.ShapeLayers;

namespace SvgCreator.Core.Tests.Orchestration;

public sealed class SvgCreationOrchestratorTests
{
    // デバッグを有効にした場合にパイプライン進捗とスナップショットが出力されることを確認する
    [Fact]
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
        var layers = ImmutableArray.Create(CreateShapeLayer("layer-0001"));
        var depthOrder = new DepthOrder(new Dictionary<string, int>
        {
            ["layer-0001"] = 0
        });

        var shapeLayerBuilder = new FakeShapeLayerBuilder(layers);
        var depthOrdering = new FakeDepthOrderingService(depthOrder);
        var occlusionCompleter = new FakeOcclusionCompleter(layers);

        // パイプラインの主要ステージを登録して実行
        var orchestrator = new SvgCreationOrchestrator(
            new IPipelineStage[]
            {
                new ImageLoadingStage(),
                new QuantizationStage(),
                new ShapeLayerExtractionStage(),
                new DepthOrderingStage(),
                new OcclusionCompletionStage()
            },
            new PipelineDependencies(imageReader, quantizer, shapeLayerBuilder, depthOrdering, occlusionCompleter),
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
                Assert.Equal(5, e.TotalStages);
            },
            e =>
            {
                Assert.Equal(PipelineStageNames.ImageLoading, e.StageName);
                Assert.Equal(PipelineStageStatus.Completed, e.Status);
                Assert.Equal(1, e.StageIndex);
                Assert.Equal(5, e.TotalStages);
            },
            e =>
            {
                Assert.Equal(PipelineStageNames.Quantization, e.StageName);
                Assert.Equal(PipelineStageStatus.Started, e.Status);
                Assert.Equal(2, e.StageIndex);
                Assert.Equal(5, e.TotalStages);
            },
            e =>
            {
                Assert.Equal(PipelineStageNames.Quantization, e.StageName);
                Assert.Equal(PipelineStageStatus.Completed, e.Status);
                Assert.Equal(2, e.StageIndex);
                Assert.Equal(5, e.TotalStages);
            },
            e =>
            {
                Assert.Equal(PipelineStageNames.ShapeLayerExtraction, e.StageName);
                Assert.Equal(PipelineStageStatus.Started, e.Status);
                Assert.Equal(3, e.StageIndex);
                Assert.Equal(5, e.TotalStages);
            },
            e =>
            {
                Assert.Equal(PipelineStageNames.ShapeLayerExtraction, e.StageName);
                Assert.Equal(PipelineStageStatus.Completed, e.Status);
                Assert.Equal(3, e.StageIndex);
                Assert.Equal(5, e.TotalStages);
            },
            e =>
            {
                Assert.Equal(PipelineStageNames.DepthOrdering, e.StageName);
                Assert.Equal(PipelineStageStatus.Started, e.Status);
                Assert.Equal(4, e.StageIndex);
                Assert.Equal(5, e.TotalStages);
            },
            e =>
            {
                Assert.Equal(PipelineStageNames.DepthOrdering, e.StageName);
                Assert.Equal(PipelineStageStatus.Completed, e.Status);
                Assert.Equal(4, e.StageIndex);
                Assert.Equal(5, e.TotalStages);
            },
            e =>
            {
                Assert.Equal(PipelineStageNames.OcclusionCompletion, e.StageName);
                Assert.Equal(PipelineStageStatus.Started, e.Status);
                Assert.Equal(5, e.StageIndex);
                Assert.Equal(5, e.TotalStages);
            },
            e =>
            {
                Assert.Equal(PipelineStageNames.OcclusionCompletion, e.StageName);
                Assert.Equal(PipelineStageStatus.Completed, e.Status);
                Assert.Equal(5, e.StageIndex);
                Assert.Equal(5, e.TotalStages);
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
        Assert.Same(depthOrder, result.DepthOrder);
        Assert.True(depthOrdering.WasInvoked);
        Assert.True(occlusionCompleter.WasInvoked);
    }

    // デバッグを無効にした場合はスナップショットが作成されないことを確認する
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
        var layers = ImmutableArray.Create(CreateShapeLayer("layer-0001"));
        var depthOrder = new DepthOrder(new Dictionary<string, int>
        {
            ["layer-0001"] = 0
        });

        var shapeLayerBuilder = new FakeShapeLayerBuilder(layers);
        var depthOrdering = new FakeDepthOrderingService(depthOrder);
        var occlusionCompleter = new FakeOcclusionCompleter(layers);

        var orchestrator = new SvgCreationOrchestrator(
            new IPipelineStage[]
            {
                new ImageLoadingStage(),
                new QuantizationStage(),
                new ShapeLayerExtractionStage(),
                new DepthOrderingStage(),
                new OcclusionCompletionStage()
            },
            new PipelineDependencies(imageReader, quantizer, shapeLayerBuilder, depthOrdering, occlusionCompleter),
            debugSink,
            progress: null,
            clock: () => new DateTimeOffset(2025, 10, 21, 9, 30, 0, TimeSpan.Zero));

        await orchestrator.ExecuteAsync(options, CancellationToken.None);

        Assert.Empty(debugSink.SnapshotCalls);
        Assert.False(debugSink.CompleteCalled);
        Assert.True(depthOrdering.WasInvoked);
        Assert.True(occlusionCompleter.WasInvoked);
    }

    // 入力読み込み失敗時にカタログ化された例外へ変換されることを確認する
    [Fact]
    public async Task ExecuteAsync_WhenImageReaderThrowsFileNotFound_ThrowsSvgCreatorException()
    {
        var options = new SvgCreatorRunOptions(
            imagePath: "missing.png",
            outputDirectory: "out",
            cliOptionSnapshot: new Dictionary<string, string>());

        var imageReader = new ThrowingImageReader(new FileNotFoundException("missing.png"));
        var quantizer = new PassthroughQuantizer();
        var shapeLayerBuilder = new PassthroughShapeLayerBuilder();
        var depthOrdering = new PassthroughDepthOrderingService();
        var occlusionCompleter = new PassthroughOcclusionCompleter();

        var orchestrator = new SvgCreationOrchestrator(
            new IPipelineStage[]
            {
                new ImageLoadingStage(),
                new QuantizationStage()
            },
            new PipelineDependencies(imageReader, quantizer, shapeLayerBuilder, depthOrdering, occlusionCompleter),
            new NullDebugSink());

        var exception = await Assert.ThrowsAsync<SvgCreatorException>(() => orchestrator.ExecuteAsync(options, CancellationToken.None));

        Assert.Equal(SvgCreatorErrorCode.InputFileNotFound, exception.ErrorCode);
        Assert.Equal(SvgCreatorErrorCategory.Input, exception.Category);
        Assert.Contains("missing.png", exception.Message);
    }

    // デバッグ出力処理の失敗がデバッグカテゴリの例外として表面化することを確認する
    [Fact]
    public async Task ExecuteAsync_WhenDebugSinkFails_ThrowsSvgCreatorException()
    {
        var options = new SvgCreatorRunOptions(
            imagePath: "input/sample.png",
            outputDirectory: "out",
            cliOptionSnapshot: new Dictionary<string, string>())
        {
            EnableDebug = true
        };

        var image = new ImageData(1, 1, PixelFormat.Rgb, new byte[] { 0, 0, 0 });
        var quantization = new QuantizationResult(
            image,
            ImmutableArray.Create(new RgbColor(0, 0, 0)),
            ImmutableArray.Create(0));
        var layer = CreateShapeLayer("layer-0001");
        var depthOrder = new DepthOrder(new Dictionary<string, int> { [layer.Id] = 0 });

        var stage = new DebugSnapshotStage(image, quantization, layer, depthOrder);

        var orchestrator = new SvgCreationOrchestrator(
            new IPipelineStage[] { stage },
            new PipelineDependencies(
                new PassthroughImageReader(image),
                new PassthroughQuantizer(quantization),
                new PassthroughShapeLayerBuilder(layer),
                new PassthroughDepthOrderingService(depthOrder),
                new PassthroughOcclusionCompleter(layer)),
            new ThrowingDebugSink(new IOException("debug directory locked")));

        var exception = await Assert.ThrowsAsync<SvgCreatorException>(() => orchestrator.ExecuteAsync(options, CancellationToken.None));

        Assert.Equal(SvgCreatorErrorCode.DebugWriteFailed, exception.ErrorCode);
        Assert.Equal(SvgCreatorErrorCategory.Debug, exception.Category);
        Assert.Contains("debug directory locked", exception.Message);
    }

    // 非対応形式による入力エラーが InputUnsupportedFormat コードへ分類されることを検証
    [Fact]
    public async Task ExecuteAsync_WhenImageStageThrowsNotSupported_ThrowsSvgCreatorException()
    {
        var options = new SvgCreatorRunOptions("input.bmp", "out", new Dictionary<string, string>());

        var orchestrator = new SvgCreationOrchestrator(
            new IPipelineStage[]
            {
                new FailingStage(PipelineStageNames.ImageLoading, new NotSupportedException("bmp"))
            },
            CreateNoOpDependencies(),
            new NullDebugSink());

        var exception = await Assert.ThrowsAsync<SvgCreatorException>(() => orchestrator.ExecuteAsync(options, CancellationToken.None));

        Assert.Equal(SvgCreatorErrorCode.InputUnsupportedFormat, exception.ErrorCode);
        Assert.Equal(SvgCreatorErrorCategory.Input, exception.Category);
        Assert.Contains("bmp", exception.Message);
    }

    // 画像デコード失敗が ImageDecodeFailed コードへ分類されることを検証
    [Fact]
    public async Task ExecuteAsync_WhenImageStageThrowsInvalidData_ThrowsSvgCreatorException()
    {
        var options = new SvgCreatorRunOptions("input.png", "out", new Dictionary<string, string>());

        var orchestrator = new SvgCreationOrchestrator(
            new IPipelineStage[]
            {
                new FailingStage(PipelineStageNames.ImageLoading, new InvalidDataException("corrupted"))
            },
            CreateNoOpDependencies(),
            new NullDebugSink());

        var exception = await Assert.ThrowsAsync<SvgCreatorException>(() => orchestrator.ExecuteAsync(options, CancellationToken.None));

        Assert.Equal(SvgCreatorErrorCode.ImageDecodeFailed, exception.ErrorCode);
        Assert.Equal(SvgCreatorErrorCategory.Input, exception.Category);
        Assert.Contains("corrupted", exception.Message);
    }

    // 深度整序ステージの一般的な失敗が DepthOrderingCyclicDependency コードになることを検証
    [Fact]
    public async Task ExecuteAsync_WhenDepthStageFails_ThrowsSvgCreatorException()
    {
        var options = new SvgCreatorRunOptions("input.png", "out", new Dictionary<string, string>());

        var orchestrator = new SvgCreationOrchestrator(
            new IPipelineStage[]
            {
                new FailingStage(PipelineStageNames.DepthOrdering, new InvalidOperationException("cycle"))
            },
            CreateNoOpDependencies(),
            new NullDebugSink());

        var exception = await Assert.ThrowsAsync<SvgCreatorException>(() => orchestrator.ExecuteAsync(options, CancellationToken.None));

        Assert.Equal(SvgCreatorErrorCode.DepthOrderingCyclicDependency, exception.ErrorCode);
        Assert.Equal(SvgCreatorErrorCategory.DepthOrdering, exception.Category);
        Assert.Contains("cycle", exception.Message);
    }

    private static ShapeLayer CreateShapeLayer(string id)
    {
        var mask = new RasterMask(1, 1, ImmutableArray.Create(true));
        var boundary = ImmutableArray.Create(
            new Vector2(0, 0),
            new Vector2(1, 0),
            new Vector2(1, 1),
            new Vector2(0, 1));

        return new ShapeLayer(
            id,
            new RgbColor(1, 1, 1),
            mask,
            boundary,
            ImmutableArray<IImmutableList<Vector2>>.Empty,
            area: 1);
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

    private sealed class FakeDepthOrderingService : IDepthOrderingService
    {
        private readonly DepthOrder _result;

        public FakeDepthOrderingService(DepthOrder result)
        {
            _result = result;
        }

        public bool WasInvoked { get; private set; }

        public DepthOrder Compute(IReadOnlyList<ShapeLayer> layers, DepthOrderingOptions options)
        {
            WasInvoked = true;
            return _result;
        }
    }

    private sealed class FakeOcclusionCompleter : IOcclusionCompleter
    {
        private readonly IReadOnlyList<ShapeLayer> _completedLayers;

        public FakeOcclusionCompleter(IReadOnlyList<ShapeLayer> completedLayers)
        {
            _completedLayers = completedLayers;
        }

        public bool WasInvoked { get; private set; }

        public Task<OcclusionCompletionResult> CompleteAsync(
            IReadOnlyList<ShapeLayer> layers,
            DepthOrder depthOrder,
            OcclusionCompletionOptions options,
            CancellationToken cancellationToken)
        {
            WasInvoked = true;
            return Task.FromResult(new OcclusionCompletionResult(_completedLayers));
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

    private sealed class NullDebugSink : IDebugSink
    {
        public Task WriteSnapshotAsync(string stageName, DebugSnapshot snapshot, DebugExecutionContext context, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task CompleteAsync(DebugExecutionContext context, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class PassthroughImageReader : IImageReader
    {
        private readonly ImageData _image;

        public PassthroughImageReader(ImageData image)
        {
            _image = image;
        }

        public Task<ImageData> ReadAsync(SvgCreatorRunOptions options, CancellationToken cancellationToken)
            => Task.FromResult(_image);
    }

    private sealed class PassthroughQuantizer : IQuantizer
    {
        private readonly QuantizationResult? _result;

        public PassthroughQuantizer()
        {
        }

        public PassthroughQuantizer(QuantizationResult result)
        {
            _result = result;
        }

        public Task<QuantizationResult> QuantizeAsync(ImageData image, SvgCreatorRunOptions options, CancellationToken cancellationToken)
        {
            if (_result is not null)
            {
                return Task.FromResult(_result);
            }

            return Task.FromResult(new QuantizationResult(
                image,
                ImmutableArray.Create(new RgbColor(0, 0, 0)),
                ImmutableArray.Create(0)));
        }
    }

    private sealed class PassthroughShapeLayerBuilder : IShapeLayerBuilder
    {
        private readonly IReadOnlyList<ShapeLayer>? _layers;

        public PassthroughShapeLayerBuilder()
        {
        }

        public PassthroughShapeLayerBuilder(ShapeLayer layer)
        {
            _layers = new[] { layer };
        }

        public Task<ShapeLayerExtractionResult> BuildLayersAsync(QuantizationResult quantization, CancellationToken cancellationToken)
        {
            var layers = _layers ?? new[] { CreateShapeLayer("layer-auto") };
            return Task.FromResult(new ShapeLayerExtractionResult(layers, Array.Empty<NoisyLayer>()));
        }
    }

    private sealed class PassthroughDepthOrderingService : IDepthOrderingService
    {
        private readonly DepthOrder? _depthOrder;

        public PassthroughDepthOrderingService()
        {
        }

        public PassthroughDepthOrderingService(DepthOrder depthOrder)
        {
            _depthOrder = depthOrder;
        }

        public DepthOrder Compute(IReadOnlyList<ShapeLayer> layers, DepthOrderingOptions options)
        {
            if (_depthOrder is not null)
            {
                return _depthOrder;
            }

            return new DepthOrder(new Dictionary<string, int> { [layers[0].Id] = 0 });
        }
    }

    private sealed class PassthroughOcclusionCompleter : IOcclusionCompleter
    {
        private readonly IReadOnlyList<ShapeLayer>? _layers;

        public PassthroughOcclusionCompleter()
        {
        }

        public PassthroughOcclusionCompleter(ShapeLayer layer)
        {
            _layers = new[] { layer };
        }

        public Task<OcclusionCompletionResult> CompleteAsync(
            IReadOnlyList<ShapeLayer> layers,
            DepthOrder depthOrder,
            OcclusionCompletionOptions options,
            CancellationToken cancellationToken)
        {
            var completed = _layers ?? layers;
            return Task.FromResult(new OcclusionCompletionResult(completed));
        }
    }

    private sealed class ThrowingImageReader : IImageReader
    {
        private readonly Exception _exception;

        public ThrowingImageReader(Exception exception)
        {
            _exception = exception;
        }

        public Task<ImageData> ReadAsync(SvgCreatorRunOptions options, CancellationToken cancellationToken)
            => Task.FromException<ImageData>(_exception);
    }

    private sealed class ThrowingDebugSink : IDebugSink
    {
        private readonly Exception _exception;

        public ThrowingDebugSink(Exception exception)
        {
            _exception = exception;
        }

        public Task WriteSnapshotAsync(string stageName, DebugSnapshot snapshot, DebugExecutionContext context, CancellationToken cancellationToken = default)
            => Task.FromException(_exception);

        public Task CompleteAsync(DebugExecutionContext context, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class DebugSnapshotStage : IPipelineStage
    {
        private readonly ImageData _image;
        private readonly QuantizationResult _quantization;
        private readonly ShapeLayer _layer;
        private readonly DepthOrder _depthOrder;

        public DebugSnapshotStage(ImageData image, QuantizationResult quantization, ShapeLayer layer, DepthOrder depthOrder)
        {
            _image = image;
            _quantization = quantization;
            _layer = layer;
            _depthOrder = depthOrder;
        }

        public string Name => "debug";

        public string DisplayName => "Debug";

        public string? DebugStageName => "debug-stage";

        public Task ExecuteAsync(PipelineContext context, PipelineDependencies dependencies, CancellationToken cancellationToken)
        {
            context.SetImage(_image);
            context.SetQuantization(_quantization);
            context.SetShapeLayerExtractionResult(new ShapeLayerExtractionResult(new[] { _layer }, Array.Empty<NoisyLayer>()));
            context.SetDepthOrder(_depthOrder);
            context.SetCompletedLayers(new[] { _layer });
            return Task.CompletedTask;
        }

        public DebugSnapshot? CreateDebugSnapshot(PipelineContext context)
        {
            return DebugSnapshot.From(_quantization, new[] { _layer });
        }
    }

    private sealed class FailingStage : IPipelineStage
    {
        private readonly string _name;
        private readonly Exception _exception;

        public FailingStage(string name, Exception exception)
        {
            _name = name;
            _exception = exception;
        }

        public string Name => _name;

        public string DisplayName => _name;

        public string? DebugStageName => null;

        public Task ExecuteAsync(PipelineContext context, PipelineDependencies dependencies, CancellationToken cancellationToken)
            => Task.FromException(_exception);

        public DebugSnapshot? CreateDebugSnapshot(PipelineContext context) => null;
    }

    private static PipelineDependencies CreateNoOpDependencies()
    {
        var image = new ImageData(1, 1, PixelFormat.Rgb, new byte[] { 0, 0, 0 });
        var quantization = new QuantizationResult(
            image,
            ImmutableArray.Create(new RgbColor(0, 0, 0)),
            ImmutableArray.Create(0));
        var layer = CreateShapeLayer("noop-layer");
        var depthOrder = new DepthOrder(new Dictionary<string, int> { [layer.Id] = 0 });

        return new PipelineDependencies(
            new PassthroughImageReader(image),
            new PassthroughQuantizer(quantization),
            new PassthroughShapeLayerBuilder(layer),
            new PassthroughDepthOrderingService(depthOrder),
            new PassthroughOcclusionCompleter(layer));
    }
}
