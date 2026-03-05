using System.Text.Json;
using BmsAtelierKyokufu.BmsPartTuner.Models;

namespace BmsAtelierKyokufu.BmsPartTuner.Services;

/// <summary>
/// アプリケーション設定の読み書きを行うサービス。
/// 設定ファイルは実行ファイルと同じ場所のsetting.jsonに保存されます。
/// </summary>
public class SettingsService
{
    private readonly string _settingsFilePath;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private AppSettings? _cachedSettings;

    public SettingsService() : this(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "setting.json"))
    {
    }

    internal SettingsService(string filePath)
    {
        _settingsFilePath = filePath;
    }

    /// <summary>
    /// 設定を読み込みます。
    /// ファイルが存在しない場合はデフォルト設定を返します。
    /// </summary>
    public AppSettings Load()
    {
        if (_cachedSettings != null)
        {
            return _cachedSettings;
        }

        try
        {
            if (File.Exists(_settingsFilePath))
            {
                var json = File.ReadAllText(_settingsFilePath);
                _cachedSettings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
            else
            {
                _cachedSettings = new AppSettings();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"設定ファイルの読み込みに失敗しました: {ex.Message}");
            _cachedSettings = new AppSettings();
        }

        return _cachedSettings;
    }

    /// <summary>
    /// 設定を保存します。
    /// </summary>
    public void Save(AppSettings settings)
    {
        try
        {
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(_settingsFilePath, json);
            _cachedSettings = settings;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"設定ファイルの保存に失敗しました: {ex.Message}");
        }
    }

    /// <summary>
    /// キャッシュを無効化して次回Load時にファイルから再読み込みします。
    /// </summary>
    public void InvalidateCache()
    {
        _cachedSettings = null;
    }
}
