using System.Numerics;
using GenerateSimdBatchUnrollAttribute = BmsAtelierKyokufu.BmsPartTuner.Core.Attributes.GenerateSimdBatchUnrollAttribute;

namespace BmsAtelierKyokufu.BmsPartTuner.Core.Audio.Comparison;

public static partial class WaveValidation
{
    #region ピアソンの相関係数計算 (波形全体)

    /// <summary>
    /// 波形1と波形2のピアソンの相関係数（ρ）をSIMDで計算します（配列版）。
    /// 音量（スケール）の違いに強く、波形の「形状」の相似性を評価します。
    /// </summary>
    static public float CalculatePearsonCorrelation(float[] wav1, float[] wav2)
    {
        if (wav1.Length != wav2.Length || wav1.Length == 0)
            return 0.0F;

        return CalculatePearsonCorrelationSIMD(wav1.AsSpan(), wav2.AsSpan());
    }

    /// <summary>
    /// ピアソンの相関係数を計算します（Span版・ゼロコピー対応・SIMD最適化・1パス計算）。
    /// 1パスでΣx, Σy, Σx², Σy², Σxyを計算し、高速に処理します。
    /// </summary>
    static public float CalculatePearsonCorrelationSIMD(ReadOnlySpan<float> wav1, ReadOnlySpan<float> wav2)
    {
        if (wav1.Length != wav2.Length || wav1.Length == 0)
            return 0.0F;

        int length = wav1.Length;

        // Guard for minimal data: if data is too short and identical, return 1.0 immediately
        if (length < 4)
        {
            bool identical = true;
            for (int i = 0; i < length; i++)
            {
                if (Math.Abs(wav1[i] - wav2[i]) > IdenticalTolerance)
                {
                    identical = false;
                    break;
                }
            }
            if (identical) return 1.0F;
        }

        int vectorSize = Vector<float>.Count;
        int vectorizedLength = length - (length % vectorSize);

        ProcessVectorizedPearson(wav1, wav2, vectorizedLength, vectorSize, out PearsonAccumulator accumulator);

        ProcessRemainderPearson(
            wav1, wav2, vectorizedLength, length, ref accumulator);

        double meanX = accumulator.SumX / length;
        double meanY = accumulator.SumY / length;

        double covXY = (accumulator.SumXY / length) - (meanX * meanY);

        double varX = (accumulator.SumX2 / length) - (meanX * meanX);
        double varY = (accumulator.SumY2 / length) - (meanY * meanY);

        // If both variances are near zero, check if data is identical
        if (varX < ZeroVarianceThreshold && varY < ZeroVarianceThreshold)
        {
            if (Math.Abs(meanX - meanY) < IdenticalTolerance)
                return 1.0F;
            else
                return 0.0F;
        }

        if (varX < ZeroVarianceThreshold || varY < ZeroVarianceThreshold)
            return 0.0F;

        double stdDevX = Math.Sqrt(varX);
        double stdDevY = Math.Sqrt(varY);
        double correlation = covXY / (stdDevX * stdDevY);

        return (float)Math.Max(-1.0, Math.Min(1.0, correlation));
    }

    /// <summary>
    /// ベクトル化された範囲の処理（ピアソン相関係数計算用）。
    /// Vector&lt;T&gt;を使用してSIMD並列演算を実行し、中間累積統計量を返します。
    /// </summary>
    [GenerateSimdBatchUnroll(UnrollFactor = 4, LogicType = "PearsonStats")]
    private static partial void ProcessVectorizedPearson(
        ReadOnlySpan<float> wav1,
        ReadOnlySpan<float> wav2,
        int vectorizedLength,
        int vectorSize,
        out PearsonAccumulator accumulator);

    /// <summary>
    /// 端数処理（ベクトル化できなかった残りの要素）（ピアソン相関係数計算用）。
    /// </summary>
    private static void ProcessRemainderPearson(
        ReadOnlySpan<float> wav1,
        ReadOnlySpan<float> wav2,
        int startIndex,
        int length,
        ref PearsonAccumulator accumulator)
    {
        for (int i = startIndex; i < length; i++)
        {
            float x = wav1[i];
            float y = wav2[i];

            accumulator.SumX += x;
            accumulator.SumY += y;
            accumulator.SumX2 += x * x;
            accumulator.SumY2 += y * y;
            accumulator.SumXY += x * y;
        }
    }

    #endregion
}
