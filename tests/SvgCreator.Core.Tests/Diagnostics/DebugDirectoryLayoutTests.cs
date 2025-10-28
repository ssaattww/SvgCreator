using System.IO;
using SvgCreator.Core.Diagnostics;

namespace SvgCreator.Core.Tests.Diagnostics;

public sealed class DebugDirectoryLayoutTests
{
    // 標準パスが期待通り生成されることを確認
    [Fact]
    public void Properties_ReturnExpectedPaths()
    {
        var layout = new DebugDirectoryLayout(Path.Combine("out", "debug"));

        Assert.Equal(Path.Combine("out", "debug"), layout.BaseDirectory);
        Assert.Equal(Path.Combine("out", "debug", "metadata.json"), layout.MetadataPath);
        Assert.Equal(Path.Combine("out", "debug", "pipeline.json"), layout.PipelineSnapshotPath);
        Assert.Equal(Path.Combine("out", "debug", "layers.json"), layout.LayerSummaryPath);
    }

    // ステージ名がサニタイズされてステージディレクトリへマップされることを確認
    [Fact]
    public void GetStageSnapshotPath_SanitizesStageName()
    {
        var layout = new DebugDirectoryLayout("debug-out");

        var path = layout.GetStageSnapshotPath("Bezier Fitter");

        Assert.Equal(Path.Combine("debug-out", "stages", "bezier-fitter.json"), path);
    }

    // アセットディレクトリはステージ名ごとに作成されることを確認
    [Fact]
    public void GetStageAssetsDirectory_ReturnsExpectedPath()
    {
        var layout = new DebugDirectoryLayout("debug-out");

        var dir = layout.GetStageAssetsDirectory("DepthOrdering");

        Assert.Equal(Path.Combine("debug-out", "stages", "depthordering", "assets"), dir);
    }
}
