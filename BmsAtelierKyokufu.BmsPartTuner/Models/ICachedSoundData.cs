using System;

namespace BmsAtelierKyokufu.BmsPartTuner.Models;

/// <summary>
/// 音声キャッシュデータの共通インターフェース。
/// BMS（事前正規化方式）とbmson（ポインタ方式）の両方に対応するための抽象化。
/// </summary>
public interface ICachedSoundData : IDisposable
{
    /// <summary>ファイルのパス、または識別子。</summary>
    string FilePath { get; }

    /// <summary>サンプリングレート。</summary>
    int SampleRate { get; }

    /// <summary>チャンネル数（例: 2=ステレオ）。</summary>
    int Channels { get; }

    int BitsPerSample { get; }

    long FileSize { get; }

    /// <summary>全体の総サンプル数。</summary>
    int TotalSamples { get; }

    /// <summary>全体のRMS（音圧）。比較前の高速フィルタリングに使用。</summary>
    float TotalRms { get; }

    /// <summary>先頭の無音サンプル数。</summary>
    int StartSilenceSamples { get; }

    /// <summary>有効な長さ（総サンプル数 - 先頭無音）。</summary>
    int EffectiveLength { get; }

    /// <summary>メモリ使用量の推定値（MB）。</summary>
    double EstimatedMemoryMB { get; }

    /// <summary>事前正規化済みのデータを持っているか（true=BMS用, false=bmson用）。</summary>
    bool IsPreNormalized { get; }

    /// <summary>
    /// 有音区間（ActiveRegion）のリストを取得します。
    /// 事前正規化方式の場合、Data (float[]) が設定されています。
    /// </summary>
    System.Collections.Generic.IReadOnlyList<ActiveRegion>[] GetActiveRegions();

    /// <summary>
    /// 指定されたチャンネルの、生の波形データ（Span）を取得します。
    /// ポインタ方式（bmson）の場合に使用します。
    /// </summary>
    /// <param name="channel">チャンネル番号（0 or 1）</param>
    /// <param name="offset">オフセット（サンプル単位）</param>
    /// <param name="length">長さ（サンプル単位）</param>
    /// <returns>波形データ</returns>
    System.ReadOnlySpan<float> GetRawSpan(int channel, int offset, int length);

    /// <summary>
    /// 指定されたチャンネルの生データの総和（ΣY）を取得します。
    /// </summary>
    /// <param name="channel">チャンネル番号（0 or 1）</param>
    double GetChannelSum(int channel);

    /// <summary>
    /// 指定されたチャンネルの生データの二乗和（ΣY²）を取得します。
    /// </summary>
    /// <param name="channel">チャンネル番号（0 or 1）</param>
    double GetChannelSumSq(int channel);

    /// <summary>
    /// 指定されたチャンネルの、指定範囲における生データの総和（ΣY）を累積和から O(1) で取得します。
    /// </summary>
    /// <param name="channel">チャンネル番号（0 or 1）</param>
    /// <param name="offset">オフセット（サンプル単位）</param>
    /// <param name="length">長さ（サンプル単位）</param>
    double GetRangeSum(int channel, int offset, int length);

    /// <summary>
    /// 指定されたチャンネルの、指定範囲における生データの二乗和（ΣY²）を累積和から O(1) で取得します。
    /// </summary>
    /// <param name="channel">チャンネル番号（0 or 1）</param>
    /// <param name="offset">オフセット（サンプル単位）</param>
    /// <param name="length">長さ（サンプル単位）</param>
    double GetRangeSumSq(int channel, int offset, int length);

    /// <summary>
    /// 指定されたチャンネルの、LSH（Locality-Sensitive Hashing）の符号ビット配列を取得します。
    /// 64サンプルごとに1つのulong値にハッシュ化されています。
    /// </summary>
    /// <param name="channel">チャンネル番号（0 or 1）</param>
    System.ReadOnlySpan<ulong> GetLsh(int channel);

    /// <summary>
    /// LSH計算において、対象のブロックが無音（または微小ノイズ）ではなく、有効な波形データであるかを示すマスク配列を取得します。
    /// </summary>
    /// <param name="channel">チャンネル番号（0 or 1）</param>
    System.ReadOnlySpan<ulong> GetLshMask(int channel);
}
