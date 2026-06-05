namespace BmsAtelierKyokufu.BmsPartTuner.Core.Context;

/// <summary>
/// 進捗報告の頻度を制限し、UIスレッドのメッセージキューが飽和（フラッディング）するのを防ぐ <see cref="IProgress{T}"/> のラッパー。
/// </summary>
/// <typeparam name="T">進捗情報の型。</typeparam>
/// <remarks>
/// ThrottledProgress の新しいインスタンスを初期化します。
/// </remarks>
/// <param name="underlyingProgress">実際の進捗報告先（通常はUIスレッドで初期化された Progress オブジェクト）。</param>
/// <param name="throttleIntervalMilliseconds">進捗報告の最小間隔（ミリ秒）。デフォルトは 50ms。</param>
[ADRAnchor("ARCH-05", nameof(ThrottledProgress<>))]
public sealed class ThrottledProgress<T>(IProgress<T> underlyingProgress, int throttleIntervalMilliseconds = 50) : IProgress<T>
{
    private readonly IProgress<T> _underlyingProgress = underlyingProgress ?? throw new ArgumentNullException(nameof(underlyingProgress));
    private readonly TimeSpan _throttleInterval = TimeSpan.FromMilliseconds(throttleIntervalMilliseconds);
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    private T? _lastReportedValue;
    private bool _hasValue;

    // スレッドセーフに進捗を処理するためのロック
    private readonly Lock _lock = new();

    /// <summary>
    /// 進捗を報告します。一定時間が経過していない場合は報告をスキップします。
    /// ただし、100%（処理完了）などの特定の条件で確実に報告させるロジックを必要に応じて追加できます。
    /// </summary>
    public void Report(T value)
    {
        lock (_lock)
        {
            _lastReportedValue = value;
            _hasValue = true;

            // 前回報告からの経過時間がしきい値を超えているか確認
            if (_stopwatch.Elapsed >= _throttleInterval)
            {
                ReportNow(value);
            }
        }
    }

    /// <summary>
    /// 最後に報告されていない進捗がある場合、強制的に報告します。
    /// 処理の完了時などに呼び出すことを推奨します。
    /// </summary>
    public void Flush()
    {
        lock (_lock)
        {
            if (_hasValue)
            {
                ReportNow(_lastReportedValue!);
            }
        }
    }

    private void ReportNow(T value)
    {
        _underlyingProgress.Report(value);
        _stopwatch.Restart();
        _hasValue = false;
    }
}
