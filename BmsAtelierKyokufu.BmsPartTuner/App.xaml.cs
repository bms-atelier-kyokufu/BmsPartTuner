using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using BmsAtelierKyokufu.BmsPartTuner.Core;
using BmsAtelierKyokufu.BmsPartTuner.Services;
using BmsAtelierKyokufu.BmsPartTuner.Services.AudioPlayer;
using BmsAtelierKyokufu.BmsPartTuner.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Win32;

namespace BmsAtelierKyokufu.BmsPartTuner
{
    public partial class App : Application
    {
        private readonly IHost _host;
        private ThemeService? _themeService;
        private UpdateService? _updateService;

        /// <summary>
        /// テーマサービスを取得します。DIコンテナからの安全なアクセスを提供します。
        /// </summary>
        public ThemeService? ThemeService => _themeService;

        public App()
        {
            // グローバル例外ハンドラーの設定
            DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            // InitializeComponent()を呼び出してResourceDictionaryを初期化
            // LightTheme.xamlがデフォルトとしてApp.xamlに静的にマージされています
            InitializeComponent();

            _host = Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    // Settings Services
                    services.AddSingleton<SettingsService>();
                    services.AddSingleton<ThemeService>();
                    services.AddSingleton<LicenseLoaderService>();
                    services.AddSingleton<UpdateService>();

                    // Core Services (Phase 5: ISP 適用)
                    services.AddSingleton<IInputValidationService, InputValidationService>();
                    services.AddSingleton<IBmsOptimizationService, BmsOptimizationService>();
                    services.AddSingleton<IAudioPlayerFactory, NAudioPlayerFactory>();
                    services.AddSingleton<IUIThreadDispatcher>(provider =>
                        new WpfUIThreadDispatcher(Current.Dispatcher));
                    services.AddSingleton(provider =>
                        new AudioPreviewService(
                            provider.GetRequiredService<IUIThreadDispatcher>(),
                            provider.GetRequiredService<IAudioPlayerFactory>()));
                    services.AddSingleton<InstrumentNameDetectionService>();

                    // UI Services (Initializeパターン)
                    services.AddSingleton<IUiElementService<ToastViewModel>, ToastNotificationService>();
                    services.AddSingleton<IUiElementService<ResultCardData>, ResultCardService>();
                    services.AddSingleton<IDragDropService>(provider =>
                        new DragDropService(AppConstants.Files.SupportedBmsExtensions));
                    services.AddSingleton<FileListFilterService>();

                    // ViewModels
                    services.AddTransient<MainViewModel>();

                    // Windows
                    services.AddTransient<MainWindow>();
                })
                .Build();
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            LogUnhandledException(e.Exception, "UIスレッド");
            e.Handled = true;
            Shutdown();
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                LogUnhandledException(ex, "バックグラウンドスレッド");
            }
        }

        /// <summary>
        /// 未処理例外をログファイルに記録し、ユーザーに通知します。
        /// </summary>
        /// <param name="ex">発生した例外。</param>
        /// <param name="source">例外の発生元（UIスレッド/バックグラウンドスレッド）。</param>
        /// <remarks>
        /// <para>【Why ログ保存】</para>
        /// リリース後の予期せぬクラッシュ時に、原因究明に必要な情報を確実に残すため。
        /// </remarks>
        private void LogUnhandledException(Exception ex, string source)
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

        protected override async void OnStartup(StartupEventArgs e)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            await _host.StartAsync();

            // ThemeServiceを取得してシステムテーマ変更の監視を設定
            _themeService = _host.Services.GetRequiredService<ThemeService>();
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;

            // UpdateServiceを取得してバックグラウンドで更新チェック
            _updateService = _host.Services.GetRequiredService<UpdateService>();
            _ = Task.Run(async () => await _updateService.CheckForUpdatesAsync());

            // DIコンテナからMainWindowを取り出す（依存関係は全て解決済み）
            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            mainWindow.Show();

            // MainViewModelが起動時にテーマを適用するので、ここでは何もしない
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            // システムテーマ変更の監視を停止
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;

            // アップデートの準備ができていればインストーラーを起動
            if (_updateService?.IsUpdateReady == true)
            {
                _updateService.LaunchUpdateInstaller();
            }

            try
            {
                await _host.StopAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ホストの停止中にエラーが発生しました: {ex}");
            }
            finally
            {
                _updateService?.Dispose();
                _host.Dispose();
                base.OnExit(e);
            }
        }

        /// <summary>
        /// システム設定の変更を検知
        /// </summary>
        private void OnUserPreferenceChanged(object sender, Microsoft.Win32.UserPreferenceChangedEventArgs e)
        {
            if (e.Category == Microsoft.Win32.UserPreferenceCategory.General)
            {
                // SettingsServiceから設定を読み込んでシステムテーマ追従が有効か確認
                var settingsService = _host.Services.GetService<SettingsService>();
                var settings = settingsService?.Load();

                if (settings?.UseSystemTheme == true && _themeService != null)
                {
                    Dispatcher.Invoke(() => _themeService.ApplySystemTheme());
                }
            }
        }
    }
}
