using System.Collections.Generic;
using System.Collections.Immutable;
using System.Numerics;
using SvgCreator.Core.DepthOrdering;
using SvgCreator.Core.Models;

namespace SvgCreator.Core.Tests.DepthOrdering;

public sealed class DepthOrderingServiceTests
{
    // 中央の小さなレイヤーが前面になるよう深度が決定されることを確認
    [Fact]
    public void ComputeDepthOrder_WithNestedLayers_PlacesInnerLayerInFront()
    {
        var layers = new List<ShapeLayer>
        {
            CreateRectangleLayer("layer-background", new RgbColor(10, 20, 30), 0, 0, 4, 4, 4, 4),
            CreateRectangleLayer("layer-foreground", new RgbColor(200, 210, 220), 1, 1, 2, 2, 4, 4)
        };

        var service = new DepthOrderingService();
        var order = service.Compute(layers, new DepthOrderingOptions { Delta = 0.05f });

        Assert.Equal(0, order.GetDepth("layer-background"));
        Assert.Equal(1, order.GetDepth("layer-foreground"));
    }

    // 面積差がデルタ未満の場合はエッジを張らず、フォールバック順序が適用されることを確認
    [Fact]
    public void ComputeDepthOrder_WithNearEqualAreas_UsesFallbackOrdering()
    {
        var layers = new List<ShapeLayer>
        {
            CreateRectangleLayer("layer-a", new RgbColor(50, 60, 70), 0, 0, 3, 3, 3, 3),
            CreateRectangleLayer("layer-b", new RgbColor(80, 90, 100), 1, 0, 3, 3, 3, 3)
        };

        var service = new DepthOrderingService();
        var order = service.Compute(layers, new DepthOrderingOptions { Delta = 0.6f });

        Assert.Equal(0, order.GetDepth("layer-a"));
        Assert.Equal(1, order.GetDepth("layer-b"));
    }

    // 複数レイヤーが連鎖的に接している場合でもトポロジカルソートで一貫した深度が得られることを確認
    [Fact]
    public void ComputeDepthOrder_WithLayerChain_ComputesConsistentOrdering()
    {
        var layers = new List<ShapeLayer>
        {
            CreateRectangleLayer("layer-back", new RgbColor(10, 20, 30), 0, 0, 5, 5, 5, 5),
            CreateRectangleLayer("layer-mid", new RgbColor(120, 130, 140), 1, 1, 3, 3, 5, 5),
            CreateRectangleLayer("layer-front", new RgbColor(220, 230, 240), 2, 2, 1, 1, 5, 5)
        };

        var service = new DepthOrderingService();
        var order = service.Compute(layers, new DepthOrderingOptions { Delta = 0.05f });

        Assert.Equal(0, order.GetDepth("layer-back"));
        Assert.Equal(1, order.GetDepth("layer-mid"));
        Assert.Equal(2, order.GetDepth("layer-front"));
    }

    private static ShapeLayer CreateRectangleLayer(
        string id,
        RgbColor color,
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

        var holes = ImmutableArray<IImmutableList<Vector2>>.Empty;
        var mask = new RasterMask(maskWidth, maskHeight, bits.MoveToImmutable());

        return new ShapeLayer(id, color, mask, boundary, holes, area);
    }
}
