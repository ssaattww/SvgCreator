using System.Threading;
using System.Threading.Tasks;

namespace SvgCreator.Core.Diagnostics;

/// <summary>
/// デバッグスナップショットを受け取り適切に処理するインターフェイスです。
/// </summary>
public interface IDebugSink
{
    /// <summary>
    /// ステージ完了時にスナップショットを受け取り、必要に応じて出力します。
    /// </summary>
    Task WriteSnapshotAsync(string stageName, DebugSnapshot snapshot, DebugExecutionContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// すべての処理が終わった際に必要な後処理を行います。
    /// </summary>
    Task CompleteAsync(DebugExecutionContext context, CancellationToken cancellationToken = default);
}
