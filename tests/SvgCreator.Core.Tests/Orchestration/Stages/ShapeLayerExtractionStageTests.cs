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

public sealed class ShapeLayerExtractionStageTests
{
    // 量子化結果からシェイプレイヤーを構築し、パイプラインコンテキストへ保存することを確認
    [Fact]
    public async Task ExecuteAsync_WithQuantization_BuildsAndStoresShapeLayers()
    {
        var image = new ImageData(1, 1, PixelFormat.Rgb, new byte[] { 1, 2, 3 });
        var quantization = new QuantizationResult(
            image,
            ImmutableArray.Create(new RgbColor(1, 2, 3)),
            ImmutableArray.Create(0));

        var context = new PipelineContext(new SvgCreatorRunOptions("input.png", "out"));
        context.SetImage(image);
        context.SetQuantization(quantization);

        var layers = new List<ShapeLayer>
        {
            new ShapeLayer(
                id: "layer-0001",
                color: new RgbColor(1, 2, 3),
                mask: new RasterMask(1, 1, ImmutableArray.Create(true)),
                boundary: ImmutableArray.Create(new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1)),
                holes: ImmutableArray<IImmutableList<Vector2>>.Empty,
                area: 1)
        };

        var builder = new FakeShapeLayerBuilder(layers);
        var stage = new ShapeLayerExtractionStage();
        var dependencies = new PipelineDependencies(new DummyImageReader(image), new DummyQuantizer(quantization), builder, new StubDepthOrderingService(), new StubOcclusionCompleter());

        await stage.ExecuteAsync(context, dependencies, CancellationToken.None);

        Assert.Same(layers, context.ShapeLayers);
        Assert.Empty(context.NoisyLayers);
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

    private sealed class DummyImageReader : IImageReader
    {
        private readonly ImageData _image;

        public DummyImageReader(ImageData image) => _image = image;

        public Task<ImageData> ReadAsync(SvgCreatorRunOptions options, CancellationToken cancellationToken)
            => Task.FromResult(_image);
    }

    private sealed class DummyQuantizer : IQuantizer
    {
        private readonly QuantizationResult _result;

        public DummyQuantizer(QuantizationResult result) => _result = result;

        public Task<QuantizationResult> QuantizeAsync(ImageData image, SvgCreatorRunOptions options, CancellationToken cancellationToken)
            => Task.FromResult(_result);
    }

    private sealed class StubDepthOrderingService : IDepthOrderingService
    {
        public DepthOrder Compute(IReadOnlyList<ShapeLayer> layers, DepthOrderingOptions options)
            => throw new NotSupportedException("Depth ordering is not required for this test.");
    }

    private sealed class StubOcclusionCompleter : IOcclusionCompleter
    {
        public Task<OcclusionCompletionResult> CompleteAsync(IReadOnlyList<ShapeLayer> layers, DepthOrder depthOrder, OcclusionCompletionOptions options, CancellationToken cancellationToken)
            => Task.FromResult(new OcclusionCompletionResult(layers));
    }
}
