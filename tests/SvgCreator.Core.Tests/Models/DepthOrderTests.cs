using System;
using System.Collections.Generic;
using SvgCreator.Core.Models;

namespace SvgCreator.Core.Tests.Models;

public sealed class DepthOrderTests
{
    [Fact]
    // レイヤー辞書が null または空の場合に例外となることを確認
    public void Constructor_Throws_WhenEntriesNullOrEmpty()
    {
        Assert.Throws<ArgumentNullException>(() => new DepthOrder(null!));
        Assert.Throws<ArgumentException>(() => new DepthOrder(new Dictionary<string, int>()));
    }

    [Fact]
    // 深度値が負のときに例外となることを確認
    public void Constructor_Throws_WhenDepthIsNegative()
    {
        var entries = new Dictionary<string, int> { ["layer-1"] = -1 };
        Assert.Throws<ArgumentOutOfRangeException>(() => new DepthOrder(entries));
    }

    [Fact]
    // 深度値の重複が許容されないことを確認
    public void Constructor_Throws_WhenDuplicateDepthValues()
    {
        var entries = new Dictionary<string, int>
        {
            ["layer-1"] = 0,
            ["layer-2"] = 0
        };

        Assert.Throws<ArgumentException>(() => new DepthOrder(entries));
    }

    [Fact]
    // Compare メソッドが深度順で大小を返すことを確認
    public void Compare_ReturnsExpectedOrdering()
    {
        var entries = new Dictionary<string, int>
        {
            ["background"] = 0,
            ["mid"] = 1,
            ["foreground"] = 2
        };

        var depthOrder = new DepthOrder(entries);

        Assert.True(depthOrder.Compare("background", "foreground") < 0);
        Assert.True(depthOrder.Compare("foreground", "background") > 0);
        Assert.Equal(0, depthOrder.Compare("mid", "mid"));
    }
}
