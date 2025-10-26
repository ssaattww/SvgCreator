using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenCvSharp;
using SvgCreator.Core.Dependencies;
using SvgCreator.Core.Models;
using SvgCreator.Core.Orchestration;
using IOPath = System.IO.Path;

namespace SvgCreator.Core;

/// <summary>
/// OpenCV を利用して画像を読み込み、減色パイプライン向けの <see cref="ImageData"/> に変換します。
/// </summary>
public sealed class ImageReader : IImageReader
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg"
    };

    private readonly ILogger<ImageReader> _logger;
    private readonly ImageReaderSettings _settings;

    /// <summary>
    /// <see cref="ImageReader"/> を初期化します。
    /// </summary>
    /// <param name="logger">進捗やエラーを記録するロガー。</param>
    /// <param name="settings">前処理設定。</param>
    public ImageReader(ILogger<ImageReader>? logger = null, ImageReaderSettings? settings = null)
    {
        _logger = logger ?? NullLogger<ImageReader>.Instance;
        _settings = settings ?? ImageReaderSettings.Default;
    }

    /// <inheritdoc />
    public Task<ImageData> ReadAsync(SvgCreatorRunOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();

        var imagePath = options.ImagePath;
        if (!File.Exists(imagePath))
        {
            _logger.LogError("Input image not found at path {Path}.", imagePath);
            throw new FileNotFoundException($"Input image '{imagePath}' was not found.", imagePath);
        }

        ValidateExtension(imagePath);

        OpenCvRuntimeBootstrapper.EnsureDependenciesAvailable(_logger);

        using var bgr = Cv2.ImRead(imagePath, ImreadModes.Color);
        if (bgr.Empty())
        {
            _logger.LogError("Failed to decode image at path {Path}.", imagePath);
            throw new InvalidDataException($"Failed to decode image '{imagePath}'. The file may be corrupted or unsupported.");
        }

        using var rgb = new Mat();
        Cv2.CvtColor(bgr, rgb, ColorConversionCodes.BGR2RGB);

        ApplyOptionalPreprocessing(rgb);

        using var continuous = rgb.Clone(); // Clone to guarantee contiguous memory.
        var width = continuous.Cols;
        var height = continuous.Rows;
        var channels = continuous.Channels();

        if (channels != 3)
        {
            _logger.LogWarning("Expected 3-channel RGB image but received {Channels} channels. Excess channels will be discarded.", channels);
        }

        var buffer = new byte[width * height * 3];
        Marshal.Copy(continuous.Data, buffer, 0, buffer.Length);

        var imageData = new ImageData(width, height, PixelFormat.Rgb, buffer);
        return Task.FromResult(imageData);
    }

    private void ValidateExtension(string path)
    {
        var extension = IOPath.GetExtension(path);
        if (string.IsNullOrWhiteSpace(extension) || !SupportedExtensions.Contains(extension))
        {
            _logger.LogError("Unsupported image extension {Extension} for path {Path}.", extension, path);
            throw new NotSupportedException($"Image format '{extension}' is not supported. Supported extensions: .png, .jpg, .jpeg");
        }
    }

    private void ApplyOptionalPreprocessing(Mat rgb)
    {
        if (!_settings.EnableSmoothing)
        {
            return;
        }

        if (_settings.SmoothingKernelSize <= 1 || _settings.SmoothingKernelSize % 2 == 0)
        {
            _logger.LogWarning("Invalid smoothing kernel size {KernelSize}. Skipping smoothing.", _settings.SmoothingKernelSize);
            return;
        }

        Cv2.GaussianBlur(
            rgb,
            rgb,
            new Size(_settings.SmoothingKernelSize, _settings.SmoothingKernelSize),
            _settings.SmoothingSigma);
    }
}

/// <summary>
/// 画像読込時の前処理設定。
/// </summary>
public sealed class ImageReaderSettings
{
    private ImageReaderSettings()
    {
    }

    /// <summary>
    /// 既定設定。小さなガウシアンぼかしを有効化。
    /// </summary>
    public static ImageReaderSettings Default { get; } = new()
    {
        EnableSmoothing = false,
        SmoothingKernelSize = 3,
        SmoothingSigma = 0.0
    };

    /// <summary>
    /// 平滑化を適用するかどうか。
    /// </summary>
    public bool EnableSmoothing { get; init; }

    /// <summary>
    /// ガウシアンカーネルのサイズ（奇数）。
    /// </summary>
    public int SmoothingKernelSize { get; init; }

    /// <summary>
    /// ガウシアンフィルタのσ値。
    /// </summary>
    public double SmoothingSigma { get; init; }

    /// <summary>
    /// 平滑化なし設定を作成します。
    /// </summary>
    public static ImageReaderSettings WithoutSmoothing() => new()
    {
        EnableSmoothing = false,
        SmoothingKernelSize = 3,
        SmoothingSigma = 0.0
    };
}
