namespace BmsAtelierKyokufu.BmsPartTuner.Models;

/// <summary>
/// ベース波形の最適化データ（Prefix Sum、LSHなど）を保持します。
/// 多数の <see cref="PointerSoundData"/> から共有参照され、メモリ効率と $O(1)$ での計算を可能にします。
/// </summary>
/// <param name="SamplesPerChannel">チャンネルごとの正規化済みサンプル配列。</param>
/// <param name="PrefixSum">チャンネルごとの累積和（平均計算用）。</param>
/// <param name="PrefixSumSq">チャンネルごとの二乗累積和（RMS計算用）。</param>
/// <param name="SignLsh">チャンネルごとのLSH（Locality-Sensitive Hashing）シグネチャ。</param>
/// <param name="SignLshMask">LSHの有効ビットマスク。</param>
public record BaseAudioOptimizationData(
    float[][] SamplesPerChannel,
    double[][] PrefixSum,
    double[][] PrefixSumSq,
    ulong[][] SignLsh,
    ulong[][] SignLshMask
);
