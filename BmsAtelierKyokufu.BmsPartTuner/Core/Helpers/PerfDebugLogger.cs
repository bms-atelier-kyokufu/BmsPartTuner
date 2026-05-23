namespace BmsAtelierKyokufu.BmsPartTuner.Core.Helpers
{
    public static class PerfDebugLogger
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
    }
}
