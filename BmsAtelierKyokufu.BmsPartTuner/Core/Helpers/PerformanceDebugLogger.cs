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
            if (message == null) return;

            var timestamp = $"[{DateTime.Now:HH:mm:ss.fff}] [{level.ToString().ToUpper()}] ";
            var indent = new string(' ', timestamp.Length);

            var lines = message.Split(["\r\n", "\r", "\n"], StringSplitOptions.None);
            var sb = new StringBuilder();
            for (int i = 0; i < lines.Length; i++)
            {
                if (i == 0)
                {
                    sb.Append(timestamp).Append(lines[i]);
                }
                else
                {
                    sb.Append(Environment.NewLine).Append(indent).Append(lines[i]);
                }
            }

            var logMessage = sb.ToString();
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

    [Conditional("DEBUG")]
    public static void LogMemoryUsage(string? context = null)
    {
        if (LogLevel.Debug < ActiveLogLevel) return;
        var process = System.Diagnostics.Process.GetCurrentProcess();
        long mem = process.WorkingSet64 / (1024 * 1024);
        long privateMem = process.PrivateMemorySize64 / (1024 * 1024);
        long gcMem = GC.GetTotalMemory(false) / (1024 * 1024);
        WriteLine($"[Memory] {context ?? "Usage"}: WorkingSet={mem}MB, Private={privateMem}MB, GC={gcMem}MB", LogLevel.Debug);
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

    // --- メモリ診断用機能 ---
    private static DateTime? _diagnosisStartTime = null;

    [Conditional("DEBUG")]
    public static void StartMemoryDiagnosis()
    {
        _diagnosisStartTime = DateTime.UtcNow;
        WriteLine("=== Memory Diagnosis Started ===", LogLevel.Info);
    }

    [Conditional("DEBUG")]
    public static void CheckAndHaltIfDiagnosisTriggered(string context, object? targetObject = null)
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
            sb.AppendLine("      MEMORY DIAGNOSIS HALT REPORT          ");
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

                if (targetObject is System.Collections.Concurrent.ConcurrentDictionary<string, Models.CachedSoundData> cacheMap)
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
            WriteLine(report, LogLevel.Trace);
            WriteLine("Memory diagnosis halt triggered: 5 seconds elapsed.", LogLevel.Trace);

            //Thread.Sleep(100);
            //Environment.FailFast("Memory diagnosis halt triggered: 5 seconds elapsed.");
        }
    }
}
