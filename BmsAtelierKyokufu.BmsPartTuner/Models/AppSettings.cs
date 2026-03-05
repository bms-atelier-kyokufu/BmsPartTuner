using System.Text.Json.Serialization;

namespace BmsAtelierKyokufu.BmsPartTuner.Models;

/// <summary>
/// アプリケーション設定のデータモデル。
/// 実行ファイルと同じ場所のsetting.jsonに永続化されます。
/// </summary>
public class AppSettings
{
    /// <summary>
    /// 外部プレイヤー(mBMplay)の実行ファイルパス。
    /// </summary>
    [JsonPropertyName("mbmPlayPath")]
    public string MbmPlayPath { get; set; } = string.Empty;

    /// <summary>
    /// ダークテーマを使用するかどうか。
    /// </summary>
    [JsonPropertyName("isDarkTheme")]
    public bool IsDarkTheme { get; set; } = false;

    /// <summary>
    /// システムテーマに追従するかどうか。
    /// trueの場合、IsDarkThemeは無視されます。
    /// </summary>
    [JsonPropertyName("useSystemTheme")]
    public bool UseSystemTheme { get; set; } = true;

    /// <summary>
    /// 外部プレイヤーの追加引数（将来の拡張用）。
    /// </summary>
    [JsonPropertyName("playerArguments")]
    public PlayerArguments PlayerArguments { get; set; } = new();
}

/// <summary>
/// 外部プレイヤーの追加引数設定。
/// </summary>
public class PlayerArguments
{
    /// <summary>
    /// 最初から再生する（iBMSCモード）。
    /// </summary>
    [JsonPropertyName("playFromStart")]
    public bool PlayFromStart { get; set; } = true;

    /// <summary>
    /// その他のカスタム引数。
    /// </summary>
    [JsonPropertyName("customArgs")]
    public string CustomArgs { get; set; } = string.Empty;
}
