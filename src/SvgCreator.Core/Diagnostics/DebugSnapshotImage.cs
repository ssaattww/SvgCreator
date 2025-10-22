using SvgCreator.Core.Models;

namespace SvgCreator.Core.Diagnostics;

/// <summary>
/// デバッグ用に保存される画像情報です。
/// </summary>
public sealed class DebugSnapshotImage
{
    /// <summary>
    /// 画像の幅（ピクセル単位）。
    /// </summary>
    public required int Width { get; init; }

    /// <summary>
    /// 画像の高さ（ピクセル単位）。
    /// </summary>
    public required int Height { get; init; }

    /// <summary>
    /// ピクセル形式。
    /// </summary>
    public required PixelFormat Format { get; init; }

    /// <summary>
    /// 行優先で格納されたピクセルデータ。
    /// </summary>
    public required byte[] Pixels { get; init; }
}
