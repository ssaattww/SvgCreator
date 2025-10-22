using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SvgCreator.Core.Diagnostics;
using SvgCreator.Core.Models;

namespace SvgCreator.Core.Tests.Diagnostics;

public sealed class DebugSnapshotSerializerTests
{
    private readonly DebugSnapshotSerializer _serializer = new();

    [Fact]
    // スナップショットを書き出して読み戻すと同じ内容になることを確認
    public async Task SerializeAndDeserialize_RoundTripsSnapshot()
    {
        var snapshot = CreateSampleSnapshot();

        await using var stream = new MemoryStream();
        await _serializer.SerializeAsync(snapshot, stream);

        stream.Position = 0;
        var restored = await _serializer.DeserializeAsync(stream);

        Assert.Equal(snapshot.Image.Width, restored.Image.Width);
        Assert.Equal(snapshot.Image.Height, restored.Image.Height);
        Assert.Equal(snapshot.Image.Format, restored.Image.Format);
        Assert.Equal(snapshot.Image.Pixels, restored.Image.Pixels);

        Assert.Equal(snapshot.Palette, restored.Palette);
        Assert.Equal(snapshot.Layers.Count, restored.Layers.Count);
        Assert.Equal(snapshot.Layers[0].Id, restored.Layers[0].Id);
        Assert.Equal(snapshot.Layers[0].Mask.Width, restored.Layers[0].Mask.Width);
        Assert.Equal(snapshot.Layers[0].Mask.Bits, restored.Layers[0].Mask.Bits);
        Assert.Equal(DebugSnapshot.CurrentVersion, restored.Version);
    }

    [Fact]
    // シリアライズされた JSON に主要フィールドが含まれることを確認
    public async Task SerializeAsync_WritesExpectedJson()
    {
        var snapshot = CreateSampleSnapshot();

        await using var stream = new MemoryStream();
        await _serializer.SerializeAsync(snapshot, stream);

        var json = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        Assert.Contains("\"image\"", json);
        Assert.Contains("\"palette\"", json);
        Assert.Contains("\"layers\"", json);
        Assert.Contains("\"layer-1\"", json);
        Assert.Contains(DebugSnapshot.CurrentVersion, json);
    }

    [Fact]
    // 対応していないバージョンを読み込むと例外となることを確認
    public async Task DeserializeAsync_Throws_OnUnsupportedVersion()
    {
        const string json = """
        {
          "version": "0.9",
          "image": { "width": 1, "height": 1, "format": 3, "pixels": "AAE=" },
          "palette": [{ "r": 0, "g": 0, "b": 0 }],
          "layers": []
        }
        """;

        await using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        await Assert.ThrowsAsync<InvalidDataException>(() => _serializer.DeserializeAsync(stream));
    }

    private static DebugSnapshot CreateSampleSnapshot()
    {
        var pixels = new byte[] { 10, 20, 30 };
        var imageData = new ImageData(1, 1, PixelFormat.Rgb, pixels);
        var quantResult = new QuantizationResult(
            imageData,
            new[] { new RgbColor(1, 2, 3) }.ToImmutableArray(),
            new[] { 0 }.ToImmutableArray());

        var mask = new RasterMask(1, 1, new[] { true }.ToImmutableArray());
        var layer = new ShapeLayer(
            "layer-1",
            new RgbColor(1, 2, 3),
            mask,
            new[] { new System.Numerics.Vector2(0, 0), new(1, 0), new(0, 1) }.ToImmutableArray(),
            ImmutableArray<IImmutableList<System.Numerics.Vector2>>.Empty,
            1);

        return DebugSnapshot.From(quantResult, new[] { layer });
    }
}
