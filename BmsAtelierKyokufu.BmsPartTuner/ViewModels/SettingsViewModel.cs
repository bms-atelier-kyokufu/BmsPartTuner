using System.Collections.ObjectModel;
using System.Reflection;
using BmsAtelierKyokufu.BmsPartTuner.Models;
using BmsAtelierKyokufu.BmsPartTuner.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace BmsAtelierKyokufu.BmsPartTuner.ViewModels;

/// <summary>
/// 設定画面のViewModel。
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    public event EventHandler<bool>? ThemeStateChanged;

    private readonly SettingsService _settingsService;
    private readonly ThemeService _themeService;
    private readonly LicenseLoaderService _licenseLoaderService;
    private readonly AppSettings _settings;

    /// <summary>
    /// 設定画面のタブインデックス。
    /// 0: 全般, 1: 情報
    /// </summary>
    [ObservableProperty]
    private int _selectedTabIndex;

    /// <summary>
    /// mBMplayの実行ファイルパス。
    /// </summary>
    public string MbmPlayPath
    {
        get => _settings.MbmPlayPath;
        set
        {
            if (_settings.MbmPlayPath != value)
            {
                _settings.MbmPlayPath = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasPlayerPath));
                _settingsService.Save(_settings);
            }
        }
    }

    /// <summary>
    /// プレイヤーパスが設定されているかどうか。
    /// </summary>
    public bool HasPlayerPath => !string.IsNullOrWhiteSpace(MbmPlayPath) && File.Exists(MbmPlayPath);

    /// <summary>
    /// ダークテーマを使用するかどうか。
    /// </summary>
    public bool IsDarkTheme
    {
        get => _settings.IsDarkTheme;
        set
        {
            if (_settings.IsDarkTheme != value)
            {
                _settings.IsDarkTheme = value;
                OnPropertyChanged();
                _settingsService.Save(_settings);

                // UseSystemThemeがfalseの場合のみテーマを適用
                if (!UseSystemTheme)
                {
                    _themeService.ApplyTheme(value);
                    ThemeStateChanged?.Invoke(this, value);
                }
            }
        }
    }

    /// <summary>
    /// システムテーマに追従するかどうか。
    /// </summary>
    public bool UseSystemTheme
    {
        get => _settings.UseSystemTheme;
        set
        {
            if (_settings.UseSystemTheme != value)
            {
                _settings.UseSystemTheme = value;
                OnPropertyChanged();
                _settingsService.Save(_settings);

                if (value)
                {
                    _themeService.ApplySystemTheme();
                    ThemeStateChanged?.Invoke(this, _themeService.IsDarkTheme);
                }
                else
                {
                    _themeService.ApplyTheme(IsDarkTheme);
                    ThemeStateChanged?.Invoke(this, IsDarkTheme);
                }
            }
        }
    }

    /// <summary>
    /// アプリケーションのバージョン。
    /// </summary>
    public string AppVersion
    {
        get
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            Version? version = assembly.GetName().Version;
            return version != null ? $"v{version.Major}.{version.Minor}.{version.Build}" : "v0.0.0";
        }
    }

    /// <summary>
    /// アプリケーション名。
    /// </summary>
    public string AppName
    {
        get
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            AssemblyTitleAttribute? titleAttr = assembly.GetCustomAttribute<AssemblyTitleAttribute>();
            return titleAttr?.Title ?? "BMS Part Tuner";
        }
    }

    /// <summary>
    /// 作者情報。
    /// </summary>
    public string AuthorInfo
    {
        get
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            AssemblyCompanyAttribute? companyAttr = assembly.GetCustomAttribute<AssemblyCompanyAttribute>();
            return companyAttr?.Company ?? "BMSアトリエ【極譜】(おちあP & L-Mys)";
        }
    }

    /// <summary>
    /// GitHubリポジトリURL。
    /// </summary>
    public string GitHubUrl
    {
        get
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            AssemblyDescriptionAttribute? descriptionAttr = assembly.GetCustomAttribute<AssemblyDescriptionAttribute>();
            return descriptionAttr?.Description ?? "https://github.com/bms-atelier-kyokufu/BmsPartTuner";
        }
    }

    /// <summary>
    /// ライセンス情報のコレクション。
    /// </summary>
    public ObservableCollection<LicenseInfo> Licenses { get; } = new();

    /// <summary>
    /// 選択されたライセンス。
    /// </summary>
    [ObservableProperty]
    private LicenseInfo? _selectedLicense;

    /// <summary>
    /// ライセンス詳細表示状態。
    /// </summary>
    [ObservableProperty]
    private bool _isLicenseDetailVisible;

    public SettingsViewModel(SettingsService settingsService, ThemeService themeService, LicenseLoaderService licenseLoaderService)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
        _licenseLoaderService = licenseLoaderService ?? throw new ArgumentNullException(nameof(licenseLoaderService));
        _settings = _settingsService.Load();

        // テーマサービスからの変更通知を購読
        _themeService.ThemeChanged += (s, isDark) =>
        {
            if (_settings.IsDarkTheme != isDark)
            {
                _settings.IsDarkTheme = isDark;
                OnPropertyChanged(nameof(IsDarkTheme));
            }
            ThemeStateChanged?.Invoke(this, isDark);
        };

        LoadLicenses();
    }

    private void LoadLicenses()
    {
        IEnumerable<LicenseInfo> licenses = _licenseLoaderService.LoadLicenses();
        Licenses.Clear();
        foreach (LicenseInfo license in licenses)
        {
            Licenses.Add(license);
        }

        SelectedLicense = null;
    }

    /// <summary>
    /// プレイヤーの実行ファイルを選択するコマンド。
    /// </summary>
    [RelayCommand]
    private void SelectPlayerPath()
    {
        OpenFileDialog dialog = new OpenFileDialog
        {
            Title = "mBMplay.exeを選択してください",
            Filter = "実行ファイル (*.exe)|*.exe|すべてのファイル (*.*)|*.*",
            CheckFileExists = true
        };

        if (!string.IsNullOrWhiteSpace(MbmPlayPath) && File.Exists(MbmPlayPath))
        {
            dialog.InitialDirectory = Path.GetDirectoryName(MbmPlayPath);
        }

        if (dialog.ShowDialog() == true)
        {
            MbmPlayPath = dialog.FileName;
        }
    }

    /// <summary>
    /// プレイヤーパスをクリアするコマンド。
    /// </summary>
    [RelayCommand]
    private void ClearPlayerPath()
    {
        MbmPlayPath = string.Empty;
    }

    /// <summary>
    /// GitHubリンクを開くコマンド。
    /// </summary>
    [RelayCommand]
    private void OpenGitHub()
    {
        OpenUrl(GitHubUrl);
    }

    /// <summary>
    /// GitHub Issues を開くコマンド。
    /// </summary>
    [RelayCommand]
    private void OpenGitHubIssues()
    {
        OpenUrl("https://github.com/bms-atelier-kyokufu/BmsPartTuner/issues");
    }

    /// <summary>
    /// Twitter (X) を開くコマンド。
    /// </summary>
    [RelayCommand]
    private void OpenTwitter()
    {
        OpenUrl("https://x.com/rian_eimu");
    }

    /// <summary>
    /// 指定されたURLをデフォルトブラウザで開きます。
    /// </summary>
    /// <param name="url">開くURL。</param>
    private static void OpenUrl(string url)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(psi);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"URLを開けませんでした: {ex.Message}");
        }
    }

    /// <summary>
    /// 初期テーマを適用します(アプリケーション起動時に呼び出し)。
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
