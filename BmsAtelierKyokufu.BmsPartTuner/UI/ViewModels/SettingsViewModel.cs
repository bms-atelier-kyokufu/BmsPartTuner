using System.Reflection;
using BmsAtelierKyokufu.BmsPartTuner.UI.Services;
using Microsoft.Win32;

namespace BmsAtelierKyokufu.BmsPartTuner.UI.ViewModels;

/// <summary>
/// アプリケーションの設定画面の状態と操作を管理するViewModel。
/// テーマ設定、プレイヤーパス設定、ライセンス情報などを提供します。
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private static readonly IPerformanceLogger s_logger = new TypedLogger(typeof(SettingsViewModel));

    private readonly SettingsService _settingsService;
    private readonly ThemeService _themeService;
    private readonly LicenseLoaderService _licenseLoaderService;
    private AppSettings _settings;

    /// <summary>
    /// 設定画面で現在選択されているタブのインデックス。
    /// 0: 全般設定, 1: アプリ情報・ライセンス
    /// </summary>
    [ObservableProperty]
    public partial int SelectedTabIndex { get; set; }

    /// <summary>
    /// mBMplayなど、テスト再生に使用する外部プレイヤーの実行ファイルパス。
    /// </summary>
    public string MbmPlayPath
    {
        get => _settings.MbmPlayPath;
        set
        {
            var cleanValue = value?.Trim('"') ?? string.Empty;
            if (_settings.MbmPlayPath != cleanValue)
            {
                _settings = _settings with { MbmPlayPath = cleanValue };
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasPlayerPath));
                _settingsService.Save(_settings);
            }
        }
    }

    /// <summary>
    /// プレイヤーの実行ファイルパスが設定され、かつファイルが存在するかどうか。
    /// </summary>
    public bool HasPlayerPath => !string.IsNullOrWhiteSpace(MbmPlayPath) && File.Exists(MbmPlayPath);

    /// <summary>
    /// UIのダークテーマが有効化されているかどうか。
    /// </summary>
    public bool IsDarkTheme
    {
        get => _settings.IsDarkTheme;
        set
        {
            if (_settings.IsDarkTheme != value)
            {
                _settings = _settings with { IsDarkTheme = value };
                OnPropertyChanged();
                _settingsService.Save(_settings);

                if (!UseSystemTheme)
                {
                    _themeService.ApplyTheme(value);
                }
            }
        }
    }

    /// <summary>
    /// OSのシステムテーマ（ダーク/ライト）に自動的に追従するかどうか。
    /// </summary>
    public bool UseSystemTheme
    {
        get => _settings.UseSystemTheme;
        set
        {
            if (_settings.UseSystemTheme != value)
            {
                _settings = _settings with { UseSystemTheme = value };
                OnPropertyChanged();
                _settingsService.Save(_settings);

                if (value)
                {
                    _themeService.ApplySystemTheme();
                }
                else
                {
                    _themeService.ApplyTheme(IsDarkTheme);
                }
            }
        }
    }

    /// <summary>
    /// アセンブリ情報から取得したアプリケーションのバージョン文字列。
    /// </summary>
    public static string AppVersion
    {
        get
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            Version? version = assembly.GetName().Version;
            return version != null ? $"v{version.Major}.{version.Minor}.{version.Build}" : "v0.0.0";
        }
    }

    /// <summary>
    /// アセンブリ情報から取得したアプリケーションの表示名。
    /// </summary>
    public static string AppName
    {
        get
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            AssemblyTitleAttribute? titleAttr = assembly.GetCustomAttribute<AssemblyTitleAttribute>();
            return titleAttr?.Title ?? "BMS Part Tuner";
        }
    }

    /// <summary>
    /// アセンブリ情報から取得した作者・組織情報。
    /// </summary>
    public static string AuthorInfo
    {
        get
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            AssemblyCompanyAttribute? companyAttr = assembly.GetCustomAttribute<AssemblyCompanyAttribute>();
            return companyAttr?.Company ?? "BMSアトリエ【極譜】(おちあP & L-Mys)";
        }
    }

    /// <summary>
    /// プロジェクトのGitHubリポジトリURL。
    /// </summary>
    public static string GitHubUrl
    {
        get
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            AssemblyDescriptionAttribute? descriptionAttr = assembly.GetCustomAttribute<AssemblyDescriptionAttribute>();
            return descriptionAttr?.Description ?? AppConstants.Files.GitHubRepositoryUrl;
        }
    }

    /// <summary>
    /// OSSライセンス情報のコレクション。
    /// </summary>
    public ObservableCollection<LicenseInfo> Licenses { get; } = [];

    /// <summary>
    /// 現在リストで選択されているOSSライセンス情報。
    /// </summary>
    [ObservableProperty]
    public partial LicenseInfo? SelectedLicense { get; set; }

    /// <summary>
    /// ライセンス詳細情報のオーバーレイ表示状態。
    /// </summary>
    [ObservableProperty]
    public partial bool IsLicenseDetailVisible { get; set; }

    public SettingsViewModel(SettingsService settingsService, ThemeService themeService, LicenseLoaderService licenseLoaderService)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
        _licenseLoaderService = licenseLoaderService ?? throw new ArgumentNullException(nameof(licenseLoaderService));
        _settings = _settingsService.Load();

        _themeService.ThemeChanged += (_, isDark) =>
        {
            if (_settings.IsDarkTheme != isDark)
            {
                _settings = _settings with { IsDarkTheme = isDark };
                OnPropertyChanged(nameof(IsDarkTheme));
            }
        };

        LoadLicenses();
    }

    private void LoadLicenses()
    {
        IEnumerable<LicenseInfo> licenses = LicenseLoaderService.LoadLicenses();
        Licenses.Clear();
        foreach (LicenseInfo license in licenses)
        {
            Licenses.Add(license);
        }

        SelectedLicense = null;
    }

    [RelayCommand]
    private void SelectPlayerPath()
    {
        OpenFileDialog dialog = new()
        {
            Title = "mBMplay.exeを選択してください",
            Filter = "実行ファイル (*.exe)|*.exe|すべてのファイル (*.*)|*.*",
            CheckFileExists = true
        };

        if (!string.IsNullOrWhiteSpace(MbmPlayPath) && File.Exists(MbmPlayPath))
        {
            dialog.InitialDirectory = Path.GetDirectoryName(MbmPlayPath);
        }

        if (dialog.ShowDialog() is true)
        {
            MbmPlayPath = dialog.FileName;
        }
    }

    [RelayCommand]
    private void ClearPlayerPath()
    {
        MbmPlayPath = string.Empty;
    }

    [RelayCommand]
    private static void OpenGitHub()
    {
        OpenUrl(GitHubUrl);
    }

    [RelayCommand]
    private static void OpenGitHubIssues()
    {
        OpenUrl($"{AppConstants.Files.GitHubRepositoryUrl}/issues");
    }

    [RelayCommand]
    private static void OpenTwitter()
    {
        OpenUrl("https://x.com/rian_eimu");
    }

    private static void OpenUrl(string url)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(psi);
        }
        catch (Exception ex)
        {
            s_logger.WriteDebug($"URLを開けませんでした: {ex.Message}");
        }
    }

    /// <summary>
    /// アプリケーション起動時に、設定に基づいた初期テーマをUIに適用します。
    /// </summary>
    public void ApplyInitialTheme()
    {
        if (UseSystemTheme)
        {
            _themeService.ApplySystemTheme();
        }
        else
        {
            _themeService.ApplyTheme(IsDarkTheme);
        }
    }

    [RelayCommand]
    private void OpenLicenseDetail(LicenseInfo license)
    {
        SelectedLicense = license;
        IsLicenseDetailVisible = true;
    }

    [RelayCommand]
    private void CloseLicenseDetail()
    {
        IsLicenseDetailVisible = false;
        SelectedLicense = null;
    }
}
