namespace BmsAtelierKyokufu.BmsPartTuner.Models;

/// <summary>
/// BMSファイル内で定義された単一のオーディオファイル情報を表します。
/// </summary>
public record BmsAudioFile
{
    /// <summary>
    /// 定義番号（ZZ進数表記）。
    /// </summary>
    public string Num { get; init; } = string.Empty;

    /// <summary>
    /// 定義番号（10進数表記）。
    /// </summary>
    public int NumInteger { get; init; }

    /// <summary>
    /// ファイルのフルパス。
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// ファイルサイズ（バイト）。
    /// </summary>
    public long FileSize { get; init; }

    /// <summary>
    /// オーディオフィンガープリント。
    /// </summary>
    public string AudioFingerprint { get; init; } = string.Empty;

    /// <summary>
    /// 推定された楽器名。推定できなかった場合は空文字列。
    /// </summary>
    public string InstrumentName { get; init; } = string.Empty;
}
