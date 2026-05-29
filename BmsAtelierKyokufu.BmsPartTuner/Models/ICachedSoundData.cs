namespace BmsAtelierKyokufu.BmsPartTuner.Models;

/// <summary>
/// 音声キャッシュデータの共通インターフェース。
/// BMS（事前正規化方式）と bmson（ポインタ方式）の両方に対応します。
/// </summary>
public interface ICachedSoundData : IDisposable
{
    /// <summary>ファイルのパスまたは識別子。</summary>
    string FilePath { get; }

    /// <summary>サンプリングレート。</summary>
    int SampleRate { get; }

    /// <summary>チャンネル数 (例: 2 = ステレオ)。</summary>
    int Channels { get; }

    /// <summary>サンプルあたりのビット数。</summary>
    int BitsPerSample { get; }

    /// <summary>ファイルサイズ (バイト)。</summary>
    long FileSize { get; }

    /// <summary>全体の総サンプル数。</summary>
    int TotalSamples { get; }

    /// <summary>全体のRMS (音圧)。比較前の高速フィルタリングに使用されます。</summary>
    float TotalRms { get; }

    /// <summary>先頭の無音サンプル数。</summary>
    int StartSilenceSamples { get; }

    /// <summary>有効な長さ (総サンプル数から先頭無音を除いたもの)。</summary>
    int EffectiveLength { get; }

    /// <summary>メモリ使用量の推定値 (MB)。</summary>
    double EstimatedMemoryMB { get; }

    /// <summary>事前正規化済みのデータを持っているかどうか (<c>true</c> = BMS用、<c>false</c> = bmson用)。</summary>
    bool IsPreNormalized { get; }

    /// <summary>
    /// 有音区間 (ActiveRegion) のリストを取得します。
    /// </summary>
    /// <returns>チャンネルごとの有音区間リストの配列。</returns>
    IReadOnlyList<ActiveRegion>[] GetActiveRegions();

    /// <summary>
    /// 指定されたチャンネルの生の波形データ (Span) を取得します。
    /// </summary>
    /// <param name="channel">チャンネル番号 (0 or 1)。</param>
    /// <param name="offset">オフセット (サンプル単位)。</param>
    /// <param name="length">長さ (サンプル単位)。</param>
    /// <returns>波形データの <see cref="ReadOnlySpan{T}"/>。</returns>
    ReadOnlySpan<float> GetRawSpan(int channel, int offset, int length);

}
