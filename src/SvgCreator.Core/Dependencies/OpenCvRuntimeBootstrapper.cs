using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace SvgCreator.Core.Dependencies;

/// <summary>
/// OpenCvSharp のネイティブ依存関係を事前に検証し、欠けている場合はプラットフォーム毎の指示を提供します。
/// </summary>
internal static class OpenCvRuntimeBootstrapper
{
    private static volatile bool _validated;
    private static readonly object SyncRoot = new();

    /// <summary>
    /// OpenCvSharp のネイティブ依存性が利用可能であることを検証します。
    /// </summary>
    /// <exception cref="PlatformNotSupportedException">依存ライブラリが不足している場合。</exception>
    public static void EnsureDependenciesAvailable(ILogger? logger = null)
    {
        if (_validated)
        {
            return;
        }

        lock (SyncRoot)
        {
            if (_validated)
            {
                return;
            }

            try
            {
                EnsureLinuxLibraryAliases(logger);
                using var _ = new Mat(1, 1, MatType.CV_8UC1);
                _validated = true;
            }
            catch (TypeInitializationException ex) when (ex.InnerException is DllNotFoundException dll)
            {
                throw CreatePlatformException(dll, logger);
            }
            catch (DllNotFoundException dll)
            {
                throw CreatePlatformException(dll, logger);
            }
        }
    }

    private static PlatformNotSupportedException CreatePlatformException(DllNotFoundException dll, ILogger? logger)
    {
        logger?.LogError(dll, "OpenCvSharp runtime dependencies are missing.");

        var diagnosis = new StringBuilder()
            .AppendLine("OpenCvSharp native runtime could not be loaded. Install the platform dependencies below and re-run SvgCreator.")
            .AppendLine($"Original error: {dll.Message}")
            .AppendLine()
            .AppendLine("Linux (Ubuntu 22.04 / 24.04) prerequisites:")
            .AppendLine("  sudo apt install libopencv-core406 libopencv-imgcodecs406 libopencv-imgproc406 libtesseract5 tesseract-ocr")
            .AppendLine("  # If libtesseract.so.4 is missing, create a compatibility symlink:")
            .AppendLine("  sudo ln -s /usr/lib/x86_64-linux-gnu/libtesseract.so.5 /usr/lib/x86_64-linux-gnu/libtesseract.so.4")
            .AppendLine("  # Depending on FFmpeg/OpenEXR versions, create .so.58/.so.5 aliases or install packages providing them")
            .AppendLine()
            .AppendLine("Windows prerequisites:")
            .AppendLine("  Install the latest Microsoft Visual C++ Redistributable (x64)")
            .AppendLine("  Ensure OpenCvSharp runtime packages are restored (OpenCvSharp4.runtime.win)");

        return new PlatformNotSupportedException(diagnosis.ToString(), dll);
    }

    private static void EnsureLinuxLibraryAliases(ILogger? logger)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return;
        }

        var nativeDirectory = Path.Combine(AppContext.BaseDirectory, "runtimes", "linux-x64", "native");
        if (!Directory.Exists(nativeDirectory))
        {
            return;
        }

        var aliasCandidates = new List<(string ExpectedName, string[] CandidatePaths)>
        {
            ("libavcodec.so.58", new[]
            {
                "/usr/lib/x86_64-linux-gnu/libavcodec.so.60",
                "/lib/x86_64-linux-gnu/libavcodec.so.60"
            }),
            ("libavformat.so.58", new[]
            {
                "/usr/lib/x86_64-linux-gnu/libavformat.so.60",
                "/lib/x86_64-linux-gnu/libavformat.so.60"
            }),
            ("libavutil.so.56", new[]
            {
                "/usr/lib/x86_64-linux-gnu/libavutil.so.58",
                "/lib/x86_64-linux-gnu/libavutil.so.58"
            }),
            ("libswscale.so.5", new[]
            {
                "/usr/lib/x86_64-linux-gnu/libswscale.so.7",
                "/lib/x86_64-linux-gnu/libswscale.so.7"
            }),
            ("libtiff.so.5", new[]
            {
                "/usr/lib/x86_64-linux-gnu/libtiff.so.6",
                "/lib/x86_64-linux-gnu/libtiff.so.6"
            }),
            ("libIlmImf-2_5.so.25", new[]
            {
                "/usr/lib/x86_64-linux-gnu/libOpenEXR-3_1.so.30",
                "/lib/x86_64-linux-gnu/libOpenEXR-3_1.so.30"
            })
        };

        foreach (var (expectedName, candidatePaths) in aliasCandidates)
        {
            var destination = Path.Combine(nativeDirectory, expectedName);
            if (File.Exists(destination))
            {
                continue;
            }

            var source = candidatePaths.FirstOrDefault(File.Exists);
            if (source is null)
            {
                logger?.LogWarning("Linux compatibility shim for {Library} could not be created. Checked paths: {Paths}", expectedName, candidatePaths);
                Console.WriteLine($"[OpenCvRuntimeBootstrapper] Missing dependency for {expectedName}. Checked: {string.Join(", ", candidatePaths)}");
                continue;
            }

            try
            {
                File.Copy(source, destination, overwrite: true);
                logger?.LogInformation("Created compatibility copy for {Library} at {Destination}.", expectedName, destination);
                Console.WriteLine($"[OpenCvRuntimeBootstrapper] Copied {source} -> {destination}");
            }
            catch (Exception copyError)
            {
                logger?.LogWarning(copyError, "Failed to create compatibility copy for {Library} from {Source}.", expectedName, source);
                Console.WriteLine($"[OpenCvRuntimeBootstrapper] Failed to copy {source} -> {destination}: {copyError.Message}");
            }
        }
    }
}
