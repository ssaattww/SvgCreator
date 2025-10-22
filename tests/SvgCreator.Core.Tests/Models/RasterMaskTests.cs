using System;
using System.Collections.Immutable;
using SvgCreator.Core.Models;

namespace SvgCreator.Core.Tests.Models;

public sealed class RasterMaskTests
{
    [Fact]
    // 幅または高さが 1 未満のときに例外となることを確認
    public void Constructor_Throws_WhenDimensionsInvalid()
    {
        var bits = ImmutableArray.Create(true);

        Assert.Throws<ArgumentOutOfRangeException>(() => new RasterMask(0, 1, bits));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RasterMask(1, 0, bits));
    }

    [Fact]
    // ビット数が幅×高さと一致しないときに例外となることを確認
    public void Constructor_Throws_WhenBitCountDoesNotMatchDimensions()
    {
        var bits = ImmutableArray.Create(true, false, true);
        Assert.Throws<ArgumentException>(() => new RasterMask(1, 1, bits));
    }

    [Fact]
    // インデクサーが正しいビット値を返すことを確認
    public void Indexer_ReturnsExpectedValue()
    {
        var bits = ImmutableArray.Create(false, true, false, true);
        var mask = new RasterMask(2, 2, bits);

        Assert.False(mask[0, 0]);
        Assert.True(mask[1, 0]);
        Assert.False(mask[0, 1]);
        Assert.True(mask[1, 1]);
    }
}
