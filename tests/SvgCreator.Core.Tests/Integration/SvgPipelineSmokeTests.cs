// Smoke test usage:
// 1. 既定入力: tests/fixtures/smoke-input.jpg（存在しなければ自動生成）
// 2. 任意入力/出力: SvgSmokeFixtures.Create("/path/to/input.jpg", "/path/to/output-dir") を利用
//    - 入力 JPEG は拡張子 .jpg/.jpeg の実ファイル
//    - 出力ディレクトリは存在しなくても可（自動で作成）
// 3. 実行後、出力先/<run-id>/ 以下に combined SVG と各 layer SVG が生成される
// 4. `dotnet test` 実行時に環境による差分が出ないよう、tests/.gitignore で成果物を除外

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SvgCreator.Core.DepthOrdering;
using SvgCreator.Core.Diagnostics;
using SvgCreator.Core.Models;
using SvgCreator.Core.Orchestration;
using SvgCreator.Core.Orchestration.Stages;
using SvgCreator.Core.ShapeLayers;
using SvgCreator.Core.Occlusion;
using SvgCreator.Core.Svg;
using Xunit;
using Xunit.Abstractions;
using IOPath = System.IO.Path;
using SvgPath = SvgCreator.Core.Models.Path;

namespace SvgCreator.Core.Tests.Integration;

public sealed class SvgPipelineSmokeTests
{
    private readonly ITestOutputHelper _output;

    public SvgPipelineSmokeTests(ITestOutputHelper output)
    {
        _output = output;
    }

    // Orchestrator と SVG 出力コンポーネントの組み合わせで成果物が生成されることを検証
    [Fact]
    public async Task ExecutePipelineSmokeTest_WritesArtifactsUnderSizeLimit()
    {
        // tests/fixtures/smoke-input.jpg を差し替えるか Create("<path>") で任意の JPEG を利用できる
        var fixture = SvgSmokeFixtures.Create();
        var report = await fixture.RunAsync(_output, CancellationToken.None);

        Assert.NotNull(report);
        Assert.True(report.LayerDocuments.Length > 0);
        Assert.True(File.Exists(report.CombinedSvgPath));

        foreach (var layerDoc in report.LayerDocuments)
        {
            Assert.True(File.Exists(layerDoc.FilePath));
            Assert.InRange(layerDoc.ByteCount, 1, SvgSmokeFixtures.MaximumLayerBytes);
        }
    }
}

internal sealed class SvgSmokeFixtures
{
    public const int MaximumLayerBytes = 15_360;

    private readonly SvgCreatorRunOptions _options;
    private readonly IReadOnlyList<IPipelineStage> _stages;
    private readonly PipelineDependencies _dependencies;
    private readonly ImmutableArray<ShapeLayer> _shapeLayers;
    private readonly ImmutableArray<LayerPathGeometry> _geometries;
    private readonly DepthOrder _depthOrder;
    private readonly ImageData _image;
    private readonly SvgEmitterOptions _emitterOptions;
    private readonly string _artifactsRoot;
    private readonly string _runIdentifier;

    private SvgSmokeFixtures(
        SvgCreatorRunOptions options,
        IReadOnlyList<IPipelineStage> stages,
        PipelineDependencies dependencies,
        ImmutableArray<ShapeLayer> shapeLayers,
        ImmutableArray<LayerPathGeometry> geometries,
        DepthOrder depthOrder,
        ImageData image,
        SvgEmitterOptions emitterOptions,
        string artifactsRoot,
        string runIdentifier)
    {
        _options = options;
        _stages = stages;
        _dependencies = dependencies;
        _shapeLayers = shapeLayers;
        _geometries = geometries;
        _depthOrder = depthOrder;
        _image = image;
        _emitterOptions = emitterOptions;
        _artifactsRoot = artifactsRoot;
        _runIdentifier = runIdentifier;
    }

    public const string DefaultImagePath = "tests/fixtures/smoke-input.jpg";
    public const string DefaultOutputDirectory = "tests/_artifacts/svg-smoke";

    /// <summary>
    /// スモークテスト用フィクスチャを生成します。
    /// デフォルトでは <c>tests/fixtures/smoke-input.jpg</c> を入力画像とし、成果物を <c>tests/_artifacts/svg-smoke</c> に出力します。
    /// ユーザー独自の画像で試す場合は、任意の JPEG パスと出力先ディレクトリを指定してください。
    /// 例: <c>SvgSmokeFixtures.Create("~/datasets/sample.jpg", "~/tmp/svg-output")</c>
    /// 1. パスが存在すればその JPEG を入力として読み込みます。
    /// 2. 存在しない場合は小さな検証用 JPEG を自動生成します。
    /// 3. 指定した出力ディレクトリ配下にラン生成の SVG ファイルを作成します。
    /// </summary>
    public static SvgSmokeFixtures Create(string? imageFilePath = null, string? outputDirectory = null)
    {
        var configuration = SmokeTestConfiguration.Load(imageFilePath ?? DefaultImagePath);
        var artifactsRoot = ResolveOutputDirectory(outputDirectory);
        var image = configuration.Image;
        var quantization = CreateQuantization(image);
        var primaryColor = quantization.Palette[0];
        var layers = ImmutableArray.Create(CreateShapeLayer("layer-0001", primaryColor, image.Width, image.Height));
        var geometries = ImmutableArray.Create(CreateRectangleGeometry(layers[0].Id, primaryColor, image.Width, image.Height));
        var depthOrder = new DepthOrder(new Dictionary<string, int>
        {
            [layers[0].Id] = 0
        });

        var options = new SvgCreatorRunOptions(
            imagePath: configuration.OptionImagePath,
            outputDirectory: "out",
            cliOptionSnapshot: new Dictionary<string, string>
            {
                ["image"] = configuration.OptionImagePath,
                ["output"] = "out"
            });

        var dependencies = new PipelineDependencies(
            new StubImageReader(image),
            new StubQuantizer(quantization),
            new StubShapeLayerBuilder(layers),
            new StubDepthOrderingService(depthOrder),
            new StubOcclusionCompleter(layers));

        var stages = new IPipelineStage[]
        {
            new ImageLoadingStage(),
            new QuantizationStage(),
            new ShapeLayerExtractionStage(),
            new DepthOrderingStage(),
            new OcclusionCompletionStage()
        };

        var emitterOptions = new SvgEmitterOptions
        {
            GeneratorName = "SvgCreatorSmokeTest",
            MaxDecimalPlaces = 2
        };

        var runIdentifier = Guid.NewGuid().ToString("N");

        return new SvgSmokeFixtures(options, stages, dependencies, layers, geometries, depthOrder, image, emitterOptions, artifactsRoot, runIdentifier);
    }

    public async Task<SmokeTestReport> RunAsync(ITestOutputHelper output, CancellationToken cancellationToken)
    {
        var orchestrator = new SvgCreationOrchestrator(_stages, _dependencies, new NullDebugSink());
        var stopwatch = Stopwatch.StartNew();
        var result = await orchestrator.ExecuteAsync(_options, cancellationToken).ConfigureAwait(false);
        stopwatch.Stop();

        var exportFilter = new ExportFilter();
        var exportItems = exportFilter.Filter(_geometries, result.DepthOrder, selectedLayerIds: null);

        var emitter = new SvgEmitter();
        var sizeLimiter = new SizeLimiter(emitter);

        var artifactsDirectory = EnsureArtifactsDirectory(_artifactsRoot, _runIdentifier);
        var combinedSvg = emitter.EmitDocument(result.Image, _geometries, result.DepthOrder, _emitterOptions);
        var combinedPath = IOPath.Combine(artifactsDirectory, $"layers-full-{_runIdentifier}.svg");
        WriteFile(combinedPath, combinedSvg);

        var layers = new List<LayerDocumentInfo>(exportItems.Length);

        foreach (var item in exportItems)
        {
            var document = sizeLimiter.EmitWithLimit(_image, _depthOrder, item, MaximumLayerBytes, _emitterOptions);
            var layerPath = IOPath.Combine(artifactsDirectory, $"layer-{document.LayerId}-{_runIdentifier}.svg");
            WriteFile(layerPath, document.SvgContent);
            layers.Add(new LayerDocumentInfo(document.LayerId, layerPath, document.ByteCount, document.AppliedDecimalPlaces));
        }

        output?.WriteLine($"SvgSmoke elapsed={stopwatch.ElapsedMilliseconds}ms artifacts={artifactsDirectory}");
        foreach (var layer in layers)
        {
            output?.WriteLine($" - {layer.LayerId}: {layer.ByteCount} bytes ({layer.AppliedDecimalPlaces} decimals)");
        }

        return new SmokeTestReport(stopwatch.Elapsed, combinedPath, layers.ToImmutableArray());
    }

    private static QuantizationResult CreateQuantization(ImageData image)
    {
        var pixelCount = image.Width * image.Height;
        var labels = new int[pixelCount];
        var span = image.Pixels.Span;
        var palette = new List<RgbColor>();
        var paletteIndex = new Dictionary<RgbColor, int>();

        for (var i = 0; i < pixelCount; i++)
        {
            var offset = i * 3;
            var color = new RgbColor(span[offset], span[offset + 1], span[offset + 2]);

            if (!paletteIndex.TryGetValue(color, out var index))
            {
                index = palette.Count;
                palette.Add(color);
                paletteIndex[color] = index;
            }

            labels[i] = index;
        }

        return new QuantizationResult(
            image,
            palette.ToImmutableArray(),
            ImmutableArray.CreateRange(labels));
    }

    private static ShapeLayer CreateShapeLayer(string id, RgbColor color, int width, int height)
    {
        var maskBits = ImmutableArray.CreateRange(Enumerable.Repeat(true, width * height));
        var mask = new RasterMask(width, height, maskBits);
        var boundary = ImmutableArray.Create(
            new Vector2(0, 0),
            new Vector2(width, 0),
            new Vector2(width, height),
            new Vector2(0, height));

        return new ShapeLayer(id, color, mask, boundary, ImmutableArray<IImmutableList<Vector2>>.Empty, area: width * height);
    }

    private static LayerPathGeometry CreateRectangleGeometry(string id, RgbColor color, int width, int height)
    {
        var segments = ImmutableArray.Create(
            new PathSegment(PathSegmentType.Move, new[] { new Vector2(0, 0) }),
            new PathSegment(PathSegmentType.Line, new[] { new Vector2(width, 0) }),
            new PathSegment(PathSegmentType.Line, new[] { new Vector2(width, height) }),
            new PathSegment(PathSegmentType.Line, new[] { new Vector2(0, height) }),
            new PathSegment(PathSegmentType.Close, Array.Empty<Vector2>()));

        var outer = new SvgPath(segments);
        return new LayerPathGeometry(id, color, outer, ImmutableArray<SvgPath>.Empty);
    }

    private static string EnsureArtifactsDirectory(string root, string runIdentifier)
    {
        Directory.CreateDirectory(root);

        var runDirectory = IOPath.Combine(root, runIdentifier);
        Directory.CreateDirectory(runDirectory);
        return runDirectory;
    }

    private static void WriteFile(string path, string content)
    {
        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        File.WriteAllText(path, content, encoding);
    }

    private static string GetRepositoryRoot()
        => IOPath.GetFullPath(IOPath.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    private static string ResolveOutputDirectory(string? outputDirectory)
    {
        var repoRoot = GetRepositoryRoot();
        var candidate = string.IsNullOrWhiteSpace(outputDirectory)
            ? IOPath.Combine(repoRoot, DefaultOutputDirectory)
            : (IOPath.IsPathRooted(outputDirectory)
                ? outputDirectory
                : IOPath.Combine(repoRoot, outputDirectory));

        return IOPath.GetFullPath(candidate);
    }

    private sealed class SmokeTestConfiguration
    {
        private SmokeTestConfiguration(ImageData image, string optionImagePath)
        {
            Image = image;
            OptionImagePath = optionImagePath;
        }

        public ImageData Image { get; }

        public string OptionImagePath { get; }

        public static SmokeTestConfiguration Load(string imagePath)
        {
            var repoRoot = GetRepositoryRoot();
            var path = IOPath.IsPathRooted(imagePath)
                ? imagePath
                : IOPath.Combine(repoRoot, imagePath);
            path = IOPath.GetFullPath(path);

            if (!File.Exists(path))
            {
                var directory = IOPath.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                using var generated = new Image<Rgb24>(4, 4);
                generated.ProcessPixelRows(accessor =>
                {
                    for (var y = 0; y < accessor.Height; y++)
                    {
                        var row = accessor.GetRowSpan(y);
                        for (var x = 0; x < accessor.Width; x++)
                        {
                            var r = (byte)(50 + x * 50);
                            var g = (byte)(40 + y * 40);
                            var b = (byte)(80 + (x + y) * 30);
                            row[x] = new Rgb24(r, g, b);
                        }
                    }
                });

                generated.Save(path, new JpegEncoder { Quality = 92 });
            }

            using var decoded = SixLabors.ImageSharp.Image.Load<Rgb24>(path);
            var buffer = new byte[decoded.Width * decoded.Height * 3];
            var index = 0;

            decoded.ProcessPixelRows(accessor =>
            {
                for (var y = 0; y < accessor.Height; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (var x = 0; x < accessor.Width; x++)
                    {
                        var pixel = row[x];
                        buffer[index++] = pixel.R;
                        buffer[index++] = pixel.G;
                        buffer[index++] = pixel.B;
                    }
                }
            });

            var imageData = new ImageData(decoded.Width, decoded.Height, PixelFormat.Rgb, buffer);
            return new SmokeTestConfiguration(imageData, path);
        }
    }

    private sealed class StubImageReader : IImageReader
    {
        private readonly ImageData _image;

        public StubImageReader(ImageData image)
        {
            _image = image;
        }

        public Task<ImageData> ReadAsync(SvgCreatorRunOptions options, CancellationToken cancellationToken)
            => Task.FromResult(_image);
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

    private sealed class StubShapeLayerBuilder : IShapeLayerBuilder
    {
        private readonly ImmutableArray<ShapeLayer> _layers;

        public StubShapeLayerBuilder(ImmutableArray<ShapeLayer> layers)
        {
            _layers = layers;
        }

        public Task<ShapeLayerExtractionResult> BuildLayersAsync(QuantizationResult quantization, CancellationToken cancellationToken)
            => Task.FromResult(new ShapeLayerExtractionResult(_layers, Array.Empty<NoisyLayer>()));
    }

    private sealed class StubDepthOrderingService : IDepthOrderingService
    {
        private readonly DepthOrder _depthOrder;

        public StubDepthOrderingService(DepthOrder depthOrder)
        {
            _depthOrder = depthOrder;
        }

        public DepthOrder Compute(IReadOnlyList<ShapeLayer> layers, DepthOrderingOptions options)
            => _depthOrder;
    }

    private sealed class StubOcclusionCompleter : IOcclusionCompleter
    {
        private readonly ImmutableArray<ShapeLayer> _layers;

        public StubOcclusionCompleter(ImmutableArray<ShapeLayer> layers)
        {
            _layers = layers;
        }

        public Task<OcclusionCompletionResult> CompleteAsync(IReadOnlyList<ShapeLayer> layers, DepthOrder depthOrder, OcclusionCompletionOptions options, CancellationToken cancellationToken)
            => Task.FromResult(new OcclusionCompletionResult(_layers));
    }

    private sealed class NullDebugSink : IDebugSink
    {
        public Task WriteSnapshotAsync(string stageName, DebugSnapshot snapshot, DebugExecutionContext context, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task CompleteAsync(DebugExecutionContext context, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}

internal sealed record SmokeTestReport(TimeSpan Duration, string CombinedSvgPath, ImmutableArray<LayerDocumentInfo> LayerDocuments);

internal sealed record LayerDocumentInfo(string LayerId, string FilePath, int ByteCount, int AppliedDecimalPlaces);
