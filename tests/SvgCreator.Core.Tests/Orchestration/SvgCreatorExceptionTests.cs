using SvgCreator.Core.Orchestration;

namespace SvgCreator.Core.Tests.Orchestration;

public sealed class SvgCreatorExceptionTests
{
    // エラーコードのみ指定した場合にサマリと推奨アクションがメッセージへ反映されることを検証
    [Fact]
    public void FromCode_ShouldPopulateSummaryAndRecommendationInMessage()
    {
        var ex = SvgCreatorException.FromCode(SvgCreatorErrorCode.InputFileNotFound);

        Assert.Contains("input image file", ex.Message);
        Assert.Contains("Verify the --image", ex.Message);
        Assert.Equal(SvgCreatorErrorCode.InputFileNotFound, ex.ErrorCode);
        Assert.Equal(SvgCreatorErrorCategory.Input, ex.Category);
        Assert.Null(ex.InnerException);
    }

    // 追加詳細がメッセージに追記されることを検証
    [Fact]
    public void FromCode_WithDetails_ShouldAppendDetails()
    {
        var ex = SvgCreatorException.FromCode(
            SvgCreatorErrorCode.OutputWriteFailed,
            details: "Access denied");

        Assert.Contains("Access denied", ex.Message);
    }

    // 内部例外が保持されることを検証
    [Fact]
    public void FromCode_WithInnerException_ShouldSetInnerException()
    {
        var inner = new IOException("disk full");
        var ex = SvgCreatorException.FromCode(
            SvgCreatorErrorCode.DebugWriteFailed,
            innerException: inner);

        Assert.Same(inner, ex.InnerException);
    }
}
