namespace SvgCreator.Core.Orchestration;

/// <summary>
/// パイプラインステージの識別子を定義します。
/// </summary>
public static class PipelineStageNames
{
    public const string ImageLoading = "image-loading";
    public const string Quantization = "quantization";
    public const string Segmentation = "segmentation";
    public const string ShapeLayerExtraction = "shape-layer-extraction";
    public const string DepthOrdering = "depth-ordering";
    public const string OcclusionCompletion = "occlusion-completion";
    public const string BezierFitting = "bezier-fitting";
    public const string SvgEmission = "svg-emission";
    public const string LayerExport = "layer-export";
}

