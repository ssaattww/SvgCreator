using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SvgCreator.Core.Diagnostics;

/// <summary>
/// デバッグスナップショットを JSON 形式で入出力するためのサービスです。
/// </summary>
public sealed class DebugSnapshotSerializer
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    /// <summary>
    /// スナップショットを JSON にシリアライズしてストリームへ書き込みます。
    /// </summary>
    /// <param name="snapshot">書き込むスナップショット。</param>
    /// <param name="destination">書き込み先ストリーム。</param>
    /// <param name="cancellationToken">キャンセル トークン。</param>
    public async Task SerializeAsync(DebugSnapshot snapshot, Stream destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(destination);

        await JsonSerializer.SerializeAsync(destination, snapshot, SerializerOptions, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// ストリームから JSON を読み込み、デバッグスナップショットにデシリアライズします。
    /// </summary>
    /// <param name="source">読み込み元ストリーム。</param>
    /// <param name="cancellationToken">キャンセル トークン。</param>
    /// <returns>デシリアライズされたスナップショット。</returns>
    public async Task<DebugSnapshot> DeserializeAsync(Stream source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        var snapshot = await JsonSerializer.DeserializeAsync<DebugSnapshot>(source, SerializerOptions, cancellationToken).ConfigureAwait(false);
        if (snapshot is null)
        {
            throw new InvalidDataException("デバッグスナップショットを読み取れませんでした。");
        }
        if (!string.Equals(snapshot.Version, DebugSnapshot.CurrentVersion, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"サポートされていないデバッグスナップショットのバージョンです: {snapshot.Version}");
        }

        return snapshot;
    }
}
