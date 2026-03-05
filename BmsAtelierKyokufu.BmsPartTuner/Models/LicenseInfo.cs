namespace BmsAtelierKyokufu.BmsPartTuner.Models;

/// <summary>
/// ライセンス情報を保持するモデルクラス。
/// </summary>
public class LicenseInfo
{
    /// <summary>
    /// ライブラリ名（ファイル名から拡張子を除いたもの）。
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// ライセンス本文（Markdown形式）。
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// アプリケーション自身のライセンスかどうか。
    /// </summary>
    public bool IsAppLicense { get; set; }
}
