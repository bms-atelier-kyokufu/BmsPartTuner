using MathNet.Numerics;

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
    /// 事前計算された周波数領域データ (FFTスペクトル)。
    /// 相互相関計算の高速化に使用されます。
    /// </summary>
    Complex32[][]? FftSpectrum { get; }

    /// <summary>
    /// シフト不変なLSH (SimHash) の256bitハッシュ値（ulong[4]）。
    /// XORとPOPCNTによる高速なハミング距離計算（スクリーニング）に利用します。
    /// </summary>
    ulong[]? SimHash256 { get; }

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

    /// <summary>
    /// 指定されたチャンネルの生データの総和を取得します。
    /// </summary>
    /// <param name="channel">チャンネル番号 (0 or 1)。</param>
    /// <returns>総和。</returns>
    double GetChannelSum(int channel);

    /// <summary>
    /// 指定されたチャンネルの生データの二乗和を取得します。
    /// </summary>
    /// <param name="channel">チャンネル番号 (0 or 1)。</param>
    /// <returns>二乗和。</returns>
    double GetChannelSumSq(int channel);

    /// <summary>
    /// 指定されたチャンネルの指定範囲における生データの総和を累積和から $O(1)$ で取得します。
    /// </summary>
    /// <param name="channel">チャンネル番号 (0 or 1)。</param>
    /// <param name="offset">オフセット (サンプル単位)。</param>
    /// <param name="length">長さ (サンプル単位)。</param>
    /// <returns>指定範囲の総和。</returns>
    double GetRangeSum(int channel, int offset, int length);

    /// <summary>
    /// 指定されたチャンネルの指定範囲における生データの二乗和を累積和から $O(1)$ で取得します。
    /// </summary>
    /// <param name="channel">チャンネル番号 (0 or 1)。</param>
    /// <param name="offset">オフセット (サンプル単位)。</param>
    /// <param name="length">長さ (サンプル単位)。</param>
    /// <returns>指定範囲の二乗和。</returns>
    double GetRangeSumSq(int channel, int offset, int length);

    /// <summary>
    /// 指定されたチャンネルの LSH (Locality-Sensitive Hashing) の符号ビット配列を取得します。
    /// </summary>
    /// <param name="channel">チャンネル番号 (0 or 1)。</param>
    /// <returns>LSHの符号ビット配列。</returns>
    ReadOnlySpan<ulong> GetLsh(int channel);

    /// <summary>
    /// LSH計算において、対象ブロックが有効な波形データであるかを示すマスク配列を取得します。
    /// </summary>
    /// <param name="channel">チャンネル番号 (0 or 1)。</param>
    /// <returns>LSH有効ビットマスク配列。</returns>
    ReadOnlySpan<ulong> GetLshMask(int channel);

    /// <summary>
    /// カスケード分類による事前足切り用の16次元ベクトル（FFT低周波ビンのL2正規化済み振幅）。
    /// </summary>
    float[]? SpectralFeatures { get; }
}
