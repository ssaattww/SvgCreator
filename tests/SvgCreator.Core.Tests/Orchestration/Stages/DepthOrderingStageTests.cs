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

public sealed class DepthOrderingStageTests
{
    // シェイプレイヤーが未設定の場合は例外を送出することを確認
    [Fact]
    public async Task ExecuteAsync_WhenShapeLayersMissing_ThrowsInvalidOperation()
    {
        var stage = new DepthOrderingStage();
        var options = new SvgCreatorRunOptions("input.png", "out");
        var context = new PipelineContext(options);
        var dependencies = CreateDependencies(new StubDepthOrderingService());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            stage.ExecuteAsync(context, dependencies, CancellationToken.None));
    }

    // 深度順序がコンテキストへ書き込まれることを確認
    [Fact]
    public async Task ExecuteAsync_WithShapeLayers_SetsDepthOrder()
    {
        var layers = ImmutableArray.Create(
            CreateRectangleLayer("layer-background", 0, 0, 4, 4, 4, 4),
            CreateRectangleLayer("layer-foreground", 1, 1, 2, 2, 4, 4));

        var depthOrder = new DepthOrder(new Dictionary<string, int>
        {
            ["layer-background"] = 0,
            ["layer-foreground"] = 1
        });

        var stage = new DepthOrderingStage();
        var options = new SvgCreatorRunOptions("input.png", "out");
        var context = new PipelineContext(options);
        context.SetShapeLayerExtractionResult(new ShapeLayerExtractionResult(layers, Array.Empty<NoisyLayer>()));

        var dependencies = CreateDependencies(new StubDepthOrderingService(depthOrder));

        await stage.ExecuteAsync(context, dependencies, CancellationToken.None);

        Assert.Same(depthOrder, context.DepthOrder);
    }

    private static PipelineDependencies CreateDependencies(IDepthOrderingService depthOrdering)
    {
        return new PipelineDependencies(
            new StubImageReader(),
            new StubQuantizer(),
            new StubShapeLayerBuilder(),
            depthOrdering,
            new StubOcclusionCompleter());
    }

    private static ShapeLayer CreateRectangleLayer(
        string id,
        int minX,
        int minY,
        int width,
        int height,
        int maskWidth,
        int maskHeight)
    {
        var bits = ImmutableArray.CreateBuilder<bool>(maskWidth * maskHeight);
        bits.Count = maskWidth * maskHeight;

        var maxX = minX + width - 1;
        var maxY = minY + height - 1;
        var area = 0;

        for (var y = 0; y < maskHeight; y++)
        {
            for (var x = 0; x < maskWidth; x++)
            {
                var inside = x >= minX && x <= maxX && y >= minY && y <= maxY;
                bits[y * maskWidth + x] = inside;
                if (inside)
                {
                    area++;
                }
            }
        }

        var boundary = ImmutableArray.Create(
            new Vector2(minX, minY),
            new Vector2(maxX + 1, minY),
            new Vector2(maxX + 1, maxY + 1),
            new Vector2(minX, maxY + 1));

        var mask = new RasterMask(maskWidth, maskHeight, bits.MoveToImmutable());
        return new ShapeLayer(id, new RgbColor(0, 0, 0), mask, boundary, ImmutableArray<IImmutableList<Vector2>>.Empty, area);
    }

    private sealed class StubDepthOrderingService : IDepthOrderingService
    {
        private readonly DepthOrder? _result;

        public StubDepthOrderingService(DepthOrder? result = null)
        {
            _result = result;
        }

        public DepthOrder Compute(IReadOnlyList<ShapeLayer> layers, DepthOrderingOptions options)
        {
            if (_result is null)
            {
                throw new InvalidOperationException("No depth order provided.");
            }

            return _result;
        }
    }

    private sealed class StubImageReader : IImageReader
    {
        public Task<ImageData> ReadAsync(SvgCreatorRunOptions options, CancellationToken cancellationToken)
            => Task.FromResult(new ImageData(1, 1, PixelFormat.Rgb, new byte[] { 0, 0, 0 }));
    }

    private sealed class StubQuantizer : IQuantizer
    {
        public Task<QuantizationResult> QuantizeAsync(ImageData image, SvgCreatorRunOptions options, CancellationToken cancellationToken)
            => Task.FromResult(new QuantizationResult(image, ImmutableArray<RgbColor>.Empty, ImmutableArray<int>.Empty));
    }

    private sealed class StubShapeLayerBuilder : IShapeLayerBuilder
    {
        public Task<ShapeLayerExtractionResult> BuildLayersAsync(QuantizationResult quantization, CancellationToken cancellationToken)
            => Task.FromResult(new ShapeLayerExtractionResult(Array.Empty<ShapeLayer>(), Array.Empty<NoisyLayer>()));
    }

    private sealed class StubOcclusionCompleter : IOcclusionCompleter
    {
        public Task<OcclusionCompletionResult> CompleteAsync(IReadOnlyList<ShapeLayer> layers, DepthOrder depthOrder, OcclusionCompletionOptions options, CancellationToken cancellationToken)
            => Task.FromResult(new OcclusionCompletionResult(Array.Empty<ShapeLayer>()));
    }
}
