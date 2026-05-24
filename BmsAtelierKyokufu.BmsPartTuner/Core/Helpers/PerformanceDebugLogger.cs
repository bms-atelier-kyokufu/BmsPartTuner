namespace BmsAtelierKyokufu.BmsPartTuner.Core.Helpers;

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

public static class PerformanceDebugLogger
{
    private static readonly Lock LockObj = new();
    private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "perf_measure.log");

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
    }

    [Conditional("DEBUG")]
    public static void WriteLine(string? message, LogLevel level = LogLevel.Debug)
    {
        if (level < ActiveLogLevel) return;

        lock (LockObj)
        {
            var logMessage = $"[{DateTime.Now:HH:mm:ss.fff}] [{level.ToString().ToUpper()}] {message ?? string.Empty}";
            Debug.WriteLine(logMessage);
            try
            {
                File.AppendAllText(LogPath, logMessage + Environment.NewLine);
            }
            catch { }
        }
    }

    [Conditional("DEBUG")]
    public static void WriteTrace(string? message) => WriteLine(message, LogLevel.Trace);

    [Conditional("DEBUG")]
    public static void WriteVerbose(string? message) => WriteLine(message, LogLevel.Verbose);

    [Conditional("DEBUG")]
    public static void WriteDebug(string? message) => WriteLine(message, LogLevel.Debug);

    [Conditional("DEBUG")]
    public static void WriteInfo(string? message) => WriteLine(message, LogLevel.Info);

    [Conditional("DEBUG")]
    public static void WriteWarning(string? message) => WriteLine(message, LogLevel.Warning);

    [Conditional("DEBUG")]
    public static void WriteError(string? message) => WriteLine(message, LogLevel.Error);

    [Conditional("DEBUG")]
    public static void WriteError(string? message, Exception ex)
    {
        if (LogLevel.Error < ActiveLogLevel) return;
        var sb = new StringBuilder();
        sb.AppendLine(message);
        sb.AppendLine($"Exception: {ex.GetType().FullName} - {ex.Message}");
        sb.AppendLine($"Stack Trace: {ex.StackTrace}");
        WriteLine(sb.ToString(), LogLevel.Error);
    }

    public static IDisposable MeasureTime(string scopeName, LogLevel level = LogLevel.Debug)
    {
        return new MeasureScope(scopeName, level);
    }

    [Conditional("DEBUG")]
    public static void Clear()
    {
        lock (LockObj)
        {
            try
            {
                if (File.Exists(LogPath)) File.Delete(LogPath);
            }
            catch { }
        }
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, long> _accumulatedTimes = new();

    [Conditional("DEBUG")]
    public static void AddInterval(string key, long elapsedMilliseconds)
    {
        _accumulatedTimes.AddOrUpdate(key, elapsedMilliseconds, (_, current) => current + elapsedMilliseconds);
    }

    [Conditional("DEBUG")]
    public static void PrintAccumulated(string prefixMessage, LogLevel level = LogLevel.Debug)
    {
        if (level < ActiveLogLevel) return;
        if (_accumulatedTimes.IsEmpty) return;
        var parts = _accumulatedTimes.Select(kv => $"{kv.Key}: {kv.Value} ms").ToArray();
        WriteLine($"{prefixMessage} [{string.Join(", ", parts)}]", level);
    }

    [Conditional("DEBUG")]
    public static void PrintAccumulatedGrouped(string title, LogLevel level = LogLevel.Debug)
    {
        if (level < ActiveLogLevel) return;
        if (_accumulatedTimes.IsEmpty) return;

        var grouped = _accumulatedTimes
            .Select(kv =>
            {
                var parts = kv.Key.Split('|');
                return new
                {
                    Group = parts.Length > 1 ? parts[0] : "Global",
                    Metric = parts.Length > 1 ? parts[1] : parts[0],
                    kv.Value
                };
            })
            .GroupBy(x => x.Group)
            .OrderByDescending(g => g.Sum(x => x.Value));

        var sb = new StringBuilder();
        sb.AppendLine($"=== {title} ===");
        foreach (var g in grouped)
        {
            var metricsStr = string.Join(", ", g.Select(x => $"{x.Metric}: {x.Value} ms"));
            sb.AppendLine($"  [{g.Key}] {metricsStr}");
        }
        WriteLine(sb.ToString(), level);
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

    /// <summary>
    /// usingスコープを用いて処理時間を計測し、自動的にログ出力するクラス。
    /// </summary>
    private class MeasureScope : IDisposable
    {
        private readonly string _scopeName;
        private readonly LogLevel _level;
        private readonly Stopwatch _sw;

        public MeasureScope(string scopeName, LogLevel level)
        {
            _scopeName = scopeName;
            _level = level;
            _sw = Stopwatch.StartNew();
            WriteLine($"[Start Scope] {_scopeName}", _level);
        }

        public void Dispose()
        {
            _sw.Stop();
            WriteLine($"[End Scope] {_scopeName} took {_sw.ElapsedMilliseconds} ms", _level);
        }
    }
}
