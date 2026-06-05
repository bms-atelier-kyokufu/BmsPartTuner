using System.Text.Json;

namespace BmsAtelierKyokufu.BmsPartTuner.Infrastructure.Common;

/// <summary>
/// アプリケーション設定の読み書きを行うサービス。
/// 設定ファイルは実行ファイルと同じ場所のsetting.jsonに保存されます。
/// </summary>
public class SettingsService
{
    private static readonly Logger<SettingsService> s_logger = new();
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

                // 既存のユーザー (setting.json が存在する) の場合、チュートリアルは既読とする
                // ただし明示的に false が保存されている場合はそれに従うため、
                // JSONに "hasSeenTutorial" キーが存在しない場合のみ true とする。
                if (!json.Contains("\"hasSeenTutorial\""))
                {
                    _cachedSettings = _cachedSettings with { HasSeenTutorial = true };
                }
            }
            else
            {
                _cachedSettings = new AppSettings();
            }
        }
        catch (Exception ex)
        {
            s_logger.WriteDebug($"設定ファイルの読み込みに失敗しました: {ex.Message}");
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
            string tempPath = _settingsFilePath + ".tmp";

            // 一時ファイルに書き込んでからアトミックにリネーム（置換）する
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, _settingsFilePath, overwrite: true);

            _cachedSettings = settings;
        }
        catch (Exception ex)
        {
            s_logger.WriteDebug($"設定ファイルの保存に失敗しました: {ex.Message}");
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

