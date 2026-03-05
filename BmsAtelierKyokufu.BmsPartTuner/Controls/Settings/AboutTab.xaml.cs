using System.Windows;
using System.Windows.Controls;
using BmsAtelierKyokufu.BmsPartTuner.Services;

namespace BmsAtelierKyokufu.BmsPartTuner.Controls.Settings
{
    /// <summary>
    /// AboutTab.xaml の相互作用ロジック
    /// </summary>
    /// <remarks>
    /// ThemeServiceへのアクセスが必要な場合は、以下のパターンを使用してください：
    /// <code>
    /// // リフレクションを使用しないでThemeServiceにアクセスする正しい方法:
    /// if (Application.Current is App app <![CDATA[&&]]> app.ThemeService != null)
    /// {
    ///     var themeService = app.ThemeService;
    ///     // themeServiceを使用...
    /// }
    /// </code>
    /// </remarks>
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
            if (_themeService != null)
            {
                _themeService.ThemeChanged -= OnThemeChanged;
            }
        }

        private void OnThemeChanged(object? sender, bool isDark)
        {
            UpdateLogo(isDark);
        }

        private void UpdateLogo(bool isDark)
        {
            var logoName = isDark ? "BmpPartTunerLogo_dark.svg" : "BmpPartTunerLogo_light.svg";
            string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Properties", "Resources", logoName);
            if (System.IO.File.Exists(path))
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
