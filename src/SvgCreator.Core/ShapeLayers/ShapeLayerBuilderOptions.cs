using System;

namespace SvgCreator.Core.ShapeLayers;

/// <summary>
/// <see cref="ShapeLayerBuilder"/> の挙動を調整するためのオプションを表します。
/// </summary>
public sealed class ShapeLayerBuilderOptions
{
    /// <summary>
    /// 既定の <see cref="ShapeLayerBuilderOptions"/> を取得します。
    /// </summary>
    public static ShapeLayerBuilderOptions Default { get; } = new();

    /// <summary>
    /// 微小成分とみなす最小画素数（未満で除外）。0 の場合は面積による除外を行いません。
    /// </summary>
    public int NoisyComponentMinimumPixelCount { get; init; }

    /// <summary>
    /// 微小成分とみなす最小周長（ピクセル単位、未満で除外）。0 以下の場合は周長による除外を行いません。
    /// </summary>
    public float NoisyComponentMinimumPerimeter { get; init; }

    /// <summary>
    /// オプション値が妥当であることを検証します。
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">不正な値が指定されています。</exception>
    public void Validate()
    {
        if (NoisyComponentMinimumPixelCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(NoisyComponentMinimumPixelCount), NoisyComponentMinimumPixelCount, "Pixel threshold must be non-negative.");
        }

        if (float.IsNaN(NoisyComponentMinimumPerimeter) || NoisyComponentMinimumPerimeter < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(NoisyComponentMinimumPerimeter), NoisyComponentMinimumPerimeter, "Perimeter threshold must be non-negative.");
        }
    }
}
