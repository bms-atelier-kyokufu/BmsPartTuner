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

    [Conditional("DEBUG")]
    public static void WriteTrace(string tag, string message) => Log(LogLevel.Trace, tag, message);

    [Conditional("DEBUG")]
    public static void WriteVerbose(string tag, string message) => Log(LogLevel.Verbose, tag, message);

    [Conditional("DEBUG")]
    public static void WriteDebug(string tag, string message) => Log(LogLevel.Debug, tag, message);

    [Conditional("DEBUG")]
    public static void WriteInfo(string tag, string message) => Log(LogLevel.Info, tag, message);

    [Conditional("DEBUG")]
    public static void WriteWarning(string tag, string message) => Log(LogLevel.Warning, tag, message);

    [Conditional("DEBUG")]
    public static void WriteError(string tag, string message) => Log(LogLevel.Error, tag, message);

    [Conditional("DEBUG")]
    public static void WriteError(string tag, string message, Exception ex)
    {
        if (LogLevel.Error < ActiveLogLevel) return;
        var sb = new StringBuilder();
        sb.AppendLine(message);
        sb.AppendLine($"Exception: {ex.GetType().FullName} - {ex.Message}");
        sb.AppendLine($"Stack Trace: {ex.StackTrace}");
        Log(LogLevel.Error, tag, sb.ToString());
    }

    [Conditional("DEBUG")]
    public static void LogMemoryUsage(string tag)
    {
        if (LogLevel.Debug < ActiveLogLevel) return;
        var process = System.Diagnostics.Process.GetCurrentProcess();
        long mem = process.WorkingSet64 / (1024 * 1024);
        long privateMem = process.PrivateMemorySize64 / (1024 * 1024);
        long gcMem = GC.GetTotalMemory(false) / (1024 * 1024);
        Log(LogLevel.Debug, tag, $"WorkingSet={mem}MB, Private={privateMem}MB, GC={gcMem}MB");
    }

    public static IDisposable MeasureTime(string tag, string scopeName, LogLevel level = LogLevel.Debug)
    {
        return new MeasureScope(tag, scopeName, level);
    }

    [Conditional("DEBUG")]
    public static void Clear()
    {
        try
        {
            if (File.Exists(LogPath)) File.Delete(LogPath);
        }
        catch { }
    }

    private static readonly ConcurrentDictionary<string, long> _accumulatedTimes = new();

    [Conditional("DEBUG")]
    public static void AddInterval(string key, long elapsedMilliseconds)
    {
        _accumulatedTimes.AddOrUpdate(key, elapsedMilliseconds, (_, current) => current + elapsedMilliseconds);
    }

    [Conditional("DEBUG")]
    public static void PrintAccumulated(string tag, string prefixMessage, LogLevel level = LogLevel.Debug)
    {
        if (level < ActiveLogLevel) return;
        if (_accumulatedTimes.IsEmpty) return;
        var parts = _accumulatedTimes.Select(static kv => $"{kv.Key}: {kv.Value} ms").ToArray();
        Log(level, tag, $"{prefixMessage} [{string.Join(", ", parts)}]");
    }

    [Conditional("DEBUG")]
    public static void PrintAccumulatedGrouped(string tag, string title, LogLevel level = LogLevel.Debug)
    {
        if (level < ActiveLogLevel) return;
        if (_accumulatedTimes.IsEmpty) return;

        var grouped = _accumulatedTimes
            .Select(static kv =>
            {
                var parts = kv.Key.Split('|');
                return new
                {
                    Group = parts.Length > 1 ? parts[0] : "Global",
                    Metric = parts.Length > 1 ? parts[1] : parts[0],
                    kv.Value
                };
            })
            .GroupBy(static x => x.Group)
            .OrderByDescending(static g => g.Sum(static x => x.Value));

        var sb = new StringBuilder();
        sb.AppendLine($"=== {title} ===");
        foreach (var g in grouped)
        {
            var metricsStr = string.Join(", ", g.Select(static x => $"{x.Metric}: {x.Value} ms"));
            sb.AppendLine($"  [{g.Key}] {metricsStr}");
        }
        Log(level, tag, sb.ToString());
    }

    [Conditional("DEBUG")]
    public static void ClearAccumulated()
    {
        _accumulatedTimes.Clear();
    }

    public class PerformanceTimer
    {
        private readonly Stopwatch _sw = Stopwatch.StartNew();

        public long Lap(string key)
        {
            long elapsed = _sw.ElapsedMilliseconds;
            AddInterval(key, elapsed);
            _sw.Restart();
            return elapsed;
        }
    }

    public static PerformanceTimer StartTimer() => new();

    private class MeasureScope : IDisposable
    {
        private readonly string _tag;
        private readonly string _scopeName;
        private readonly LogLevel _level;
        private readonly Stopwatch _sw;

        public MeasureScope(string tag, string scopeName, LogLevel level)
        {
            _tag = tag;
            _scopeName = scopeName;
            _level = level;
            _sw = Stopwatch.StartNew();
            Log(_level, _tag, $"[Start Scope] {_scopeName}");
        }

        public void Dispose()
        {
            _sw.Stop();
            Log(_level, _tag, $"[End Scope] {_scopeName} took {_sw.ElapsedMilliseconds} ms");
        }
    }

    // --- メモリ診断用機能 ---
    private static DateTime? _diagnosisStartTime = null;

    [Conditional("DEBUG")]
    public static void StartMemoryDiagnosis()
    {
        _diagnosisStartTime = DateTime.UtcNow;
        Log(LogLevel.Info, "MemoryDiag", "=== Memory Diagnosis Started ===");
    }

    [Conditional("DEBUG")]
    public static void CheckAndHaltIfDiagnosisTriggered(string tag, string context, object? targetObject = null)
    {
        if (_diagnosisStartTime == null) return;

        var elapsed = DateTime.UtcNow - _diagnosisStartTime.Value;
        if (elapsed.TotalSeconds >= 5)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            long totalMemory = GC.GetTotalMemory(true);
            long workingSet = Environment.WorkingSet;

            var sb = new StringBuilder();
            sb.AppendLine("============================================");
            sb.AppendLine("         MEMORY DIAGNOSIS REPORT");
            sb.AppendLine("============================================");
            sb.AppendLine($"Context      : {context}");
            sb.AppendLine($"Elapsed Time : {elapsed.TotalSeconds:F2} seconds");
            sb.AppendLine($"Managed Mem  : {totalMemory / 1024.0 / 1024.0:F2} MB");
            sb.AppendLine($"Working Set  : {workingSet / 1024.0 / 1024.0:F2} MB");

            if (targetObject != null)
            {
                sb.AppendLine($"Object Type  : {targetObject.GetType().FullName}");
                if (targetObject is System.Collections.IEnumerable enumerable)
                {
                    int count = 0;
                    foreach (var item in enumerable) count++;
                    sb.AppendLine($"Item Count   : {count}");
                }

                if (targetObject is ConcurrentDictionary<string, ICachedSoundData> cacheMap)
                {
                    double totalEstMb = 0;
                    foreach (var kvp in cacheMap)
                    {
                        if (kvp.Value != null) totalEstMb += kvp.Value.EstimatedMemoryMB;
                    }
                    sb.AppendLine($"Sum EstimatedMemoryMB of CachedSoundData: {totalEstMb:F2} MB");
                }
            }
            sb.AppendLine("============================================");

            string report = sb.ToString();
            Log(LogLevel.Trace, tag, report);
            Log(LogLevel.Trace, tag, "Memory diagnosis halt triggered: 5 seconds elapsed.");
        }
    }
}

/// <summary>
/// クラス名をタグとして自動付与するジェネリックなパフォーマンスロガー
/// </summary>
/// <typeparam name="T">ログを出力するクラスの型</typeparam>
public static class PerformanceDebugLogger<T>
{
    private static readonly string Tag = typeof(T).Name;

    [Conditional("DEBUG")]
    public static void WriteTrace(string message) => PerformanceDebugLogger.WriteTrace(Tag, message);

    [Conditional("DEBUG")]
    public static void WriteVerbose(string message) => PerformanceDebugLogger.WriteVerbose(Tag, message);

    [Conditional("DEBUG")]
    public static void WriteDebug(string message) => PerformanceDebugLogger.WriteDebug(Tag, message);

    [Conditional("DEBUG")]
    public static void WriteInfo(string message) => PerformanceDebugLogger.WriteInfo(Tag, message);

    [Conditional("DEBUG")]
    public static void WriteWarning(string message) => PerformanceDebugLogger.WriteWarning(Tag, message);

    [Conditional("DEBUG")]
    public static void WriteError(string message) => PerformanceDebugLogger.WriteError(Tag, message);

    [Conditional("DEBUG")]
    public static void WriteError(string message, Exception ex) => PerformanceDebugLogger.WriteError(Tag, message, ex);

    [Conditional("DEBUG")]
    public static void LogMemoryUsage() => PerformanceDebugLogger.LogMemoryUsage(Tag);

    public static IDisposable MeasureTime(string scopeName, LogLevel level = LogLevel.Debug)
        => PerformanceDebugLogger.MeasureTime(Tag, scopeName, level);

    [Conditional("DEBUG")]
    public static void PrintAccumulated(string prefixMessage, LogLevel level = LogLevel.Debug)
        => PerformanceDebugLogger.PrintAccumulated(Tag, prefixMessage, level);

    [Conditional("DEBUG")]
    public static void PrintAccumulatedGrouped(string title, LogLevel level = LogLevel.Debug)
        => PerformanceDebugLogger.PrintAccumulatedGrouped(Tag, title, level);

    [Conditional("DEBUG")]
    public static void CheckAndHaltIfDiagnosisTriggered(string context, object? targetObject = null)
        => PerformanceDebugLogger.CheckAndHaltIfDiagnosisTriggered(Tag, context, targetObject);
}

/// <summary>
/// インスタンスとして保持可能な型安全ロガー
/// </summary>
public class TypedLogger
{
    private readonly string _tag;

    public TypedLogger(Type type)
    {
        _tag = type.Name;
    }

    public TypedLogger(string tag)
    {
        _tag = tag;
    }

    [Conditional("DEBUG")]
    public void WriteTrace(string message) => PerformanceDebugLogger.WriteTrace(_tag, message);

    [Conditional("DEBUG")]
    public void WriteVerbose(string message) => PerformanceDebugLogger.WriteVerbose(_tag, message);

    [Conditional("DEBUG")]
    public void WriteDebug(string message) => PerformanceDebugLogger.WriteDebug(_tag, message);

    [Conditional("DEBUG")]
    public void WriteInfo(string message) => PerformanceDebugLogger.WriteInfo(_tag, message);

    [Conditional("DEBUG")]
    public void WriteWarning(string message) => PerformanceDebugLogger.WriteWarning(_tag, message);

    [Conditional("DEBUG")]
    public void WriteError(string message) => PerformanceDebugLogger.WriteError(_tag, message);

    [Conditional("DEBUG")]
    public void WriteError(string message, Exception ex) => PerformanceDebugLogger.WriteError(_tag, message, ex);

    [Conditional("DEBUG")]
    public void LogMemoryUsage() => PerformanceDebugLogger.LogMemoryUsage(_tag);

    public IDisposable MeasureTime(string scopeName, LogLevel level = LogLevel.Debug)
        => PerformanceDebugLogger.MeasureTime(_tag, scopeName, level);
}
