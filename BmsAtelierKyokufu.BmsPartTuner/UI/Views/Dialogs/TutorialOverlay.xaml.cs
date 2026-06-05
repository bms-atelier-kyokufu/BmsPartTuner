namespace BmsAtelierKyokufu.BmsPartTuner.UI.Views.Dialogs;

[ExcludeFromCodeCoverage]
public partial class TutorialOverlay : UserControl
{
    private ThemeService? _themeService;

    public TutorialOverlay()
    {
        InitializeComponent();
        Loaded += TutorialOverlay_Loaded;
        Unloaded += TutorialOverlay_Unloaded;
    }

    private void TutorialOverlay_Loaded(object sender, RoutedEventArgs e)
    {
        if (Application.Current is App app && app.ThemeService != null)
        {
            _themeService = app.ThemeService;
            _themeService.ThemeChanged += OnThemeChanged;
            UpdateLogo(_themeService.IsDarkTheme);
        }
    }

    private void TutorialOverlay_Unloaded(object sender, RoutedEventArgs e)
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