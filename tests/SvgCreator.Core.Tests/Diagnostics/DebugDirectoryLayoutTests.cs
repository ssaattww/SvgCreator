using System.IO;
using SvgCreator.Core.Diagnostics;

namespace SvgCreator.Core.Tests.Diagnostics;

public sealed class DebugDirectoryLayoutTests
{
    [Fact]
    // 標準パスが期待通り生成されることを確認
    public void Properties_ReturnExpectedPaths()
    {
        var layout = new DebugDirectoryLayout(Path.Combine("out", "debug"));

        Assert.Equal(Path.Combine("out", "debug"), layout.BaseDirectory);
        Assert.Equal(Path.Combine("out", "debug", "metadata.json"), layout.MetadataPath);
        Assert.Equal(Path.Combine("out", "debug", "pipeline.json"), layout.PipelineSnapshotPath);
        Assert.Equal(Path.Combine("out", "debug", "layers.json"), layout.LayerSummaryPath);
    }

    [Fact]
    // ステージ名がサニタイズされてステージディレクトリへマップされることを確認
    public void GetStageSnapshotPath_SanitizesStageName()
    {
        var layout = new DebugDirectoryLayout("debug-out");

        var path = layout.GetStageSnapshotPath("Bezier Fitter");

        Assert.Equal(Path.Combine("debug-out", "stages", "bezier-fitter.json"), path);
    }

    [Fact]
    // アセットディレクトリはステージ名ごとに作成されることを確認
    public void GetStageAssetsDirectory_ReturnsExpectedPath()
    {
        var layout = new DebugDirectoryLayout("debug-out");

        var dir = layout.GetStageAssetsDirectory("DepthOrdering");

        Assert.Equal(Path.Combine("debug-out", "stages", "depthordering", "assets"), dir);
    }
}
