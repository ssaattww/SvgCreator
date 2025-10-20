namespace SvgCreator.Core.Diagnostics;

/// <summary>
/// デバッグ用に保存されるラスターマスク情報です。
/// </summary>
public sealed class DebugSnapshotMask
{
    public required int Width { get; init; }

    public required int Height { get; init; }

    public required bool[] Bits { get; init; }
}
