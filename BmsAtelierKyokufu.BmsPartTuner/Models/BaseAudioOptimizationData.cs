namespace BmsAtelierKyokufu.BmsPartTuner.Models;

/// <summary>
/// ベース波形の最適化データを保持するクラス（Prefix Sum、LSH等）。
/// 数千の PointerSoundData から共有参照され、メモリ効率と $O(1)$ 計算を実現します。
/// </summary>
public record BaseAudioOptimizationData(
    float[][] SamplesPerChannel,
    double[][] PrefixSum,
    double[][] PrefixSumSq,
    ulong[][] SignLsh,
    ulong[][] SignLshMask
);
