using System;
using System.Text;
using SvgCreator.Core.Models;

namespace SvgCreator.Core.Svg;

/// <summary>
/// SVG 出力サイズが 15KB 制約以内に収まるよう座標表現の精度を調整します。
/// </summary>
public sealed class SizeLimiter
{
    private static readonly Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    private readonly SvgEmitter _emitter;

    /// <summary>
    /// <see cref="SizeLimiter"/> を初期化します。
    /// </summary>
    /// <param name="emitter">SVG 文字列生成に用いるエミッタ。</param>
    public SizeLimiter(SvgEmitter emitter)
    {
        _emitter = emitter ?? throw new ArgumentNullException(nameof(emitter));
    }

    /// <summary>
    /// レイヤー単位で SVG を生成し、指定したバイト制限以下に収まるよう調整します。
    /// </summary>
    /// <param name="image">出力寸法を決定する画像メタ。</param>
    /// <param name="depthOrder">レイヤーの深度順序。</param>
    /// <param name="layer">出力対象レイヤー。</param>
    /// <param name="maxBytes">許容バイト数（>0）。</param>
    /// <param name="options">既定の SVG 書式設定。</param>
    /// <returns>エクスポート結果。</returns>
    /// <exception cref="ArgumentNullException">必須引数が <c>null</c> の場合。</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxBytes"/> が 1 未満。</exception>
    /// <exception cref="InvalidOperationException">精度を 0 桁に落としても制約を満たせない場合。</exception>
    public LayerExportDocument EmitWithLimit(
        ImageData image,
        DepthOrder depthOrder,
        LayerExportItem layer,
        int maxBytes,
        SvgEmitterOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(depthOrder);
        ArgumentNullException.ThrowIfNull(layer);

        if (maxBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxBytes), maxBytes, "Maximum byte size must be positive.");
        }

        var normalizedOptions = NormalizeOptions(options);

        for (var decimals = normalizedOptions.MaxDecimalPlaces; decimals >= 0; decimals--)
        {
            var attemptOptions = new SvgEmitterOptions
            {
                MaxDecimalPlaces = decimals,
                GeneratorName = normalizedOptions.GeneratorName
            };

            var svg = _emitter.EmitDocument(image, new[] { layer.Geometry }, depthOrder, attemptOptions);
            var byteCount = Utf8.GetByteCount(svg);

            if (byteCount <= maxBytes)
            {
                return new LayerExportDocument(layer.LayerId, svg, byteCount, decimals);
            }
        }

        throw new InvalidOperationException($"Layer '{layer.LayerId}' exceeds the size limit of {maxBytes} bytes.");
    }

    private static SvgEmitterOptions NormalizeOptions(SvgEmitterOptions? options)
    {
        var baseOptions = options ?? SvgEmitterOptions.Default;
        var decimals = baseOptions.MaxDecimalPlaces;

        if (decimals < 0)
        {
            decimals = SvgEmitterOptions.Default.MaxDecimalPlaces;
        }

        decimals = Math.Min(decimals, SvgEmitterOptions.MaximumSupportedDecimalPlaces);

        var generator = string.IsNullOrWhiteSpace(baseOptions.GeneratorName)
            ? SvgEmitterOptions.Default.GeneratorName
            : baseOptions.GeneratorName.Trim();

        return new SvgEmitterOptions
        {
            MaxDecimalPlaces = decimals,
            GeneratorName = generator
        };
    }
}

/// <summary>
/// エクスポート済みレイヤーの SVG ドキュメントを表します。
/// </summary>
public sealed class LayerExportDocument
{
    /// <summary>
    /// <see cref="LayerExportDocument"/> を初期化します。
    /// </summary>
    /// <param name="layerId">対象レイヤー ID。</param>
    /// <param name="svgContent">SVG マークアップ。</param>
    /// <param name="byteCount">UTF-8 バイト長。</param>
    /// <param name="appliedDecimalPlaces">適用した小数桁数。</param>
    public LayerExportDocument(string layerId, string svgContent, int byteCount, int appliedDecimalPlaces)
    {
        if (string.IsNullOrWhiteSpace(layerId))
        {
            throw new ArgumentException("Layer id must be non-empty.", nameof(layerId));
        }

        LayerId = layerId;
        SvgContent = svgContent ?? throw new ArgumentNullException(nameof(svgContent));
        ByteCount = byteCount;
        AppliedDecimalPlaces = appliedDecimalPlaces;
    }

    /// <summary>
    /// レイヤー ID を取得します。
    /// </summary>
    public string LayerId { get; }

    /// <summary>
    /// SVG マークアップを取得します。
    /// </summary>
    public string SvgContent { get; }

    /// <summary>
    /// UTF-8 でのバイト長を取得します。
    /// </summary>
    public int ByteCount { get; }

    /// <summary>
    /// 適用した小数桁数を取得します。
    /// </summary>
    public int AppliedDecimalPlaces { get; }
}
