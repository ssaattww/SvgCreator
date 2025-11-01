using System;
using System.Runtime.InteropServices;
using OpenCvSharp;
using SvgCreator.Core.Models;
using SvgCreator.Core.Orchestration;
using System.IO;
using IOPath = System.IO.Path;

namespace SvgCreator.Core;

/// <summary>
/// 量子化結果を 2 値化したグレースケール画像として書き出す補助コンポーネントです。
/// </summary>
internal static class BinarizedImageWriter
{
    private const double Epsilon = 1e-9;

    /// <summary>
    /// 量子化結果を基に 2 値化画像を生成し、指定したパスへ保存します。
    /// </summary>
    /// <param name="result">量子化結果。</param>
    /// <param name="options">書き出し設定を保持する実行オプション。</param>
    public static void Write(QuantizationResult result, SvgCreatorRunOptions options)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.BinarizedImageOutputName))
        {
            return;
        }

        var targetPath = ResolveTargetPath(options.OutputDirectory, options.BinarizedImageOutputName!);
        EnsureDirectory(targetPath);

        var palette = result.Palette;
        if (palette.IsDefaultOrEmpty)
        {
            throw new InvalidOperationException("Cannot render binarized image without quantization palette.");
        }

        var luminances = new double[palette.Length];
        var minLuminance = double.MaxValue;
        var maxLuminance = double.MinValue;

        for (var i = 0; i < palette.Length; i++)
        {
            var color = palette[i];
            var luminance = ComputeLuminance(color);
            luminances[i] = luminance;
            minLuminance = Math.Min(minLuminance, luminance);
            maxLuminance = Math.Max(maxLuminance, luminance);
        }

        var threshold = Math.Abs(maxLuminance - minLuminance) < Epsilon
            ? maxLuminance
            : minLuminance + (maxLuminance - minLuminance) * 0.5d;

        var labels = result.LabelIndices;
        var pixels = new byte[labels.Length];

        for (var index = 0; index < labels.Length; index++)
        {
            var paletteIndex = labels[index];
            var luminance = luminances[paletteIndex];
            pixels[index] = luminance >= threshold ? (byte)255 : (byte)0;
        }

        using var mat = new Mat(result.Image.Height, result.Image.Width, MatType.CV_8UC1);
        Marshal.Copy(pixels, 0, mat.Data, pixels.Length);

        var parameters = CreateEncodingParams(targetPath);
        if (parameters is null)
        {
            Cv2.ImWrite(targetPath, mat);
        }
        else
        {
            Cv2.ImWrite(targetPath, mat, parameters);
        }
    }

    private static double ComputeLuminance(RgbColor color)
    {
        return 0.2126d * color.R + 0.7152d * color.G + 0.0722d * color.B;
    }

    private static string ResolveTargetPath(string outputDirectory, string fileName)
    {
        return IOPath.IsPathRooted(fileName)
            ? fileName
            : IOPath.Combine(outputDirectory, fileName);
    }

    private static void EnsureDirectory(string path)
    {
        var directory = IOPath.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private static ImageEncodingParam[]? CreateEncodingParams(string path)
    {
        var extension = IOPath.GetExtension(path);
        if (string.IsNullOrEmpty(extension))
        {
            return null;
        }

        switch (extension.ToLowerInvariant())
        {
            case ".jpg":
            case ".jpeg":
                return new[] { new ImageEncodingParam(ImwriteFlags.JpegQuality, 95) };
            case ".png":
                return new[] { new ImageEncodingParam(ImwriteFlags.PngCompression, 3) };
            default:
                return null;
        }
    }
}
