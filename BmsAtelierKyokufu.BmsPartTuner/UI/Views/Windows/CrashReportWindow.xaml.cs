namespace BmsAtelierKyokufu.BmsPartTuner.UI.Views.Windows
{
    /// <summary>
    /// CrashReportWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class CrashReportWindow : Window
    {
        private readonly string? _logPath;
        private readonly string _detailsText;

        public CrashReportWindow(Exception ex, string? logPath)
        {
            InitializeComponent();

            _logPath = logPath;

            // テキスト表示の初期化
            if (string.IsNullOrEmpty(_logPath))
            {
                LogPathTextBox.Text = "保存できませんでした（ログディレクトリ書き込みエラー）";
                OpenFolderButton.IsEnabled = false;
            }
            else
            {
                LogPathTextBox.Text = _logPath;
            }

            // 例外の詳細情報をフォーマットして表示
            _detailsText = FormatExceptionDetails(ex);
            DetailsTextBox.Text = _detailsText;

            // アプリロゴの読み込み
            UpdateLogo();
        }

        private void UpdateLogo()
        {
            try
            {
                LogoImage.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/BmsAtelierKyokufu.BmsPartTuner;component/Properties/Resources/icon.ico"));
            }
            catch
            {
                // ロゴ表示に失敗しても例外はスローせず、アプリケーションエラー情報の表示自体を最優先にする
            }
        }

        private static string FormatExceptionDetails(Exception ex)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[Exception Type] {ex.GetType().FullName}");
            sb.AppendLine($"[Message] {ex.Message}");
            sb.AppendLine();
            sb.AppendLine("[Stack Trace]");
            sb.AppendLine(ex.StackTrace);

            var inner = ex.InnerException;
            for (int level = 1; inner != null && level <= 5; level++)
            {
                sb.AppendLine();
                sb.AppendLine($"--- Inner Exception (Level {level}) ---");
                sb.AppendLine($"Type: {inner.GetType().FullName}");
                sb.AppendLine($"Message: {inner.Message}");
                sb.AppendLine("Stack Trace:");
                sb.AppendLine(inner.StackTrace);
                inner = inner.InnerException;
            }

            return sb.ToString();
        }

        private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_logPath) || !File.Exists(_logPath)) return;

            try
            {
                // ファイルを選択した状態でエクスプローラーを開く
                var startInfo = new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{_logPath}\"",
                    UseShellExecute = true
                };
                Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"フォルダを開くことができませんでした。\n詳細: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void CopyLogButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(_detailsText);

                // コピー完了のマイクロインタラクション
                CopyLogButton.Content = "✓ コピーしました！";
                CopyLogButton.IsEnabled = false;

                await Task.Delay(2000);

                CopyLogButton.Content = "📋 詳細をコピー";
                CopyLogButton.IsEnabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"クリップボードにコピーできませんでした。\n詳細: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
