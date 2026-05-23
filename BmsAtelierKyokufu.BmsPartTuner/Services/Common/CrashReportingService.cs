using System.Reflection;

namespace BmsAtelierKyokufu.BmsPartTuner.Services.Common
{
    /// <summary>
    /// 未処理例外の記録とユーザーへの通知を担当するサービス。
    /// </summary>
    public static class CrashReportingService
    {
        /// <summary>
        /// 未処理例外をログファイルに記録し、ユーザーに通知します。
        /// </summary>
        /// <param name="ex">発生した例外。</param>
        /// <param name="source">例外の発生元（UIスレッド/バックグラウンドスレッドなど）。</param>
        public static void LogUnhandledException(Exception ex, string source)
        {
            string? logPath = null;
            try
            {
                // ログディレクトリを作成
                var logDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "BmsPartTuner",
                    "Logs");
                Directory.CreateDirectory(logDir);

                // ログファイル名を生成
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                logPath = Path.Combine(logDir, $"crash_{timestamp}.log");

                // ログ内容を構築
                var sb = new StringBuilder();
                sb.AppendLine("=== BMS Part Tuner Crash Report ===");
                sb.AppendLine($"Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"Source: {source}");
                sb.AppendLine();

                // アプリバージョン
                var version = Assembly.GetExecutingAssembly().GetName().Version;
                sb.AppendLine($"App Version: {version}");

                // OSバージョン
                sb.AppendLine($"OS Version: {Environment.OSVersion}");
                sb.AppendLine($".NET Version: {Environment.Version}");
                sb.AppendLine();

                // 例外情報を再帰的に記録
                AppendExceptionDetails(sb, ex, 0);

                // ファイルに保存
                File.WriteAllText(logPath, sb.ToString(), Encoding.UTF8);
            }
            catch
            {
                // ログ保存に失敗しても処理を続行
            }

            // ユーザーへの通知
            var message = "予期せぬエラーが発生しました。";
            if (logPath != null && File.Exists(logPath))
            {
                message += $"\n\nエラーログを保存しました:\n{logPath}";
            }
            message += $"\n\n詳細: {ex.Message}";

            MessageBox.Show(message, "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        /// <summary>
        /// 例外の詳細を再帰的に追加します。
        /// </summary>
        private static void AppendExceptionDetails(StringBuilder sb, Exception ex, int depth)
        {
            var indent = new string(' ', depth * 2);

            if (depth > 0)
            {
                sb.AppendLine($"{indent}--- Inner Exception (Level {depth}) ---");
            }

            sb.AppendLine($"{indent}Type: {ex.GetType().FullName}");
            sb.AppendLine($"{indent}Message: {ex.Message}");
            sb.AppendLine($"{indent}StackTrace:");
            sb.AppendLine(ex.StackTrace);
            sb.AppendLine();

            if (ex.InnerException != null && depth < 5)
            {
                AppendExceptionDetails(sb, ex.InnerException, depth + 1);
            }
        }
    }
}
