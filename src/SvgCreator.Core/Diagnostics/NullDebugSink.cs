using System.Threading;
using System.Threading.Tasks;

namespace SvgCreator.Core.Diagnostics;

/// <summary>
/// デバッグ出力を行わないシンクです。
/// </summary>
public sealed class NullDebugSink : IDebugSink
{
    public Task WriteSnapshotAsync(string stageName, DebugSnapshot snapshot, DebugExecutionContext context, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task CompleteAsync(DebugExecutionContext context, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
