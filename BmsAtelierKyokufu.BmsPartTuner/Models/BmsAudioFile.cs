namespace BmsAtelierKyokufu.BmsPartTuner.Models;

/// <summary>
/// BMSファイル内で定義された1つのオーディオファイル（#WAV定義など）を表すモデル。
/// </summary>
/// <remarks>
/// <para>【データ構造】</para>
/// <list type="bullet">
/// <item>Num: ZZ進数表記（例: "01", "0Z", "ZZ"）</item>
/// <item>NumInteger: 10進数表記（例: 1, 35, 1295）</item>
/// <item>Name: ファイルフルパス</item>
/// <item>FileSize: バイト単位のサイズ</item>
/// <item>InstrumentName: 推定された楽器種別（例: "kick", "snare"）</item>
/// </list>
/// </remarks>
public class BmsAudioFile
{
    /// <summary>定義番号（ZZ進数、例: "01", "0Z"）。</summary>
    public string Num { get; set; } = string.Empty;

    /// <summary>定義番号（10進数、例: 1, 35）。</summary>
    public int NumInteger { get; set; }

    /// <summary>ファイルフルパス。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>ファイルサイズ（バイト）。</summary>
    public long FileSize { get; set; }



    /// <summary>オーディオフィンガープリント</summary>
    public string AudioFingerprint { get; set; } = string.Empty;

    /// <summary>
    /// 推定された楽器名（例: "kick", "snare", "hihat"）。
    /// 空文字列の場合は推定できなかったことを示します。
    /// </summary>
    public string InstrumentName { get; set; } = string.Empty;

}
