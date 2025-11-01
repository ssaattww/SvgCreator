using System.Numerics;

namespace SvgCreator.Core.Quantization;

/// <summary>
/// 量子化処理に用いる色サンプルとその重みを表します。
/// </summary>
internal readonly struct QuantizationSample
{
    /// <summary>
    /// 新しい <see cref="QuantizationSample"/> を初期化します。
    /// </summary>
    /// <param name="color">RGB 空間におけるサンプル色。</param>
    /// <param name="weight">サンプルが代表する画素数。</param>
    public QuantizationSample(Vector3 color, int weight)
    {
        Color = color;
        Weight = weight;
    }

    /// <summary>
    /// RGB 空間におけるサンプル色を取得します。
    /// </summary>
    public Vector3 Color { get; }

    /// <summary>
    /// サンプルが代表する画素数を取得します。
    /// </summary>
    public int Weight { get; }
}
