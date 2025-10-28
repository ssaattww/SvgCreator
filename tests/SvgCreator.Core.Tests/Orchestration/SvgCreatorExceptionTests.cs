using SvgCreator.Core.Orchestration;

namespace SvgCreator.Core.Tests.Orchestration;

public sealed class SvgCreatorExceptionTests
{
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

    [Fact]
    public void FromCode_WithDetails_ShouldAppendDetails()
    {
        var ex = SvgCreatorException.FromCode(
            SvgCreatorErrorCode.OutputWriteFailed,
            details: "Access denied");

        Assert.Contains("Access denied", ex.Message);
    }

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
