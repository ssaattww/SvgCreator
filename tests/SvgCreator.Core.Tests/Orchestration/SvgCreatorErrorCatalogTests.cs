using SvgCreator.Core.Orchestration;

namespace SvgCreator.Core.Tests.Orchestration;

public sealed class SvgCreatorErrorCatalogTests
{
    [Fact]
    public void AllDescriptors_ShouldContainEveryErrorCode()
    {
        var allCodes = Enum.GetValues<SvgCreatorErrorCode>();
        var descriptors = SvgCreatorErrorCatalog.AllDescriptors;

        foreach (var code in allCodes)
        {
            Assert.Contains(descriptors, descriptor => descriptor.Code == code);
        }
    }

    [Theory]
    [InlineData(SvgCreatorErrorCode.InputFileNotFound, SvgCreatorErrorCategory.Input)]
    [InlineData(SvgCreatorErrorCode.InputUnsupportedFormat, SvgCreatorErrorCategory.Input)]
    [InlineData(SvgCreatorErrorCode.ImageDecodeFailed, SvgCreatorErrorCategory.Input)]
    [InlineData(SvgCreatorErrorCode.SegmentationProducedNoLayers, SvgCreatorErrorCategory.Segmentation)]
    [InlineData(SvgCreatorErrorCode.DepthOrderingCyclicDependency, SvgCreatorErrorCategory.DepthOrdering)]
    [InlineData(SvgCreatorErrorCode.OcclusionSolverDidNotConverge, SvgCreatorErrorCategory.Occlusion)]
    [InlineData(SvgCreatorErrorCode.LayerSizeLimitExceeded, SvgCreatorErrorCategory.Output)]
    [InlineData(SvgCreatorErrorCode.OutputWriteFailed, SvgCreatorErrorCategory.Output)]
    [InlineData(SvgCreatorErrorCode.DebugWriteFailed, SvgCreatorErrorCategory.Debug)]
    [InlineData(SvgCreatorErrorCode.DebugSnapshotVersionMismatch, SvgCreatorErrorCategory.Debug)]
    [InlineData(SvgCreatorErrorCode.UnexpectedPipelineFailure, SvgCreatorErrorCategory.Unexpected)]
    public void GetDescriptor_ShouldProvideExpectedCategory(
        SvgCreatorErrorCode code,
        SvgCreatorErrorCategory expectedCategory)
    {
        var descriptor = SvgCreatorErrorCatalog.GetDescriptor(code);

        Assert.Equal(expectedCategory, descriptor.Category);
    }

    [Theory]
    [InlineData(SvgCreatorErrorCode.InputFileNotFound)]
    [InlineData(SvgCreatorErrorCode.InputUnsupportedFormat)]
    [InlineData(SvgCreatorErrorCode.ImageDecodeFailed)]
    [InlineData(SvgCreatorErrorCode.SegmentationProducedNoLayers)]
    [InlineData(SvgCreatorErrorCode.DepthOrderingCyclicDependency)]
    [InlineData(SvgCreatorErrorCode.OcclusionSolverDidNotConverge)]
    [InlineData(SvgCreatorErrorCode.LayerSizeLimitExceeded)]
    [InlineData(SvgCreatorErrorCode.OutputWriteFailed)]
    [InlineData(SvgCreatorErrorCode.DebugWriteFailed)]
    [InlineData(SvgCreatorErrorCode.DebugSnapshotVersionMismatch)]
    [InlineData(SvgCreatorErrorCode.UnexpectedPipelineFailure)]
    public void Descriptor_ShouldProvideSummaryAndAction(SvgCreatorErrorCode code)
    {
        var descriptor = SvgCreatorErrorCatalog.GetDescriptor(code);

        Assert.False(string.IsNullOrWhiteSpace(descriptor.Summary));
        Assert.False(string.IsNullOrWhiteSpace(descriptor.RecommendedAction));
    }
}
