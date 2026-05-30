using BmsAtelierKyokufu.BmsPartTuner.Extensions;
using BmsAtelierKyokufu.BmsPartTuner.UI.Services;
using BmsAtelierKyokufu.BmsPartTuner.UI.Views.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Win32;

namespace BmsAtelierKyokufu.BmsPartTuner
{
    public partial class App : Application
    {
    private static readonly Logger<App> s_logger = new();
        private readonly IHost _host;
        private ThemeService? _themeService;
        private IUpdateService? _updateService;

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
                .ConfigureServices(static (_, services) => services.ConfigureAppServices())
                .Build();
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            CrashReportingService.LogUnhandledException(e.Exception, "UIスレッド");
            e.Handled = true;
            Shutdown();
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                CrashReportingService.LogUnhandledException(ex, "バックグラウンドスレッド");
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
            _updateService = _host.Services.GetRequiredService<IUpdateService>();
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
            if (_updateService?.IsUpdateReady is true)
            {
                _updateService.LaunchUpdateInstaller();
            }

            try
            {
                await _host.StopAsync();
            }
            catch (Exception ex)
            {
                s_logger.WriteDebug($"ホストの停止中にエラーが発生しました: {ex}");
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
        private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category == Microsoft.Win32.UserPreferenceCategory.General)
            {
                // SettingsServiceから設定を読み込んでシステムテーマ追従が有効か確認
                var settingsService = _host.Services.GetService<SettingsService>();
                var settings = settingsService?.Load();

                if (settings?.UseSystemTheme is true && _themeService != null)
                {
                    Dispatcher.Invoke(() => _themeService.ApplySystemTheme());
                }
            }
        }
    }
}

