using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using SvgCreator.Core.DepthOrdering;
using SvgCreator.Core.Models;
using SvgCreator.Core.Occlusion;
using SvgCreator.Core.Orchestration;
using SvgCreator.Core.Orchestration.Stages;
using SvgCreator.Core.ShapeLayers;

namespace SvgCreator.Core.Tests.Orchestration.Stages;

public sealed class OcclusionCompletionStageTests
{
    // 深度順序が未設定の場合は例外となることを確認
    [Fact]
    public async Task ExecuteAsync_WhenDepthOrderMissing_ThrowsInvalidOperation()
    {
        var stage = new OcclusionCompletionStage();
        var options = new SvgCreatorRunOptions("input.png", "out");
        var context = new PipelineContext(options);
        context.SetShapeLayerExtractionResult(new ShapeLayerExtractionResult(Array.Empty<ShapeLayer>(), Array.Empty<NoisyLayer>()));

        var dependencies = CreateDependencies(new StubOcclusionCompleter());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            stage.ExecuteAsync(context, dependencies, CancellationToken.None));
    }

    // 成功時に完了済みシェイプレイヤーがコンテキストへ保存されることを確認
    [Fact]
    public async Task ExecuteAsync_WithValidContext_StoresCompletedLayers()
    {
        var options = new SvgCreatorRunOptions("input.png", "out");
        var context = new PipelineContext(options);

        var layer = CreateShapeLayer();
        context.SetShapeLayerExtractionResult(new ShapeLayerExtractionResult(new[] { layer }, Array.Empty<NoisyLayer>()));
        context.SetDepthOrder(new DepthOrder(new Dictionary<string, int> { ["layer-0001"] = 0 }));

        var completedLayer = new ShapeLayer(
            layer.Id,
            layer.Color,
            layer.Mask,
            ImmutableArray.CreateRange(layer.Boundary),
            layer.Holes,
            layer.Area);

        var completer = new StubOcclusionCompleter(new OcclusionCompletionResult(new[] { completedLayer }));
        var dependencies = CreateDependencies(completer);
        var stage = new OcclusionCompletionStage();

        await stage.ExecuteAsync(context, dependencies, CancellationToken.None);

        var stored = Assert.Single(context.CompletedLayers);
        Assert.Equal(completedLayer.Id, stored.Id);
    }

    private static PipelineDependencies CreateDependencies(IOcclusionCompleter occlusionCompleter)
        => new PipelineDependencies(
            new DummyImageReader(),
            new DummyQuantizer(),
            new DummyShapeLayerBuilder(),
            new DummyDepthOrderingService(),
            occlusionCompleter);

    private static ShapeLayer CreateShapeLayer()
    {
        var boundary = ImmutableArray.Create(
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f));

        var mask = new RasterMask(2, 2, ImmutableArray.Create(true, true, true, true));
        return new ShapeLayer("layer-0001", new RgbColor(1, 2, 3), mask, boundary, ImmutableArray<IImmutableList<Vector2>>.Empty, 3);
    }

    private sealed class StubOcclusionCompleter : IOcclusionCompleter
    {
        private readonly OcclusionCompletionResult _result;

        public StubOcclusionCompleter(OcclusionCompletionResult? result = null)
        {
            _result = result ?? new OcclusionCompletionResult(Array.Empty<ShapeLayer>());
        }

        public Task<OcclusionCompletionResult> CompleteAsync(
            IReadOnlyList<ShapeLayer> layers,
            DepthOrder depthOrder,
            OcclusionCompletionOptions options,
            CancellationToken cancellationToken)
            => Task.FromResult(_result);
    }

    private sealed class DummyImageReader : IImageReader
    {
        public Task<ImageData> ReadAsync(SvgCreatorRunOptions options, CancellationToken cancellationToken)
            => Task.FromResult(new ImageData(1, 1, PixelFormat.Rgb, new byte[] { 0, 0, 0 }));
    }

    private sealed class DummyQuantizer : IQuantizer
    {
        public Task<QuantizationResult> QuantizeAsync(ImageData image, SvgCreatorRunOptions options, CancellationToken cancellationToken)
            => Task.FromResult(new QuantizationResult(image, ImmutableArray<RgbColor>.Empty, ImmutableArray<int>.Empty));
    }

    private sealed class DummyShapeLayerBuilder : IShapeLayerBuilder
    {
        public Task<ShapeLayerExtractionResult> BuildLayersAsync(QuantizationResult quantization, CancellationToken cancellationToken)
            => Task.FromResult(new ShapeLayerExtractionResult(Array.Empty<ShapeLayer>(), Array.Empty<NoisyLayer>()));
    }

    private sealed class DummyDepthOrderingService : IDepthOrderingService
    {
        public DepthOrder Compute(IReadOnlyList<ShapeLayer> layers, DepthOrderingOptions options)
            => new DepthOrder(new Dictionary<string, int>());
    }
}
