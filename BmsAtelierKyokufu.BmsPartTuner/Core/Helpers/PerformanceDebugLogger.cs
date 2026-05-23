namespace BmsAtelierKyokufu.BmsPartTuner.Core.Helpers
{
    public static class PerformanceDebugLogger
    {
        private static readonly Lock LockObj = new();
        private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "perf_measure.log");

        [Conditional("DEBUG")]
        public static void WriteLine(string? message)
        {
            lock (LockObj)
            {
                var logMessage = $"[{DateTime.Now:HH:mm:ss.fff}] {message ?? string.Empty}";
                Debug.WriteLine(logMessage);
                try
                {
                    File.AppendAllText(LogPath, logMessage + Environment.NewLine);
                }
                catch { }
            }
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
        public static void PrintAccumulated(string prefixMessage)
        {
            if (_accumulatedTimes.IsEmpty) return;
            var parts = _accumulatedTimes.Select(kv => $"{kv.Key}: {kv.Value} ms").ToArray();
            WriteLine($"{prefixMessage} [{string.Join(", ", parts)}]");
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
    }
}
