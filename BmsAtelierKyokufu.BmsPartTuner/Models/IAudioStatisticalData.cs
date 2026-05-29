using MathNet.Numerics;
namespace BmsAtelierKyokufu.BmsPartTuner.Models;

/// <summary>
/// 音声データの高度な統計情報および特徴量（FFT, LSH, 累積和など）を提供するインターフェースです。
/// </summary>
[ADRAnchor("OPT-05", nameof(IAudioStatisticalData))]
public interface IAudioStatisticalData
{
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
    /// カスケード分類による事前足切り用の16次元ベクトル（FFT低周波ビンのL2正規化済み振幅）。
    /// </summary>
    float[]? SpectralFeatures { get; }

    /// <summary>
    /// 指定されたチャンネルの生データの総和を取得します。
    /// </summary>
    double GetChannelSum(int channel);

    /// <summary>
    /// 指定されたチャンネルの生データの二乗和を取得します。
    /// </summary>
    double GetChannelSumSq(int channel);

    /// <summary>
    /// 指定されたチャンネルの指定範囲における生データの総和を累積和から $O(1)$ で取得します。
    /// </summary>
    double GetRangeSum(int channel, int offset, int length);

    /// <summary>
    /// 指定されたチャンネルの指定範囲における生データの二乗和を累積和から $O(1)$ で取得します。
    /// </summary>
    double GetRangeSumSq(int channel, int offset, int length);

    /// <summary>
    /// 指定されたチャンネルの LSH (Locality-Sensitive Hashing) の符号ビット配列を取得します。
    /// </summary>
    ReadOnlySpan<ulong> GetLsh(int channel);

    /// <summary>
    /// LSH計算において、対象ブロックが有効な波形データであるかを示すマスク配列を取得します。
    /// </summary>
    ReadOnlySpan<ulong> GetLshMask(int channel);
}
