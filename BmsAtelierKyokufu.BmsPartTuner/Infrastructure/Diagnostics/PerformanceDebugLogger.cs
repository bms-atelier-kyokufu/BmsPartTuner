using System.Threading.Channels;

namespace BmsAtelierKyokufu.BmsPartTuner.Infrastructure.Diagnostics;

public enum LogLevel
{
    Trace = 0,
    Verbose = 1,
    Debug = 2,
    Info = 3,
    Warning = 4,
    Error = 5,
    None = 6
}

[ADRAnchor("OPT-03", nameof(PerformanceDebugLogger))]
public static class PerformanceDebugLogger
{
    private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "perf_measure.log");
    private static readonly Channel<string> _logChannel = Channel.CreateUnbounded<string>();
    private static readonly CancellationTokenSource _cts = new();

    public static LogLevel ActiveLogLevel { get; set; } = LogLevel.Debug;

    static PerformanceDebugLogger()
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

    public static void Shutdown()
    {
        _logChannel.Writer.Complete();
        _cts.Cancel();
    }

    private static void Log(LogLevel level, string tag, string message)
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

    [Conditional("DEBUG")] public static void WriteTrace(string tag, string msg) => Log(LogLevel.Trace, tag, msg);
    [Conditional("DEBUG")] public static void WriteVerbose(string tag, string msg) => Log(LogLevel.Verbose, tag, msg);
    [Conditional("DEBUG")] public static void WriteDebug(string tag, string msg) => Log(LogLevel.Debug, tag, msg);
    [Conditional("DEBUG")] public static void WriteInfo(string tag, string msg) => Log(LogLevel.Info, tag, msg);
    [Conditional("DEBUG")] public static void WriteWarning(string tag, string msg) => Log(LogLevel.Warning, tag, msg);
    [Conditional("DEBUG")] public static void WriteError(string tag, string msg) => Log(LogLevel.Error, tag, msg);

    [Conditional("DEBUG")]
    public static void WriteError(string tag, string message, Exception ex)
    {
        if (LogLevel.Error < ActiveLogLevel) return;
        Log(LogLevel.Error, tag, $"{message}\nException: {ex.GetType().FullName} - {ex.Message}\nStack Trace: {ex.StackTrace}");
    }

    [Conditional("DEBUG")]
    public static void LogMemoryUsage(string tag)
    {
        if (LogLevel.Debug < ActiveLogLevel) return;
        var process = System.Diagnostics.Process.GetCurrentProcess();
        Log(LogLevel.Debug, tag, $"WorkingSet={process.WorkingSet64 / 1024 / 1024}MB, Private={process.PrivateMemorySize64 / 1024 / 1024}MB, GC={GC.GetTotalMemory(false) / 1024 / 1024}MB");
    }

    public static IDisposable MeasureTime(string tag, string scopeName, LogLevel level = LogLevel.Debug)
        => new MeasureScope(tag, scopeName, level);

    [Conditional("DEBUG")]
    public static void Clear() { try { if (File.Exists(LogPath)) File.Delete(LogPath); } catch { } }

    private static readonly ConcurrentDictionary<string, long> _accumulatedTimes = new();

    [Conditional("DEBUG")]
    public static void AddInterval(string key, long elapsedMilliseconds)
        => _accumulatedTimes.AddOrUpdate(key, elapsedMilliseconds, (_, current) => current + elapsedMilliseconds);

    [Conditional("DEBUG")]
    public static void PrintAccumulated(string tag, string prefixMessage, LogLevel level = LogLevel.Debug)
    {
        if (level < ActiveLogLevel || _accumulatedTimes.IsEmpty) return;
        Log(level, tag, $"{prefixMessage} [{string.Join(", ", _accumulatedTimes.Select(kv => $"{kv.Key}: {kv.Value} ms"))}]");
    }

    [Conditional("DEBUG")]
    public static void PrintAccumulatedGrouped(string tag, string title, LogLevel level = LogLevel.Debug)
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

    [Conditional("DEBUG")] public static void ClearAccumulated() => _accumulatedTimes.Clear();

    public class PerformanceTimer
    {
        private readonly Stopwatch _sw = Stopwatch.StartNew();
        public long Lap(string key) { long elapsed = _sw.ElapsedMilliseconds; AddInterval(key, elapsed); _sw.Restart(); return elapsed; }
    }

    public static PerformanceTimer StartTimer() => new();

    private class MeasureScope : IDisposable
    {
        private readonly string _tag, _scopeName;
        private readonly LogLevel _level;
        private readonly Stopwatch _sw;
        public MeasureScope(string tag, string scopeName, LogLevel level)
        {
            (_tag, _scopeName, _level, _sw) = (tag, scopeName, level, Stopwatch.StartNew());
            Log(_level, _tag, $"[Start Scope] {_scopeName}");
        }
        public void Dispose() { _sw.Stop(); Log(_level, _tag, $"[End Scope] {_scopeName} took {_sw.ElapsedMilliseconds} ms"); }
    }

    private static DateTime? _diagnosisStartTime = null;

    [Conditional("DEBUG")]
    public static void StartMemoryDiagnosis() { _diagnosisStartTime = DateTime.UtcNow; Log(LogLevel.Info, "MemoryDiag", "=== Memory Diagnosis Started ==="); }

    [Conditional("DEBUG")]
    public static void CheckAndHaltIfDiagnosisTriggered(string tag, string context, object? targetObject = null)
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
/// パフォーマンス計測・デバッグ用のロガーインターフェース
/// </summary>
public interface IPerformanceLogger
{
    string Tag { get; }
}

/// <summary>
/// IPerformanceLogger用の拡張メソッド（コンパイラによる呼び出しの最適化を含む）
/// </summary>
public static class PerformanceLoggerExtensions
{
    [Conditional("DEBUG")]
    public static void WriteTrace(this IPerformanceLogger logger, string msg)
        => PerformanceDebugLogger.WriteTrace(logger.Tag, msg);
    [Conditional("DEBUG")]
    public static void WriteVerbose(this IPerformanceLogger logger, string msg)
        => PerformanceDebugLogger.WriteVerbose(logger.Tag, msg);
    [Conditional("DEBUG")]
    public static void WriteDebug(this IPerformanceLogger logger, string msg)
        => PerformanceDebugLogger.WriteDebug(logger.Tag, msg);
    [Conditional("DEBUG")]
    public static void WriteInfo(this IPerformanceLogger logger, string msg)
        => PerformanceDebugLogger.WriteInfo(logger.Tag, msg);
    [Conditional("DEBUG")]
    public static void WriteWarning(this IPerformanceLogger logger, string msg)
        => PerformanceDebugLogger.WriteWarning(logger.Tag, msg);
    [Conditional("DEBUG")]
    public static void WriteError(this IPerformanceLogger logger, string msg)
        => PerformanceDebugLogger.WriteError(logger.Tag, msg);
    [Conditional("DEBUG")]
    public static void WriteError(this IPerformanceLogger logger, string msg, Exception ex)
        => PerformanceDebugLogger.WriteError(logger.Tag, msg, ex);
    [Conditional("DEBUG")]
    public static void LogMemoryUsage(this IPerformanceLogger logger)
        => PerformanceDebugLogger.LogMemoryUsage(logger.Tag);
    public static IDisposable MeasureTime(this IPerformanceLogger logger, string scope, LogLevel level = LogLevel.Debug)
        => PerformanceDebugLogger.MeasureTime(logger.Tag, scope, level);
    [Conditional("DEBUG")]
    public static void PrintAccumulated(this IPerformanceLogger logger, string prefix, LogLevel level = LogLevel.Debug)
        => PerformanceDebugLogger.PrintAccumulated(logger.Tag, prefix, level);
    [Conditional("DEBUG")]
    public static void PrintAccumulatedGrouped(this IPerformanceLogger logger, string title, LogLevel level = LogLevel.Debug)
        => PerformanceDebugLogger.PrintAccumulatedGrouped(logger.Tag, title, level);
    [Conditional("DEBUG")]
    public static void CheckAndHaltIfDiagnosisTriggered(this IPerformanceLogger logger, string context, object? target = null)
        => PerformanceDebugLogger.CheckAndHaltIfDiagnosisTriggered(logger.Tag, context, target);
}

/// <summary>
/// インスタンスとして保持可能な型安全ロガー
/// </summary>
public class TypedLogger : IPerformanceLogger
{
    public string Tag { get; }
    public TypedLogger(Type type) => Tag = type.Name;
    public TypedLogger(string tag) => Tag = tag;
}
