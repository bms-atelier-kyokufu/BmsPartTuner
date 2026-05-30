using System.Threading.Channels;

namespace BmsAtelierKyokufu.BmsPartTuner.Infrastructure.Diagnostics;

/// <summary>
/// ログの出力レベル。
/// </summary>
public enum LogLevel
{
    /// <summary>詳細なトレース情報。</summary>
    Trace = 0,
    /// <summary>詳細なデバッグ情報。</summary>
    Verbose = 1,
    /// <summary>一般的なデバッグ情報。</summary>
    Debug = 2,
    /// <summary>一般的な通知情報。</summary>
    Info = 3,
    /// <summary>警告情報。</summary>
    Warning = 4,
    /// <summary>エラー情報。</summary>
    Error = 5,
    /// <summary>ログ出力を無効化する。</summary>
    None = 6
}

/// <summary>
/// パフォーマンス計測および診断ログの出力を行う基底クラス。
/// </summary>
[ADRAnchor("OPT-03", nameof(Logger))]
public abstract class Logger
{
    /// <summary>
    /// ログファイルの出力先パス。
    /// </summary>
    private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "perf_measure.log");

    /// <summary>
    /// ログメッセージを非同期にバッファリングするチャネル。
    /// </summary>
    private static readonly Channel<string> _logChannel = Channel.CreateUnbounded<string>();

    /// <summary>
    /// バックグラウンド処理キャンセルのためのトークンソース。
    /// </summary>
    private static readonly CancellationTokenSource _cts = new();

    /// <summary>
    /// 現在有効なログ出力のしきい値レベル。
    /// </summary>
    public static LogLevel ActiveLogLevel { get; set; } = LogLevel.Debug;

    /// <summary>
    /// 静的コンストラクタ。デフォルトログレベルのロードとバックグラウンド処理を開始する。
    /// </summary>
    static Logger()
    {
        try
        {
            ActiveLogLevel = AppConstants.Logging.DefaultLogLevel;
        }
        catch
        {
            ActiveLogLevel = LogLevel.Debug;
        }

        // バックグラウンドでログ書き込みタスクを開始
        Task.Run(ProcessLogQueueAsync);
    }

    /// <summary>
    /// チャネルからログを読み取り、バッチ処理でファイルに書き込む。
    /// </summary>
    /// <returns>非同期タスク。</returns>
    private static async Task ProcessLogQueueAsync()
    {
        try
        {
            // バッチ書き込み用
            var buffer = new List<string>(100);

            await foreach (var logMessage in _logChannel.Reader.ReadAllAsync(_cts.Token))
            {
                buffer.Add(logMessage);

                // 少し待ってキューに溜まっていればまとめて書き込む
                while (_logChannel.Reader.TryRead(out var msg))
                {
                    buffer.Add(msg);
                    if (buffer.Count >= 100) break;
                }

                try
                {
                    File.AppendAllLines(LogPath, buffer);
                }
                catch { } // I/Oエラー無視

                buffer.Clear();
            }
        }
        catch (OperationCanceledException)
        {
            // シャットダウン時
        }
    }

    /// <summary>
    /// ログ書き込み処理を安全に終了し、リソースを解放する。
    /// </summary>
    public static void Shutdown()
    {
        _logChannel.Writer.Complete();
        _cts.Cancel();
    }

    /// <summary>
    /// ログを指定されたレベルとタグで出力する。
    /// </summary>
    /// <param name="level">ログレベル。</param>
    /// <param name="tag">識別用タグ。</param>
    /// <param name="message">出力するメッセージ。</param>
    protected static void Log(LogLevel level, string tag, string message)
    {
        if (level < ActiveLogLevel) return;
        if (message == null) return;

        var timestamp = $"[{DateTime.Now:HH:mm:ss.fff}]";
        var levelStr = level.ToString().ToUpper();

        // ANSIカラーの割り当て (コンソールやVS拡張出力用)
        string colorPrefix = level switch
        {
            LogLevel.Trace => "\x1b[90m",   // Gray
            LogLevel.Verbose => "\x1b[36m", // Cyan
            LogLevel.Debug => "\x1b[37m",   // White
            LogLevel.Info => "\x1b[32m",    // Green
            LogLevel.Warning => "\x1b[33m", // Yellow
            LogLevel.Error => "\x1b[31m",   // Red
            _ => "\x1b[0m"
        };
        const string colorSuffix = "\x1b[0m";

        // フォーマット: [Time] [LEVEL] [Tag] Message
        var logLine = $"{timestamp} [{levelStr}] [{tag}] {message}";

        // ターミナル用にはカラーリングして出力
        Debug.WriteLine($"{colorPrefix}{logLine}{colorSuffix}");

        // ファイル用にはプレーンテキストをキューへ
        _logChannel.Writer.TryWrite(logLine);
    }

    /// <summary>
    /// ログファイルを削除する。
    /// </summary>
    [Conditional("DEBUG")]
    public static void Clear() { try { if (File.Exists(LogPath)) File.Delete(LogPath); } catch { } }

    /// <summary>
    /// 累積計測時間を保持するスレッドセーフな辞書。
    /// </summary>
    private static readonly ConcurrentDictionary<string, long> _accumulatedTimes = new();

    /// <summary>
    /// 指定されたキーに対して経過時間をミリ秒単位で加算する。
    /// </summary>
    /// <param name="key">計測キー。</param>
    /// <param name="elapsedMilliseconds">加算するミリ秒数。</param>
    [Conditional("DEBUG")]
    protected static void AddInterval(string key, long elapsedMilliseconds)
        => _accumulatedTimes.AddOrUpdate(key, elapsedMilliseconds, (_, current) => current + elapsedMilliseconds);

    /// <summary>
    /// 累積された計測時間をコンソールおよびファイルに出力する。
    /// </summary>
    /// <param name="tag">識別用タグ。</param>
    /// <param name="prefixMessage">出力のプレフィックスメッセージ。</param>
    /// <param name="level">出力するログレベル。</param>
    [Conditional("DEBUG")]
    protected static void PrintAccumulatedInternal(string tag, string prefixMessage, LogLevel level = LogLevel.Debug)
    {
        if (level < ActiveLogLevel || _accumulatedTimes.IsEmpty) return;
        Log(level, tag, $"{prefixMessage} [{string.Join(", ", _accumulatedTimes.Select(kv => $"{kv.Key}: {kv.Value} ms"))}]");
    }

    /// <summary>
    /// 累積された計測時間をキーのプレフィックスごとにグループ化して出力する。
    /// </summary>
    /// <param name="tag">識別用タグ。</param>
    /// <param name="title">グループ化出力のタイトル。</param>
    /// <param name="level">出力するログレベル。</param>
    [Conditional("DEBUG")]
    protected static void PrintAccumulatedGroupedInternal(string tag, string title, LogLevel level = LogLevel.Debug)
    {
        if (level < ActiveLogLevel || _accumulatedTimes.IsEmpty) return;
        var grouped = _accumulatedTimes
            .Select(kv =>
            {
                var parts = kv.Key.Split('|');
                return new { Group = parts.Length > 1 ? parts[0] : "Global", Metric = parts.Length > 1 ? parts[1] : parts[0], kv.Value };
            })
            .GroupBy(x => x.Group)
            .OrderByDescending(g => g.Sum(x => x.Value));

        var sb = new StringBuilder().AppendLine($"=== {title} ===");
        foreach (var g in grouped)
            sb.AppendLine($"  [{g.Key}] {string.Join(", ", g.Select(x => $"{x.Metric}: {x.Value} ms"))}");
        Log(level, tag, sb.ToString());
    }

    /// <summary>
    /// 累積されたすべての計測時間をクリアする。
    /// </summary>
    [Conditional("DEBUG")] public static void ClearAccumulated() => _accumulatedTimes.Clear();

    /// <summary>
    /// ラップタイムの計測および累積時間の記録を行うタイマー。
    /// </summary>
    public class PerformanceTimer
    {
        private readonly Stopwatch _sw = Stopwatch.StartNew();

        /// <summary>
        /// 前回の計測からの経過時間を取得し、指定されたキーにミリ秒単位で累積する。
        /// </summary>
        /// <param name="key">計測キー。</param>
        /// <returns>前回からの経過時間（ミリ秒）。</returns>
        public long Lap(string key) { long elapsed = _sw.ElapsedMilliseconds; AddInterval(key, elapsed); _sw.Restart(); return elapsed; }
    }

    /// <summary>
    /// スコープの開始から終了までの時間を自動的に計測して出力するスコープオブジェクト。
    /// </summary>
    public class MeasureScope : IDisposable
    {
        private readonly string _tag, _scopeName;
        private readonly LogLevel _level;
        private readonly Stopwatch _sw;

        /// <summary>
        /// インスタンスを初期化し、計測スコープを開始する。
        /// </summary>
        /// <param name="tag">識別用タグ。</param>
        /// <param name="scopeName">スコープ名。</param>
        /// <param name="level">出力するログレベル。</param>
        public MeasureScope(string tag, string scopeName, LogLevel level)
        {
            (_tag, _scopeName, _level, _sw) = (tag, scopeName, level, Stopwatch.StartNew());
            Log(_level, _tag, $"[Start Scope] {_scopeName}");
        }

        /// <summary>
        /// スコープを終了し、経過時間を出力する。
        /// </summary>
        public void Dispose() { _sw.Stop(); Log(_level, _tag, $"[End Scope] {_scopeName} took {_sw.ElapsedMilliseconds} ms"); }
    }

    /// <summary>
    /// メモリ診断の開始日時。
    /// </summary>
    private static DateTime? _diagnosisStartTime = null;

    /// <summary>
    /// メモリ診断を開始する。
    /// </summary>
    [Conditional("DEBUG")]
    public static void StartMemoryDiagnosis() { _diagnosisStartTime = DateTime.UtcNow; Log(LogLevel.Info, "MemoryDiag", "=== Memory Diagnosis Started ==="); }

    /// <summary>
    /// 診断開始から一定時間経過している場合にメモリ使用量を診断し、レポートを出力する。
    /// </summary>
    /// <param name="tag">識別用タグ。</param>
    /// <param name="context">実行コンテキスト。</param>
    /// <param name="targetObject">診断対象のオブジェクト。</param>
    [Conditional("DEBUG")]
    protected static void DiagnosisTriggeredInternal(string tag, string context, object? targetObject = null)
    {
        if (_diagnosisStartTime == null) return;
        var elapsed = DateTime.UtcNow - _diagnosisStartTime.Value;
        if (elapsed.TotalSeconds >= 5)
        {
            GC.Collect(); GC.WaitForPendingFinalizers();
            var sb = new StringBuilder()
                .AppendLine("============================================")
                .AppendLine("         MEMORY DIAGNOSIS REPORT")
                .AppendLine("============================================")
                .AppendLine($"Context      : {context}")
                .AppendLine($"Elapsed Time : {elapsed.TotalSeconds:F2} seconds")
                .AppendLine($"Managed Mem  : {GC.GetTotalMemory(true) / 1024.0 / 1024.0:F2} MB")
                .AppendLine($"Working Set  : {Environment.WorkingSet / 1024.0 / 1024.0:F2} MB");

            if (targetObject != null)
            {
                sb.AppendLine($"Object Type  : {targetObject.GetType().FullName}");
                if (targetObject is System.Collections.IEnumerable enumerable)
                {
                    int count = enumerable.Cast<object>().Count();
                    sb.AppendLine($"Item Count   : {count}");
                }
                if (targetObject is ConcurrentDictionary<string, ICachedSoundData> cacheMap)
                {
                    sb.AppendLine($"Sum EstimatedMemoryMB of CachedSoundData: {cacheMap.Values.Sum(v => v?.EstimatedMemoryMB ?? 0):F2} MB");
                }
            }
            sb.AppendLine("============================================");
            Log(LogLevel.Trace, tag, sb.ToString());
            Log(LogLevel.Trace, tag, "Memory diagnosis halt triggered: 5 seconds elapsed.");
        }
    }
}

/// <summary>
/// 特定の型に関連付けられた、インスタンス単位でのログ出力機能を提供する。
/// </summary>
/// <typeparam name="T">ログを関連付けるクラスの型。</typeparam>
public class Logger<T> : Logger
{
    /// <summary>
    /// ログ出力に使用するタグ。
    /// </summary>
    public string Tag => LogTagCache<T>.Tag;

    /// <summary>
    /// Trace レベルのログを出力する。
    /// </summary>
    /// <param name="msg">出力するメッセージ。</param>
    [Conditional("DEBUG")] public void WriteTrace(string msg) => Log(LogLevel.Trace, Tag, msg);

    /// <summary>
    /// Verbose レベルのログを出力する。
    /// </summary>
    /// <param name="msg">出力するメッセージ。</param>
    [Conditional("DEBUG")] public void WriteVerbose(string msg) => Log(LogLevel.Verbose, Tag, msg);

    /// <summary>
    /// Debug レベルのログを出力する。
    /// </summary>
    /// <param name="msg">出力するメッセージ。</param>
    [Conditional("DEBUG")] public void WriteDebug(string msg) => Log(LogLevel.Debug, Tag, msg);

    /// <summary>
    /// Info レベルのログを出力する。
    /// </summary>
    /// <param name="msg">出力するメッセージ。</param>
    [Conditional("DEBUG")] public void WriteInfo(string msg) => Log(LogLevel.Info, Tag, msg);

    /// <summary>
    /// Warning レベルのログを出力する。
    /// </summary>
    /// <param name="msg">出力するメッセージ。</param>
    [Conditional("DEBUG")] public void WriteWarning(string msg) => Log(LogLevel.Warning, Tag, msg);

    /// <summary>
    /// Error レベルのログを出力する。
    /// </summary>
    /// <param name="msg">出力するメッセージ。</param>
    [Conditional("DEBUG")] public void WriteError(string msg) => Log(LogLevel.Error, Tag, msg);

    /// <summary>
    /// 例外情報を含む Error レベルのログを出力する。
    /// </summary>
    /// <param name="message">出力するメッセージ。</param>
    /// <param name="ex">発生した例外。</param>
    [Conditional("DEBUG")]
    public void WriteError(string message, Exception ex)
    {
        if (LogLevel.Error < ActiveLogLevel) return;
        Log(LogLevel.Error, Tag, $"{message}\nException: {ex.GetType().FullName} - {ex.Message}\nStack Trace: {ex.StackTrace}");
    }

    /// <summary>
    /// 現在のメモリ使用状況を出力する。
    /// </summary>
    /// <param name="context">コンテキスト文字列。</param>
    [Conditional("DEBUG")]
    public void LogMemoryUsage(string context = "")
    {
        if (LogLevel.Debug < ActiveLogLevel) return;
        var process = Process.GetCurrentProcess();
        string prefix = string.IsNullOrEmpty(context) ? "" : $"[{context}] ";
        Log(LogLevel.Debug, Tag, $"{prefix}WorkingSet={process.WorkingSet64 / 1024 / 1024}MB, Private={process.PrivateMemorySize64 / 1024 / 1024}MB, GC={GC.GetTotalMemory(false) / 1024 / 1024}MB");
    }

    /// <summary>
    /// 指定されたスコープの実行時間を測定するための使い捨てオブジェクトを生成する。
    /// </summary>
    /// <param name="scopeName">スコープ名。</param>
    /// <param name="level">出力するログレベル。</param>
    /// <returns>スコープ破棄時にログ出力を行うオブジェクト。</returns>
    public IDisposable MeasureTime(string scopeName, LogLevel level = LogLevel.Debug)
        => new MeasureScope(Tag, scopeName, level);

    /// <summary>
    /// パフォーマンス計測用のタイマーを開始する。
    /// </summary>
    /// <returns>パフォーマンスタイマーのインスタンス。</returns>
    public PerformanceTimer StartTimer() => new();

    /// <summary>
    /// 累積された計測時間をプレフィックスメッセージとともに出力する。
    /// </summary>
    /// <param name="prefixMessage">出力のプレフィックスメッセージ。</param>
    /// <param name="level">出力するログレベル。</param>
    [Conditional("DEBUG")]
    public void PrintAccumulated(string prefixMessage, LogLevel level = LogLevel.Debug)
        => PrintAccumulatedInternal(Tag, prefixMessage, level);

    /// <summary>
    /// 累積された計測時間をグループ化して出力する。
    /// </summary>
    /// <param name="title">グループ化出力のタイトル。</param>
    /// <param name="level">出力するログレベル。</param>
    [Conditional("DEBUG")]
    public void PrintAccumulatedGrouped(string title, LogLevel level = LogLevel.Debug)
        => PrintAccumulatedGroupedInternal(Tag, title, level);

    /// <summary>
    /// 診断開始から一定時間経過している場合にメモリ使用量を診断し、レポートを出力する。
    /// </summary>
    /// <param name="context">実行コンテキスト。</param>
    /// <param name="targetObject">診断対象のオブジェクト。</param>
    [Conditional("DEBUG")]
    public void CheckAndHaltIfDiagnosisTriggered(string context, object? targetObject = null)
        => DiagnosisTriggeredInternal(Tag, context, targetObject);
}
