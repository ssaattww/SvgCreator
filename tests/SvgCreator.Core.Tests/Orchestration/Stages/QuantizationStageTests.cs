using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OpenCvSharp;
using SvgCreator.Core.DepthOrdering;
using IOPath = System.IO.Path;
using SvgCreator.Core.Models;
using SvgCreator.Core.Occlusion;
using SvgCreator.Core.Orchestration;
using SvgCreator.Core.Orchestration.Stages;
using SvgCreator.Core.ShapeLayers;

namespace SvgCreator.Core.Tests.Orchestration.Stages;

public sealed class QuantizationStageTests
{
    // 2値化画像の書き出しが有効な場合、quantization 結果から PNG が生成されることを確認
    [Fact]
    public async Task ExecuteAsync_WhenBinarizedOutputConfigured_WritesBinaryImage()
    {
        using var temp = new TempDirectory();
        var image = new ImageData(2, 1, PixelFormat.Rgb, new byte[] { 10, 10, 10, 250, 250, 250 });
        var quantization = new QuantizationResult(
            image,
            ImmutableArray.Create(new RgbColor(20, 20, 20), new RgbColor(240, 240, 240)),
            ImmutableArray.Create(0, 1));

        var options = new SvgCreatorRunOptions("input.png", temp.Path)
        {
            BinarizedImageOutputName = "binarized.png"
        };

        var context = new PipelineContext(options);
        context.SetImage(image);

        var stage = new QuantizationStage();
        var dependencies = CreateDependencies(new StubQuantizer(quantization));

        await stage.ExecuteAsync(context, dependencies, CancellationToken.None);

        var outputPath = IOPath.Combine(temp.Path, "binarized.png");
        Assert.True(File.Exists(outputPath));

        using var mat = Cv2.ImRead(outputPath, ImreadModes.Grayscale);
        Assert.Equal(2, mat.Cols);
        Assert.Equal(1, mat.Rows);

        var left = mat.Get<byte>(0, 0);
        var right = mat.Get<byte>(0, 1);

        Assert.Equal(0, left);
        Assert.Equal(255, right);
    }

    private static PipelineDependencies CreateDependencies(IQuantizer quantizer)
    {
        return new PipelineDependencies(
            new StubImageReader(),
            quantizer,
            new StubShapeLayerBuilder(),
            new StubDepthOrderingService(),
            new StubOcclusionCompleter());
    }

    private sealed class StubImageReader : IImageReader
    {
        public Task<ImageData> ReadAsync(SvgCreatorRunOptions options, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class StubShapeLayerBuilder : IShapeLayerBuilder
    {
        public Task<ShapeLayerExtractionResult> BuildLayersAsync(QuantizationResult quantization, CancellationToken cancellationToken)
            => Task.FromResult(new ShapeLayerExtractionResult(Array.Empty<ShapeLayer>(), Array.Empty<NoisyLayer>()));
    }

    private sealed class StubDepthOrderingService : IDepthOrderingService
    {
        public DepthOrder Compute(IReadOnlyList<ShapeLayer> layers, DepthOrderingOptions options)
            => new DepthOrder(new Dictionary<string, int>());
    }

    private sealed class StubOcclusionCompleter : IOcclusionCompleter
    {
        public Task<OcclusionCompletionResult> CompleteAsync(IReadOnlyList<ShapeLayer> layers, DepthOrder depthOrder, OcclusionCompletionOptions options, CancellationToken cancellationToken)
            => Task.FromResult(new OcclusionCompletionResult(Array.Empty<ShapeLayer>()));
    }

    private sealed class StubQuantizer : IQuantizer
    {
        private readonly QuantizationResult _result;

        public StubQuantizer(QuantizationResult result)
        {
            _result = result;
        }

        public Task<QuantizationResult> QuantizeAsync(ImageData image, SvgCreatorRunOptions options, CancellationToken cancellationToken)
            => Task.FromResult(_result);
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = IOPath.Combine(System.IO.Path.GetTempPath(), "svgcreator-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, recursive: true);
                }
            }
            catch
            {
                // テスト終了時のクリーンアップで発生した例外は無視する。
            }
        }
    }
}
