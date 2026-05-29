using BmsAtelierKyokufu.BmsPartTuner.UI.Services;

namespace BmsAtelierKyokufu.BmsPartTuner.UI.Views.Dialogs.Settings
{
    /// <summary>
    /// AboutTab.xaml の相互作用ロジックを提供します。
    /// ThemeServiceへのアクセスが必要な場合は、リフレクションを使用せず Application.Current 経由で取得してください。
    /// <code>
    /// if (Application.Current is App app &amp;&amp; app.ThemeService != null)
    /// {
    ///     var themeService = app.ThemeService;
    ///     // themeServiceを使用...
    /// }
    /// </code>
    /// </summary>
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public partial class AboutTab : UserControl
    {
        // NOTE: This field is part of the example implementation for PR #118.
        // When the SVG logo and theme switching logic is added, this field will be used
        // to store the ThemeService reference and subscribe to ThemeChanged events.
        private ThemeService? _themeService;

        public AboutTab()
        {
            InitializeComponent();
            Loaded += AboutTab_Loaded;
            Unloaded += AboutTab_Unloaded;
        }

        private void AboutTab_Loaded(object sender, RoutedEventArgs e)
        {
            if (Application.Current is App app && app.ThemeService != null)
            {
                _themeService = app.ThemeService;
                _themeService.ThemeChanged += OnThemeChanged;
                UpdateLogo(_themeService.IsDarkTheme);
            }
        }

        private void AboutTab_Unloaded(object sender, RoutedEventArgs e)
        {
            _themeService?.ThemeChanged -= OnThemeChanged;
        }

        private void OnThemeChanged(object? sender, bool isDark)
        {
            UpdateLogo(isDark);
        }

        private void UpdateLogo(bool isDark)
        {
            var logoName = isDark ? "BmpPartTunerLogo_dark.svg" : "BmpPartTunerLogo_light.svg";
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Properties", "Resources", logoName);
            if (File.Exists(path))
            {
                LogoViewbox.Source = new Uri(path, UriKind.Absolute);
            }
            else
            {
                // Fallback pack URI in case it's embedded as Resource instead of Content
                string packUri = $"pack://application:,,,/BmsPartTuner;component/Properties/Resources/{logoName}";
                try
                {
                    LogoViewbox.Source = new Uri(packUri, UriKind.Absolute);
                }
                catch { }
            }
        }
    }
}
